using System.Reflection;
using Sek.Modeling;

namespace Sek.Engine;

/// <summary>Reflected description of a rule parameter and the domain that feeds it.</summary>
public sealed record RuleParameter(string Name, Type Type, string DomainMethod);

/// <summary>Reflected description of a model rule (an action).</summary>
public sealed record RuleInfo(string ActionLabel, MethodInfo Method, IReadOnlyList<RuleParameter> Parameters);

/// <summary>
/// Reflects a <see cref="ModelProgram"/> subclass into the rules, domain methods and
/// accepting-condition methods the explorer needs.
/// </summary>
public sealed class ModelIntrospector
{
    public Type ModelType { get; }
    public IReadOnlyList<RuleInfo> Rules { get; }
    public IReadOnlyList<MethodInfo> AcceptingConditions { get; }

    private const BindingFlags AllInstanceOrStatic =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    public ModelIntrospector(Type modelType)
    {
        if (!typeof(ModelProgram).IsAssignableFrom(modelType))
        {
            throw new ArgumentException($"'{modelType.FullName}' does not derive from Sek.Modeling.ModelProgram.");
        }

        ModelType = modelType;

        var rules = new List<RuleInfo>();
        foreach (var method in modelType.GetMethods(AllInstanceOrStatic))
        {
            var ruleAttr = method.GetCustomAttribute<RuleAttribute>();
            if (ruleAttr is null)
            {
                continue;
            }

            var label = ruleAttr.Action ?? $"{method.DeclaringType?.Name}.{method.Name}";

            var parameters = new List<RuleParameter>();
            foreach (var p in method.GetParameters())
            {
                // [Domain(...)] is optional. When absent, the parameter's candidate values
                // come from Cord `where { Condition.In(...) }` constraints (solver-driven),
                // or from the type's natural domain (enum members / bool) for enums and bools.
                var domain = p.GetCustomAttribute<DomainAttribute>();
                parameters.Add(new RuleParameter(p.Name ?? "arg", p.ParameterType, domain?.MethodName ?? string.Empty));
            }

            rules.Add(new RuleInfo(label, method, parameters));
        }

        // Deterministic action ordering.
        Rules = rules.OrderBy(r => r.ActionLabel, StringComparer.Ordinal).ToList();

        AcceptingConditions = modelType
            .GetMethods(AllInstanceOrStatic)
            .Where(m => m.GetCustomAttribute<AcceptingConditionAttribute>() is not null)
            .OrderBy(m => m.Name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Resolve a named domain method (instance or static) on the model.</summary>
    public MethodInfo GetDomainMethod(string name)
    {
        var m = ModelType.GetMethod(name, AllInstanceOrStatic, binder: null, types: Type.EmptyTypes, modifiers: null);
        return m ?? throw new InvalidOperationException($"Domain method '{name}' not found on '{ModelType.Name}'.");
    }
}
