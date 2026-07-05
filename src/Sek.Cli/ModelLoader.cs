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
    /// Resolves the model-program type for a Cord <c>scope</c> (e.g.
    /// <c>construct model program … where scope = "PG.ModelWithStruct"</c>): the single concrete
    /// <see cref="Sek.Modeling.ModelProgram"/> subclass whose namespace equals the scope. Falls
    /// back to <paramref name="defaultTypeName"/> when the scope is empty.
    /// </summary>
    public static Type LoadModelTypeInScope(string assemblyPath, string? scope, string defaultTypeName)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return LoadModelType(assemblyPath, defaultTypeName);
        }

        var full = Path.GetFullPath(assemblyPath);
        if (!File.Exists(full))
        {
            throw new FileNotFoundException(
                $"Model assembly not found: {full}. Build the model project first (dotnet build).");
        }

        InstallProbingResolver(Path.GetDirectoryName(full)!);
        var asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(full);

        var candidates = asm.GetTypes()
            .Where(t => !t.IsAbstract && typeof(Sek.Modeling.ModelProgram).IsAssignableFrom(t) && t.Namespace == scope)
            .ToList();

        if (candidates.Count == 1)
        {
            return candidates[0];
        }

        if (candidates.Count > 1)
        {
            throw new InvalidOperationException($"Scope '{scope}' matches {candidates.Count} model programs in '{full}'; expected one.");
        }

        throw new InvalidOperationException($"No model program found in scope (namespace) '{scope}' in '{full}'.");
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
