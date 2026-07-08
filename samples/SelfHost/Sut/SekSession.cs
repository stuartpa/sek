using System;
using System.Diagnostics;
using System.IO;

namespace SelfHost.Sut
{
    /// <summary>
    /// A stateful system-under-test that drives the <b>real</b> <c>sek</c> CLI through a project
    /// lifecycle. It is SEK validating SEK — the SUT is the actual tool. Each instance owns a
    /// <b>fresh, un-initialised</b> workspace (a copy of the Turnstile sample's Cord, pointed at the
    /// real Turnstile model/adapter assemblies) so that the CLI's ordering rules are exercised for
    /// real: before <c>init</c> there is no project config, so <c>validate</c>/<c>explore</c>/
    /// <c>generate</c>/<c>test</c> genuinely fail; and <c>view</c> fails until an <c>explore</c> has
    /// written a graph. Every action either succeeds or throws — so the model-derived negative tests
    /// (attempt an illegal action, assert rejection) verify the real CLI's real error behaviour.
    /// </summary>
    public sealed class SekSession
    {
        private readonly string _repoRoot;
        private readonly string _sekDll;
        private readonly string _work;      // this session's fresh workspace
        private readonly string _configJson;

        public SekSession()
        {
            _repoRoot = FindRepoRoot();
            _sekDll = Path.Combine(_repoRoot, "src", "Sek.Cli", "bin", "Debug", "sek.dll");

            var turnstile = Path.Combine(_repoRoot, "samples", "Turnstile");

            // A fresh, deliberately UN-initialised workspace: the Cord is present, but there is no
            // .specexplorerkit/config.json until Init() writes one. Assembly paths are absolute, so
            // the workspace reuses the real (already-built) Turnstile model + adapter.
            _work = Path.Combine(Path.GetTempPath(), "sek_selfhost_" + Guid.NewGuid().ToString("N"));
            var modelDir = Path.Combine(_work, "Model");
            Directory.CreateDirectory(modelDir);
            Directory.CreateDirectory(Path.Combine(_work, ".specexplorerkit", "out"));
            foreach (var cord in Directory.EnumerateFiles(Path.Combine(turnstile, "Model"), "*.cord"))
            {
                File.Copy(cord, Path.Combine(modelDir, Path.GetFileName(cord)), overwrite: true);
            }

            var modelDll = Path.Combine(turnstile, "Sut", "..", "Model", "bin", "Debug", "Turnstile.Model.dll");
            var sutDll = Path.Combine(turnstile, "Sut", "bin", "Debug", "Turnstile.Sut.dll");
            _configJson =
                "{\n" +
                $"  \"model\":   {{ \"assembly\": {Json(Path.GetFullPath(modelDll))}, \"type\": \"Turnstile.Model.TurnstileModel\" }},\n" +
                "  \"cord\":    \"Model\",\n" +
                $"  \"binding\": {{ \"assembly\": {Json(Path.GetFullPath(sutDll))}, \"namespace\": \"Turnstile.Sut\" }},\n" +
                "  \"out\":     \".specexplorerkit/out\"\n" +
                "}\n";
        }

        /// <summary>Rule <c>SekSession.Init</c>: `sek init` — write the project config (idempotent).
        /// Always legal; establishes the project.</summary>
        public void Init()
        {
            // Mirror what `sek init` does, but with a config wired to the real Turnstile assemblies
            // (a bare `sek init` scaffolds a placeholder config that wouldn't resolve a real model).
            File.WriteAllText(Path.Combine(_work, ".specexplorerkit", "config.json"), _configJson);
        }

        /// <summary>Rule <c>SekSession.Validate</c>: `sek validate` — needs an initialized project.</summary>
        public void Validate() => RunOk("validate", "--project", _work);

        /// <summary>Rule <c>SekSession.Explore</c>: `sek explore` → a .seexpl graph. Needs init.</summary>
        public void Explore() => RunOk("explore", "ModelProgram", "--project", _work);

        /// <summary>Rule <c>SekSession.View</c>: `sek view` the produced graph. Needs a prior explore.</summary>
        public void View()
        {
            var seexpl = Path.Combine(_work, ".specexplorerkit", "out", "ModelProgram.seexpl");
            var outFile = Path.Combine(Path.GetTempPath(), "selfhost_view_" + Guid.NewGuid().ToString("N") + ".dot");
            RunOk("view", seexpl, "--format", "dot", "--out", outFile);
            try { File.Delete(outFile); } catch { /* best effort */ }
        }

        /// <summary>Rule <c>SekSession.Generate</c>: `sek generate` an xUnit project. Needs init.</summary>
        public void Generate()
        {
            var outDir = Path.Combine(Path.GetTempPath(), "selfhost_gen_" + Guid.NewGuid().ToString("N"));
            RunOk("generate", "ModelProgram", "--project", _work, "--out", outDir, "--max", "3");
            try { Directory.Delete(outDir, recursive: true); } catch { /* best effort */ }
        }

        /// <summary>Rule <c>SekSession.Test</c>: `sek test` — explore + replay conformance. Needs init.</summary>
        public void Test() => RunOk("test", "ModelProgram", "--project", _work);

        /// <summary>Runs the real `sek` CLI and throws on a non-zero exit (a rejection). This is what
        /// makes the model-derived negative tests meaningful: an illegal action drives the CLI to a
        /// real error, which surfaces here as an exception the test harness asserts.</summary>
        private void RunOk(params string[] args)
        {
            var psi = new ProcessStartInfo("dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            psi.ArgumentList.Add(_sekDll);
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi)!;
            var err = p.StandardError.ReadToEnd();
            p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            if (p.ExitCode != 0)
            {
                throw new InvalidOperationException("sek " + string.Join(' ', args) + " failed: " + err.Trim());
            }
        }

        private static string Json(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        private static string FindRepoRoot()
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !Directory.Exists(Path.Combine(d.FullName, "samples")))
            {
                d = d.Parent;
            }

            return d?.FullName ?? AppContext.BaseDirectory;
        }
    }
}
