using System;
using System.Diagnostics;
using System.IO;

namespace SelfHost.Sut
{
    /// <summary>
    /// A stateful system-under-test that drives the <b>real</b> <c>sek</c> CLI against the Turnstile
    /// sample. It is SEK validating SEK — the SUT is the actual tool, not a re-implementation. It
    /// exercises the full command surface (init/validate/explore/view/generate/test) plus the tool's
    /// error behaviour (view of a missing file, explore of an unknown machine). The one genuine
    /// ordering constraint it captures is that <c>view</c> can only render a graph a prior
    /// <c>explore</c> produced.
    /// </summary>
    public sealed class SekSession
    {
        private readonly string _repoRoot;
        private readonly string _sekDll;
        private readonly string _turnstile;
        private bool _explored;

        public SekSession()
        {
            _repoRoot = FindRepoRoot();
            _sekDll = Path.Combine(_repoRoot, "src", "Sek.Cli", "bin", "Debug", "sek.dll");
            _turnstile = Path.Combine(_repoRoot, "samples", "Turnstile");
        }

        /// <summary>Rule <c>SekSession.Init</c>: `sek init` is idempotent against an existing project.</summary>
        public void Init() => RunOk("init", "--project", _turnstile);

        /// <summary>Rule <c>SekSession.Validate</c>: the CLI validates the Turnstile project.</summary>
        public void Validate() => RunOk("validate", "--project", _turnstile);

        /// <summary>Rule <c>SekSession.Explore</c>: explore Turnstile's ModelProgram → a .seexpl graph.</summary>
        public void Explore()
        {
            RunOk("explore", "ModelProgram", "--project", _turnstile);
            _explored = true;
        }

        /// <summary>Rule <c>SekSession.View</c>: render the produced graph. The graph only exists after
        /// a prior explore, so this is the workflow's real ordering constraint.</summary>
        public void View()
        {
            var seexpl = Path.Combine(_turnstile, ".specexplorerkit", "out", "ModelProgram.seexpl");
            if (!_explored || !File.Exists(seexpl))
            {
                throw new InvalidOperationException("view requires a graph produced by a prior explore");
            }

            var outFile = Path.Combine(Path.GetTempPath(), "selfhost_" + Guid.NewGuid().ToString("N") + ".dot");
            RunOk("view", seexpl, "--format", "dot", "--out", outFile);
            try { File.Delete(outFile); } catch { /* best effort */ }
        }

        /// <summary>Rule <c>SekSession.Generate</c>: `sek generate` explores internally and emits an
        /// xUnit test project (written to a throwaway temp dir here).</summary>
        public void Generate()
        {
            var outDir = Path.Combine(Path.GetTempPath(), "selfhost_gen_" + Guid.NewGuid().ToString("N"));
            RunOk("generate", "ModelProgram", "--project", _turnstile, "--out", outDir, "--max", "3");
            try { Directory.Delete(outDir, recursive: true); } catch { /* best effort */ }
        }

        /// <summary>Rule <c>SekSession.Test</c>: `sek test` explores internally and replays conformance
        /// against the Turnstile SUT.</summary>
        public void Test() => RunOk("test", "ModelProgram", "--project", _turnstile);

        /// <summary>Rule <c>SekSession.ViewMissing</c>: error transition — `sek view` of a missing file
        /// must fail cleanly with a non-zero exit.</summary>
        public void ViewMissing()
        {
            var missing = Path.Combine(Path.GetTempPath(), "selfhost_missing_" + Guid.NewGuid().ToString("N") + ".seexpl");
            RunFail("view", missing);
        }

        /// <summary>Rule <c>SekSession.ExploreUnknown</c>: error transition — `sek explore` of an unknown
        /// machine must fail cleanly with a non-zero exit.</summary>
        public void ExploreUnknown() => RunFail("explore", "NoSuchMachine", "--project", _turnstile);

        private void RunOk(params string[] args)
        {
            var (exit, err) = Run(args);
            if (exit != 0)
            {
                throw new InvalidOperationException("sek " + string.Join(' ', args) + " failed: " + err);
            }
        }

        private void RunFail(params string[] args)
        {
            var (exit, _) = Run(args);
            if (exit == 0)
            {
                throw new InvalidOperationException("sek " + string.Join(' ', args) + " was expected to fail but exited 0");
            }
        }

        private (int exit, string err) Run(string[] args)
        {
            var psi = new ProcessStartInfo("dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            psi.ArgumentList.Add(_sekDll);
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi);
            var err = p.StandardError.ReadToEnd();
            p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            return (p.ExitCode, err);
        }

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
