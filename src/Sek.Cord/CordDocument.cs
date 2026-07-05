using Sek.Cord.Ast;
using Sek.Cord.Parsing;

namespace Sek.Cord;

/// <summary>
/// A parsed Cord "project": one or more <c>.cord</c> files merged, with helpers to
/// look up configurations/machines and to resolve effective switch values across
/// configuration inheritance and machine <c>where</c> overrides.
/// </summary>
public sealed class CordDocument
{
    public CordScript Script { get; }

    public CordDocument(CordScript script) => Script = script;

    public static CordDocument ParseText(string source) => new(Parser.ParseText(source));

    public static CordDocument LoadDirectory(string directory)
    {
        var merged = new CordScript();
        foreach (var file in Directory.EnumerateFiles(directory, "*.cord", SearchOption.AllDirectories).OrderBy(f => f))
        {
            var script = Parser.ParseText(File.ReadAllText(file));
            merged.Usings.AddRange(script.Usings);
            merged.Configurations.AddRange(script.Configurations);
            merged.Machines.AddRange(script.Machines);
        }

        return new CordDocument(merged);
    }

    public Configuration? GetConfiguration(string name) =>
        Script.Configurations.FirstOrDefault(c => c.Name == name);

    public Machine? GetMachine(string name) =>
        Script.Machines.FirstOrDefault(m => m.Name == name);

    /// <summary>Effective switches for a configuration, honoring base-config inheritance.</summary>
    public Dictionary<string, string> ResolveConfigSwitches(string configName)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var visited = new HashSet<string>();
        void Walk(string name)
        {
            if (!visited.Add(name)) return;
            var cfg = GetConfiguration(name);
            if (cfg is null) return;
            foreach (var b in cfg.BaseConfigs) Walk(b);   // bases first
            foreach (var kv in cfg.Switches) result[kv.Key] = kv.Value; // derived overrides
        }

        Walk(configName);
        return result;
    }

    /// <summary>Effective switches for a machine: its base configs plus its own where-overrides.</summary>
    public Dictionary<string, string> ResolveMachineSwitches(string machineName)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var machine = GetMachine(machineName);
        if (machine is null) return result;

        foreach (var baseCfg in machine.BaseConfigs)
        {
            foreach (var kv in ResolveConfigSwitches(baseCfg)) result[kv.Key] = kv.Value;
        }

        foreach (var kv in machine.Switches) result[kv.Key] = kv.Value;
        return result;
    }

    /// <summary>All action targets declared/imported across configs (for validation).</summary>
    public IEnumerable<string> AllDeclaredActionTargets()
    {
        foreach (var c in Script.Configurations)
        {
            foreach (var t in c.DeclaredActions) yield return t.Target;
        }
    }
    /// <summary>Effective declared actions for a configuration (base-first, derived overrides).</summary>
    public Dictionary<string, DeclaredAction> ResolveDeclaredActions(string configName)
    {
        var result = new Dictionary<string, DeclaredAction>(StringComparer.Ordinal);
        var visited = new HashSet<string>();
        void Walk(string name)
        {
            if (!visited.Add(name)) return;
            var cfg = GetConfiguration(name);
            if (cfg is null) return;
            foreach (var b in cfg.BaseConfigs) Walk(b);
            foreach (var da in cfg.DeclaredActions) result[da.Target] = da;
        }

        Walk(configName);
        return result;
    }

    /// <summary>Effective declared actions visible to a machine (across its base configs).</summary>
    public Dictionary<string, DeclaredAction> ResolveMachineDeclaredActions(string machineName)
    {
        var result = new Dictionary<string, DeclaredAction>(StringComparer.Ordinal);
        var machine = GetMachine(machineName);
        if (machine is null) return result;
        foreach (var baseCfg in machine.BaseConfigs)
        {
            foreach (var kv in ResolveDeclaredActions(baseCfg)) result[kv.Key] = kv.Value;
        }

        return result;
    }

    /// <summary>Adapter types imported via <c>action all T</c> across a config and its bases.</summary>
    public List<string> ResolveImportedActionTypes(string configName)
    {
        var result = new List<string>();
        var visited = new HashSet<string>();
        void Walk(string name)
        {
            if (!visited.Add(name)) return;
            var cfg = GetConfiguration(name);
            if (cfg is null) return;
            foreach (var b in cfg.BaseConfigs) Walk(b);
            foreach (var t in cfg.ImportedActionTypes) result.Add(t);
        }

        Walk(configName);
        return result;
    }

    /// <summary>Adapter types imported via <c>action all T</c> visible to a machine.</summary>
    public List<string> ResolveMachineImportedActionTypes(string machineName)
    {
        var result = new List<string>();
        var machine = GetMachine(machineName);
        if (machine is null) return result;
        foreach (var baseCfg in machine.BaseConfigs) result.AddRange(ResolveImportedActionTypes(baseCfg));
        return result;
    }
}
