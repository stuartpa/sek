using System.Runtime.Loader;

namespace Sek.Cli;

/// <summary>Loads a model program type from a built model assembly.</summary>
public static class ModelLoader
{
    private static readonly HashSet<string> ProbeDirs = new(StringComparer.OrdinalIgnoreCase);
    private static bool _resolverInstalled;

    public static Type LoadModelType(string assemblyPath, string typeName)
    {
        var full = Path.GetFullPath(assemblyPath);
        if (!File.Exists(full))
        {
            throw new FileNotFoundException(
                $"Model assembly not found: {full}. Build the model project first (dotnet build).");
        }

        InstallProbingResolver(Path.GetDirectoryName(full)!);

        var asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(full);
        return asm.GetType(typeName)
               ?? throw new InvalidOperationException($"Type '{typeName}' not found in '{full}'.");
    }

    /// <summary>
    /// Register a directory to probe for dependencies not already in the tool's app
    /// base. Handles model and binding assembly dependency graphs (e.g. Adapter -> Tpcc).
    /// </summary>
    public static void InstallProbingResolver(string directory)
    {
        ProbeDirs.Add(Path.GetFullPath(directory));

        if (_resolverInstalled)
        {
            return;
        }

        _resolverInstalled = true;
        AssemblyLoadContext.Default.Resolving += (ctx, name) =>
        {
            foreach (var dir in ProbeDirs)
            {
                var candidate = Path.Combine(dir, name.Name + ".dll");
                if (File.Exists(candidate))
                {
                    return ctx.LoadFromAssemblyPath(candidate);
                }
            }

            return null;
        };
    }
}
