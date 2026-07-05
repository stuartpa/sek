namespace Sek.Cord.Ast;

/// <summary>Root of a parsed Cord script.</summary>
public sealed class CordScript
{
    public List<string> Usings { get; } = new();
    public List<Configuration> Configurations { get; } = new();
    public List<Machine> Machines { get; } = new();
}

/// <summary>A <c>config</c> declaration.</summary>
public sealed class Configuration
{
    public string Name { get; set; } = string.Empty;
    public List<string> BaseConfigs { get; } = new();

    /// <summary><c>action all T</c> imported action types.</summary>
    public List<string> ImportedActionTypes { get; } = new();

    /// <summary>Declared actions (<c>action ... X.Y(params) where {. ... .}</c>).</summary>
    public List<DeclaredAction> DeclaredActions { get; } = new();

    public Dictionary<string, string> Switches { get; } = new();
}

/// <summary>A declared action with its parameter list and optional <c>where</c> constraint code.</summary>
public sealed class DeclaredAction
{
    public string Target { get; set; } = string.Empty;
    public List<Parameter> Parameters { get; } = new();

    /// <summary>Raw embedded C# from the <c>where {. ... .}</c> block, if any.</summary>
    public string? WhereCode { get; set; }
}

public sealed class Parameter
{
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

/// <summary>A <c>machine</c> declaration.</summary>
public sealed class Machine
{
    public string Name { get; set; } = string.Empty;
    public List<Parameter> Parameters { get; } = new();
    public List<string> BaseConfigs { get; } = new();
    public Dictionary<string, string> Switches { get; } = new();
    public Behavior? Body { get; set; }
}

public abstract class Behavior
{
    /// <summary>All action/machine targets referenced anywhere in this behavior.</summary>
    public IEnumerable<string> ReferencedTargets()
    {
        switch (this)
        {
            case InvocationBehavior inv:
                yield return inv.Target;
                break;
            case SequenceBehavior s:
                foreach (var r in s.Items.SelectMany(i => i.ReferencedTargets())) yield return r;
                break;
            case ChoiceBehavior c:
                foreach (var r in c.Items.SelectMany(i => i.ReferencedTargets())) yield return r;
                break;
            case ParallelBehavior p:
                foreach (var r in p.Items.SelectMany(i => i.ReferencedTargets())) yield return r;
                break;
            case PermutationBehavior pm:
                foreach (var r in pm.Items.SelectMany(i => i.ReferencedTargets())) yield return r;
                break;
            case LooseSequenceBehavior ls:
                foreach (var r in ls.Items.SelectMany(i => i.ReferencedTargets())) yield return r;
                break;
            case RepetitionBehavior rep:
                foreach (var r in rep.Inner.ReferencedTargets()) yield return r;
                break;
            case GroupBehavior g:
                foreach (var r in g.Inner.ReferencedTargets()) yield return r;
                break;
            case PreconstraintBehavior pc:
                foreach (var r in pc.Inner.ReferencedTargets()) yield return r;
                break;
            case LetBehavior l:
                foreach (var r in l.Inner.ReferencedTargets()) yield return r;
                break;
            case BindBehavior b:
                foreach (var r in b.Inner.ReferencedTargets()) yield return r;
                break;
            case ConstructBehavior cb when cb.Target is not null:
                foreach (var r in cb.Target.ReferencedTargets()) yield return r;
                break;
        }
    }

    /// <summary>The first <see cref="ConstructBehavior"/> anywhere in this behavior, if any.</summary>
    public ConstructBehavior? FindConstruct()
    {
        switch (this)
        {
            case ConstructBehavior cb: return cb;
            case SequenceBehavior s: return s.Items.Select(i => i.FindConstruct()).FirstOrDefault(x => x != null);
            case ChoiceBehavior c: return c.Items.Select(i => i.FindConstruct()).FirstOrDefault(x => x != null);
            case ParallelBehavior p: return p.Items.Select(i => i.FindConstruct()).FirstOrDefault(x => x != null);
            case PermutationBehavior pm: return pm.Items.Select(i => i.FindConstruct()).FirstOrDefault(x => x != null);
            case LooseSequenceBehavior ls: return ls.Items.Select(i => i.FindConstruct()).FirstOrDefault(x => x != null);
            case RepetitionBehavior r: return r.Inner.FindConstruct();
            case GroupBehavior g: return g.Inner.FindConstruct();
            case PreconstraintBehavior pc: return pc.Inner.FindConstruct();
            case LetBehavior l: return l.Inner.FindConstruct();
            case BindBehavior b: return b.Inner.FindConstruct();
            default: return null;
        }
    }
}

public sealed class SequenceBehavior : Behavior { public List<Behavior> Items { get; } = new(); }
public sealed class ChoiceBehavior : Behavior { public List<Behavior> Items { get; } = new(); }
public sealed class ParallelBehavior : Behavior { public List<Behavior> Items { get; } = new(); public string Op { get; set; } = "sync"; }
public sealed class PermutationBehavior : Behavior { public List<Behavior> Items { get; } = new(); }
public sealed class LooseSequenceBehavior : Behavior { public List<Behavior> Items { get; } = new(); }
public sealed class GroupBehavior : Behavior { public Behavior Inner { get; set; } = null!; }

public sealed class RepetitionBehavior : Behavior
{
    public Behavior Inner { get; set; } = null!;
    public string Op { get; set; } = "*"; // * + ? or {n..m}
    public int? Min { get; set; }
    public int? Max { get; set; }
}

public sealed class InvocationBehavior : Behavior
{
    public string Target { get; set; } = string.Empty;
    public List<string>? Args { get; set; } // null => parentheses omitted (all params unknown)
    public bool Negated { get; set; }
    public string? Qualifier { get; set; } // call | return | event
}

public sealed class PreconstraintBehavior : Behavior
{
    public string Code { get; set; } = string.Empty; // embedded C# from {. .}
    public Behavior Inner { get; set; } = null!;
}

public sealed class LetBehavior : Behavior
{
    public string Raw { get; set; } = string.Empty;
    public Behavior Inner { get; set; } = null!;

    /// <summary>Local variable declarations (<c>let Type name, ...</c>).</summary>
    public List<Parameter> Vars { get; } = new();

    /// <summary>Embedded C# from the <c>where {. ... .}</c> block (Condition.In / Combination.*).</summary>
    public string? WhereCode { get; set; }
}

public enum ConstructKind { ModelProgram, AcceptingPaths, TestCases, BoundedExploration, PointShoot, AcceptCompletion, RequirementCoverage }

public sealed class ConstructBehavior : Behavior
{
    public ConstructKind Kind { get; set; }
    public string Reference { get; set; } = string.Empty; // config (model program) or machine (paths/tests)
    public string? Where { get; set; }

    /// <summary>Parsed <c>where key = value</c> options (e.g. PathDepth, Shoot, Completer).</summary>
    public Dictionary<string, string> Params { get; } = new();

    /// <summary>When the <c>for</c> target is itself a behavior/construct rather than a named machine.</summary>
    public Behavior? Target { get; set; }
}

/// <summary><c>bind Action(argDomains), ... in Behavior</c>: binds parameter domains for the
/// named actions within the inner behavior.</summary>
public sealed class BindBehavior : Behavior
{
    public List<BindClause> Binds { get; } = new();
    public Behavior Inner { get; set; } = null!;
}

public sealed class BindClause
{
    public string Action { get; set; } = string.Empty;
    /// <summary>Per-parameter bound domain (each entry is a list of candidate value tokens; a
    /// single <c>_</c> means "unbound / any").</summary>
    public List<List<string>> ArgDomains { get; } = new();
}
