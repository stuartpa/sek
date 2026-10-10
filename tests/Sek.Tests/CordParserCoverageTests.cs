using Sek.Cord;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Parser/lexer coverage for <c>Sek.Cord</c>: parses a broad spread of grammar productions
/// (action kinds, switches, inheritance, the full behavior algebra, every construct kind, bind/let,
/// generic/array parameter types) and the lexer edge cases, asserting they parse without error;
/// plus the syntax-error path.
/// </summary>
public class CordParserCoverageTests
{
    [Theory]
    [InlineData("\"fast\"", "fast")]
    [InlineData("42", "42")]
    [InlineData("CollapseLabelView", "CollapseLabelView")]
    public void SwitchValues_AreRetainedInConfigAndMachine(string token, string expected)
    {
        var script = Sek.Cord.Parsing.Parser.ParseText(
            $"config C {{ switch Mode = {token}; }} machine M() : C where Mode = {token} {{ }}");
        Assert.Equal(expected, Assert.Single(script.Configurations).Switches["Mode"]);
        Assert.Equal(expected, Assert.Single(script.Machines).Switches["Mode"]);
    }

    [Theory]
    [InlineData("config C { switch Mode = ; }")]
    [InlineData("config C { switch Mode = +; }")]
    [InlineData("config C { switch Mode = ")]
    [InlineData("config C {} machine M() : C where Mode = + { }")]
    [InlineData("config C {} machine M() : C where Mode = ")]
    [InlineData("config C { action void Service.Call(int")]
    [InlineData("config C { action void Service.Call(int value")]
    [InlineData("config C { action void Service.Call() where value")]
    [InlineData("config C { switch Mode = 42;")]
    [InlineData("config C {} machine M(int value")]
    [InlineData("config C {} machine M(System.Collections.Generic.List<int")]
    [InlineData("config C {} machine M(int[")]
    [InlineData("config C { action void Service.Call(int value) where (value")]
    [InlineData("config C {} machine M() : C { Call((1)")]
    [InlineData("config C {} machine M() : C { bind Call({1")]
    [InlineData("config C {} machine M() : C { bind Call(Value(Field={1})")]
    [InlineData("config C {} machine M() : C { let int value where (value")]
    [InlineData("config C {} machine M() : C { bind Call(")]
    [InlineData("config C {} machine M() : C { bind Call({")]
    [InlineData("config C {} machine M() : C { bind Call(Value(")]
    [InlineData("config C {} machine M() : C { Call(1,")]
    [InlineData("config C { unknown value")]
    public void TruncatedDeclarationsAndInvalidSwitchValues_RejectSyntax(string source)
    {
        var exception = Assert.ThrowsAny<System.Exception>(() => Sek.Cord.Parsing.Parser.ParseText(source));
        Assert.Equal("CordSyntaxException", exception.GetType().Name);
    }

    [Fact]
    public void DeclaredActionWithoutParameterName_PreservesEmptyName()
    {
        var script = Sek.Cord.Parsing.Parser.ParseText("config C { action void Service.Call(int); }");
        var action = Assert.Single(Assert.Single(script.Configurations).DeclaredActions);
        var parameter = Assert.Single(action.Parameters);
        Assert.Equal("int", parameter.Type);
        Assert.Equal(string.Empty, parameter.Name);
    }

