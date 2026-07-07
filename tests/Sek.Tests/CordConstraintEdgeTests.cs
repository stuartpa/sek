using System.Linq;
using Sek.Cord;
using Sek.Cord.Ast;
using SpecExplorerKit.Components.Solving;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Edge coverage for <c>CordConstraintExtractor</c>'s literal parsing and comment stripping:
/// bool / long / enum-name literals in <c>Condition.In</c>, block comments in a where-block, and
/// multi-value domains.
/// </summary>
public class CordConstraintEdgeTests
{
    private static DeclaredAction Act(string where, params (string type, string name)[] ps)
    {
        var a = new DeclaredAction { Target = "SUT.M", WhereCode = where };
        foreach (var (t, n) in ps) a.Parameters.Add(new Parameter { Type = t, Name = n });
        return a;
    }

    private static InConstraint In(DeclaredAction a) =>
        CordConstraintExtractor.Extract(a).Constraints.OfType<InConstraint>().Single();

    [Fact]
    public void ParseLiteral_BoolValues()
    {
        var c = In(Act("Condition.In(flag, true, false);", ("bool", "flag")));
        Assert.Contains(true, c.Values);
        Assert.Contains(false, c.Values);
    }

    [Fact]
    public void ParseLiteral_IntAndLongOverflow()
    {
        var c = In(Act("Condition.In(n, 60, 9999999999);", ("long", "n")));
        var nums = c.Values.Select(v => System.Convert.ToInt64(v)).OrderBy(x => x).ToList();
        Assert.Equal(new long[] { 60, 9999999999L }, nums);
        Assert.Contains(c.Values, v => v is long); // the overflowing value is kept as long
    }

    [Fact]
    public void ParseLiteral_EnumMemberName_FallsBackToToken()
    {
        var c = In(Act("Condition.In(day, Monday, Tuesday);", ("DayOfWeek", "day")));
        Assert.Contains("Monday", c.Values);
        Assert.Contains("Tuesday", c.Values);
    }

    [Fact]
    public void ParseLiteral_QuotedStrings_Unquote()
    {
        var c = In(Act("Condition.In(name, \"a\", \"b c\");", ("string", "name")));
        Assert.Contains("a", c.Values);
        Assert.Contains("b c", c.Values);
    }

    [Fact]
    public void BlockComment_IsStripped_SoStatementSurvives()
    {
        var where = "/* choose the domain */\nCondition.In(name, \"x\", \"y\");";
        var c = In(Act(where, ("string", "name")));
        Assert.Equal(2, c.Values.Count);
    }

    [Fact]
    public void CommentInsideString_IsPreserved()
    {
        // a "//"-like sequence inside a string literal must not be treated as a comment
        var where = "Condition.In(url, \"http://x\", \"y\");";
        var c = In(Act(where, ("string", "url")));
        Assert.Contains("http://x", c.Values);
    }
}
