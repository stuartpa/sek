using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Sek.Tests;

/// <summary>
/// Invokes the <c>sek</c> CLI's top-level entry point in-process via reflection, capturing the
/// exit code and console output. Lets tests drive real commands (init/validate/explore/view/
/// generate/test) so the CLI and the deep <see cref="Sek.Engine.Explorer"/> paths reached through
/// its Interpret flow are covered by the test host — without refactoring <c>Program.cs</c>.
/// </summary>
internal static class CliHost
{
    private static readonly MethodInfo Entry = ResolveEntry();

    private static MethodInfo ResolveEntry()
    {
        var asm = typeof(Sek.Cli.ProjectConfig).Assembly;
        var program = asm.GetType("Program", throwOnError: true)!;
        return program.GetMethod("<Main>$", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
               ?? program.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                   .First(m => m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string[]));
    }

    private static readonly object Gate = new();

    public static (int Code, string Out, string Err) Run(params string[] args)
    {
        // The CLI uses process-wide Console; serialize in-process invocations.
        lock (Gate)
        {
            var origOut = Console.Out;
            var origErr = Console.Error;
            var so = new StringWriter();
            var se = new StringWriter();
            Console.SetOut(so);
            Console.SetError(se);
            try
            {
                var result = Entry.Invoke(null, new object[] { args });
                return (result is int i ? i : 0, so.ToString(), se.ToString());
            }
            catch (TargetInvocationException tie)
            {
                return (99, so.ToString(), se.ToString() + tie.InnerException?.Message);
            }
            finally
            {
                Console.SetOut(origOut);
                Console.SetError(origErr);
            }
        }
    }

    /// <summary>Walks up from the test output directory to the repository root (the folder that
    /// contains <c>samples/</c>).</summary>
    public static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "samples")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? AppContext.BaseDirectory;
    }
}
