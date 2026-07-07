using System;
using System.Diagnostics;
using System.IO;

namespace SelfHost.Sut
{
    /// <summary>
    /// A stateful system-under-test that drives the <b>real</b> <c>sek</c> CLI against the Turnstile
    /// sample. It captures a genuine SEK workflow constraint: <c>view</c> can only render a graph
    /// that a prior <c>explore</c> produced. This is SEK validating SEK — the SUT is the actual tool,
    /// not a re-implementation.
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
            using var p = Process.Start(psi);
            var err = p.StandardError.ReadToEnd();
            p.WaitForExit();
            if (p.ExitCode != 0)
            {
                throw new InvalidOperationException("sek " + string.Join(' ', args) + " failed: " + err);
            }
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
