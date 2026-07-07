using Sek.Cord;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Line/branch coverage for the Cord lexer and the rarer parser productions: string escape
/// sequences, line and block comments, the behaviour operators (choice / repetition / negation /
/// grouping / bounded repetition), switch value kinds, machine parameter modifiers and var/base
/// lists, action-kind modifiers, and the lexer/parser error paths.
/// </summary>
public class CordLexerGrammarCoverageTests
{
    private static CordDocument Parse(string text) => CordDocument.ParseText(text);

    [Fact]
    public void StringEscapes_AreDecoded()
    {
        var cord = Parse("config C { switch S = \"a\\nb\\tc\\rd\\\"e\\\\f\\qg\"; }\n");
        Assert.Contains("\n", cord.Script.Configurations[0].Switches["S"]);
        Assert.Contains("\t", cord.Script.Configurations[0].Switches["S"]);
    }

    [Fact]
    public void LineAndBlockComments_AreSkipped()
    {
        var cord = Parse(
            "// a line comment\n" +
            "config C { /* block\n comment */ switch K = 1; }\n" +
            "machine M() : C { construct model program from C } // trailing\n");
        Assert.Single(cord.Script.Machines);
        Assert.Equal("1", cord.Script.Configurations[0].Switches["K"]);
    }

    [Fact]
    public void SwitchValueKinds_StringIntIdentifier()
    {
        var cord = Parse("config C { switch A = \"str\"; switch B = 42; switch D = someIdent; }\n");
        var sw = cord.Script.Configurations[0].Switches;
        Assert.Equal("str", sw["A"]);
        Assert.Equal("42", sw["B"]);
        Assert.Equal("someIdent", sw["D"]);
    }

    [Fact]
    public void BehaviorOperators_Parse()
    {
        var cord = Parse(
            "config C { action all S; }\n" +
            "machine M() : C { (A | B)* ; !D ; E+ ; F? ; G{2} ; H{1..3} }\n");
        Assert.NotNull(cord.GetMachine("M")!.Body);
    }

    [Fact]
    public void MachineParameters_OutRef_VarDecl_MultiBase()
    {
        var cord = Parse(
            "config C { }\nconfig D { }\n" +
            "machine M(out int x, ref string y) / int z : C, D { A }\n");
        var m = cord.GetMachine("M")!;
        Assert.Equal(2, m.Parameters.Count);
        Assert.Contains("C", m.BaseConfigs);
        Assert.Contains("D", m.BaseConfigs);
    }

    [Fact]
    public void ActionKinds_And_WhereForms()
    {
        var cord = Parse(
            "config C {\n" +
            "  action all S;\n" +
            "  action event void S.Ev();\n" +
            "  action exclude void S.Ex();\n" +
            "  action static void S.M(int p) where (p > 0);\n" +
            "  action static void S.N(int q) where {. q >= 0 .};\n" +
            "}\n");
        Assert.NotEmpty(cord.Script.Configurations[0].DeclaredActions);
    }

    [Fact]
    public void LexerAndParser_ErrorPaths_Throw()
    {
        Assert.ThrowsAny<System.Exception>(() => Parse("config C { switch X = @bad; }\n"));   // unexpected char
        Assert.ThrowsAny<System.Exception>(() => Parse("config C { switch S = \"unterminated; }\n")); // unterminated string
        Assert.ThrowsAny<System.Exception>(() => Parse("config C { action static void S.M() where {. unterminated ; }\n")); // unterminated embedded
        Assert.ThrowsAny<System.Exception>(() => Parse("config C { switch X = ; }\n"));        // invalid switch value
    }
}
