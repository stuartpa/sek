namespace Sek.Modeling;

/// <summary>
/// Associates a rule parameter with a domain method: a public method on the model
/// (instance or static) returning an <see cref="System.Collections.IEnumerable"/> of
/// candidate values for that parameter during exploration.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class DomainAttribute : Attribute
{
    public string MethodName { get; }

    public DomainAttribute(string methodName)
    {
        MethodName = methodName;
    }
}