    [Theory]
    // action kinds + modifiers
    [InlineData("config C { action event void Sub.Received(string d); }")]
    [InlineData("config C { action return int Svc.Compute(); }")]
    [InlineData("config C { action call void Svc.Do(); }")]
    [InlineData("config C { action abstract static void I.A(); }")]
    [InlineData("config C { action all Adapter; }")]
    // config switches: int / string / enum-ish value
    [InlineData("config C { switch StateBound = 42; switch Mode = \"Fast\"; switch RecommendedViews = CollapseLabelView; }")]
    // inheritance (single + multi)
    [InlineData("config Base { } config Derived : Base { } config Multi : Base, Derived { }")]
    [InlineData("config A { } config B { } machine M() : A, B { construct model program from A }")]
    // behavior operators
    [InlineData("config C { action all S; } machine M() : C { a; b }")]
    [InlineData("config C { action all S; } machine M() : C { a | b }")]
    [InlineData("config C { action all S; } machine P() : C { a } machine Q() : C { b } machine M() : C { P || Q }")]
    [InlineData("config C { action all S; } machine P() : C { a } machine Q() : C { b } machine M() : C { P ||| Q }")]
    [InlineData("config C { action all S; } machine P() : C { a } machine Q() : C { b } machine M() : C { P |?| Q }")]
    [InlineData("config C { action all S; } machine M() : C { a & b }")]
    [InlineData("config C { action all S; } machine M() : C { a -> b }")]
    [InlineData("config C { action all S; } machine M() : C { !a }")]
    [InlineData("config C { action all S; } machine M() : C { a* }")]
    [InlineData("config C { action all S; } machine M() : C { a+ }")]
    [InlineData("config C { action all S; } machine M() : C { a? }")]
    [InlineData("config C { action all S; } machine M() : C { a{2} }")]
    [InlineData("config C { action all S; } machine M() : C { a{2,} }")]
    [InlineData("config C { action all S; } machine M() : C { a{2,4} }")]
    [InlineData("config C { action all S; } machine M() : C { (a; b); c }")]
    // return binding
    [InlineData("config C { action all S; } machine M() : C { Open() / h; Close(h) }")]
    // generic + array parameter types
    [InlineData("config C { action all S; } machine M(System.Collections.Generic.List<int> xs) : C { a }")]
    [InlineData("config C { action all S; } machine M(int[] arr) : C { a }")]
    // constructs (verified syntax)
    [InlineData("config Actions { action all S; } machine ModelProgram() : Actions { construct model program from Actions where scope = \"Ns\" }")]
    [InlineData("config Actions { action all S; } machine ModelProgram() : Actions { construct model program from Actions } machine T() : Actions { construct test cases where Strategy=\"longtests\" for ModelProgram }")]
    [InlineData("config C { action all S; } machine M() : C { construct accepting paths for (a; b) }")]
    [InlineData("config C { action all S; } machine Shoot() : C { a } machine M() : C { construct bounded exploration where PathDepth = 2 for Shoot }")]
    [InlineData("config C { action all S; } machine Shoot() : C { a } machine M() : C { construct accept completion where Completer = \"X\" for Shoot }")]
    [InlineData("config C { action all S; } machine Point() : C { a } machine M() : C { construct point shoot where Shoot = \"Shoot\" with (. Ns.T.M .) for Point }")]
    // bind (single + multi + arg domains)
    [InlineData("config C { action all S; } machine Mp() : C { construct model program from C } machine M() : C { ( bind Open({1}) in Mp ) }")]
    [InlineData("config C { action all S; } machine Mp() : C { construct model program from C } machine M() : C { ( bind Open({1,2}), Write(_, \"hi\") in Mp ) }")]
    // let
    [InlineData("config C { action all S; } machine M() : C { let int x, int y where {. Condition.In(x, 1, 2); Condition.In(y, 3); .} in a; b }")]
    // using clauses + comments
    [InlineData("using Some.Namespace;\n// a line comment\nconfig C { action all S; } machine M() : C { a }")]
    // declared-action parameter lists (out/ref/comma) + the three where-clause forms
    [InlineData("config C { action void Svc.Do(out int x, ref string y, int z); }")]
    [InlineData("config C { action void Svc.Do(int x) where (x in {1,2}); }")]
    [InlineData("config C { action void Svc.Do(int x) where {. Condition.In(x, 1, 2); .}; }")]
    [InlineData("config C { action void Svc.Do() where SomeBareToken; }")]
    // argument-domain forms in bind: integer range, non-integer range, unions with the
    // unbound marker (both sides), a structured value, and an `instances` domain
    [InlineData("config C { action all S; } machine Mp() : C { construct model program from C } machine M() : C { ( bind Open(1..3) in Mp ) }")]
    [InlineData("config C { action all S; } machine Mp() : C { construct model program from C } machine M() : C { ( bind Open(lo..hi) in Mp ) }")]
    [InlineData("config C { action all S; } machine Mp() : C { construct model program from C } machine M() : C { ( bind Open(_ + {1,2}) in Mp ) }")]
    [InlineData("config C { action all S; } machine Mp() : C { construct model program from C } machine M() : C { ( bind Open({1} + _) in Mp ) }")]
    [InlineData("config C { action all S; } machine Mp() : C { construct model program from C } machine M() : C { ( bind Make(Foo(F={1,2}, G=3)) in Mp ) }")]
    [InlineData("config C { action all S; } machine Mp() : C { construct model program from C } machine M() : C { ( bind Use(instances Foo) in Mp ) }")]
    [InlineData("config C {} machine M() : C { bind Call in Step }")]
    [InlineData("config C {} machine M() : C { bind Call(out {1}, ref {2}) in Step }")]
    [InlineData("config C {} machine M() : C { bind Call(_ + _, {1} + {2}, Value()) in Step }")]
    [InlineData("config C {} machine M() : C { bind Call(Value(Field=_)) in Step }")]
    [InlineData("config C {} machine M() : C { bind Call(hi..3, 3..lo, 4..2, 1..5000) in Step }")]
    [InlineData("config C {} machine M() : C { bind Call({+}) in Step }")]
    [InlineData("config C {} machine M() : C { let int value in Step }")]
    [InlineData("config C {} machine M() : C { let int value where (Check((value))) in Step }")]
    [InlineData("config C {} machine M() : C { let int value where (. Check(value) .) in Step }")]
    [InlineData("config C {} machine M() : C { (Open() / _) }")]
    [InlineData("config C {} machine M() : C { construct accepting paths where Flag, Mode = Fast with (Check((1))) for Step }")]
    [InlineData("config C {} machine M() : C { construct requirement coverage for Step }")]
    [InlineData("config C {} machine M() : C { construct requirement coverage where Flag=true for Step }")]
    [InlineData("config C {} machine M() : C { construct test cases for Step }")]
    [InlineData("config C {} machine M() : C { construct bounded exploration for Step }")]
    [InlineData("config C {} machine M() : C { construct point shoot for Step }")]
    [InlineData("config C {} machine M() : C { construct accept completion for Step }")]
    public void Parses_Without_Error(string source)
    {
        var ex = Record.Exception(() => CordDocument.ParseText(source));
        Assert.True(ex is null, $"parse failed: {ex?.Message}\nsource: {source}");
    }

    [Theory]
    [InlineData("foo bar baz;")]                       // invalid top-level keyword
    [InlineData("machine M() : C { a | | }")]          // dangling operator
    public void InvalidSyntax_Throws(string source)
    {
        Assert.ThrowsAny<System.Exception>(() => CordDocument.ParseText(source));
    }
}
