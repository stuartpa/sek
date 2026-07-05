using Sek.Cord.Ast;
using Sek.Cord.Lexing;

namespace Sek.Cord.Parsing;

/// <summary>
/// Recursive-descent parser for Cord, following the MSDN grammar. Behavior operators
/// are parsed with the documented precedence (lowest to highest): parallel-family
/// (<c>||</c>, <c>-&gt;</c>), choice <c>|</c>, sequence <c>;</c>, then postfix repetition
/// (<c>* + ? {n} {n,} {n,m}</c>), then primaries (grouping, construct, let,
/// preconstraint, invocation, universal <c>_</c>).
/// </summary>
public sealed class Parser
{
    private readonly List<Token> _tokens;
    private int _i;

    public Parser(IEnumerable<Token> tokens) => _tokens = tokens.ToList();

    public static CordScript ParseText(string source) => new Parser(new Lexer(source).Tokenize()).ParseScript();

    private Token Cur => _tokens[_i];
    private Token Ahead(int n = 1) => _tokens[Math.Min(_i + n, _tokens.Count - 1)];
    private bool Is(TokenKind k) => Cur.Kind == k;
    private bool IsId(string text) => Cur.Kind == TokenKind.Identifier && Cur.Text == text;
    private Token Take() => _tokens[_i++];

    private Token Expect(TokenKind k)
    {
        if (!Is(k)) throw new CordSyntaxException($"Expected {k} but found {Cur.Kind} '{Cur.Text}'", Cur.Line, Cur.Column);
        return Take();
    }

    private bool Accept(TokenKind k)
    {
        if (Is(k)) { _i++; return true; }
        return false;
    }

    private bool AcceptId(string text)
    {
        if (IsId(text)) { _i++; return true; }
        return false;
    }

    public CordScript ParseScript()
    {
        var script = new CordScript();
        while (!Is(TokenKind.EndOfFile))
        {
            if (IsId("using")) { script.Usings.Add(ParseUsing()); }
            else if (IsId("config")) { script.Configurations.Add(ParseConfiguration()); }
            else if (IsId("machine")) { script.Machines.Add(ParseMachine()); }
            else throw new CordSyntaxException($"Expected using/config/machine but found '{Cur.Text}'", Cur.Line, Cur.Column);
        }

        return script;
    }

    private string ParseUsing()
    {
        Take();
        var name = ParseQualIdent();
        Expect(TokenKind.Semicolon);
        return name;
    }

    private Configuration ParseConfiguration()
    {
        Take();
        var cfg = new Configuration { Name = Expect(TokenKind.Identifier).Text };

        if (Accept(TokenKind.Colon))
        {
            cfg.BaseConfigs.Add(Expect(TokenKind.Identifier).Text);
            while (Accept(TokenKind.Comma)) cfg.BaseConfigs.Add(Expect(TokenKind.Identifier).Text);
        }

        Expect(TokenKind.LBrace);
        while (!Is(TokenKind.RBrace) && !Is(TokenKind.EndOfFile))
        {
            ParseConfigClause(cfg);
        }

        Expect(TokenKind.RBrace);
        return cfg;
    }

    private void ParseConfigClause(Configuration cfg)
    {
        if (IsId("action"))
        {
            Take();
            if (AcceptId("all"))
            {
                AcceptId("public");
                AcceptId("internal");
                cfg.ImportedActionTypes.Add(ParseTypeName());
            }
            else
            {
                AcceptId("exclude"); AcceptId("abstract"); AcceptId("event");

                var lastQual = new List<string>();
                while (!Is(TokenKind.LParen) && !Is(TokenKind.Semicolon) && !Is(TokenKind.EndOfFile) && !IsId("where"))
                {
                    if (Is(TokenKind.Identifier))
                    {
                        lastQual = new List<string> { Take().Text };
                        while (Accept(TokenKind.Dot)) lastQual.Add(Expect(TokenKind.Identifier).Text);
                    }
                    else
                    {
                        Take();
                    }
                }

                var declared = new DeclaredAction { Target = string.Join(".", lastQual) };

                if (Accept(TokenKind.LParen))
                {
                    while (!Is(TokenKind.RParen) && !Is(TokenKind.EndOfFile))
                    {
                        AcceptId("out"); AcceptId("ref");
                        var pType = ParseTypeName();
                        var pName = Is(TokenKind.Identifier) ? Take().Text : string.Empty;
                        declared.Parameters.Add(new Parameter { Type = pType, Name = pName });
                        if (!Accept(TokenKind.Comma)) break;
                    }

                    Expect(TokenKind.RParen);
                }

                if (AcceptId("where"))
                {
                    if (Is(TokenKind.EmbeddedStmt) || Is(TokenKind.EmbeddedExpr))
                    {
                        declared.WhereCode = Take().Text;
                    }
                    else if (Is(TokenKind.LParen))
                    {
                        SkipBalancedParens();
                    }
                    else
                    {
                        while (!Is(TokenKind.Semicolon) && !Is(TokenKind.EndOfFile)) Take();
                    }
                }

                cfg.DeclaredActions.Add(declared);
            }

            Expect(TokenKind.Semicolon);
        }
        else if (IsId("switch"))
        {
            Take();
            var name = Expect(TokenKind.Identifier).Text;
            Expect(TokenKind.Equals);
            var value = Cur.Kind switch
            {
                TokenKind.StringLiteral => Take().Text,
                TokenKind.IntLiteral => Take().Text,
                TokenKind.Identifier => Take().Text,
                _ => throw new CordSyntaxException($"Invalid switch value '{Cur.Text}'", Cur.Line, Cur.Column),
            };
            cfg.Switches[name] = value;
            Expect(TokenKind.Semicolon);
        }
        else
        {
            while (!Is(TokenKind.Semicolon) && !Is(TokenKind.EndOfFile)) Take();
            Expect(TokenKind.Semicolon);
        }
    }

    private Machine ParseMachine()
    {
        Take();
        var m = new Machine { Name = Expect(TokenKind.Identifier).Text };

        Expect(TokenKind.LParen);
        while (!Is(TokenKind.RParen) && !Is(TokenKind.EndOfFile))
        {
            AcceptId("out"); AcceptId("ref");
            var type = ParseTypeName();
            var name = Expect(TokenKind.Identifier).Text;
            m.Parameters.Add(new Parameter { Type = type, Name = name });
            if (!Accept(TokenKind.Comma)) break;
        }

        Expect(TokenKind.RParen);

        if (Accept(TokenKind.Slash))
        {
            ParseTypeName();
            if (Is(TokenKind.Identifier)) Take();
        }

        Expect(TokenKind.Colon);
        m.BaseConfigs.Add(Expect(TokenKind.Identifier).Text);
        while (Accept(TokenKind.Comma)) m.BaseConfigs.Add(Expect(TokenKind.Identifier).Text);

        if (AcceptId("where"))
        {
            ParseSwitchInto(m.Switches);
            while (Accept(TokenKind.Comma)) ParseSwitchInto(m.Switches);
        }

        Expect(TokenKind.LBrace);
        if (!Is(TokenKind.RBrace))
        {
            m.Body = ParseBehavior();
        }

        Expect(TokenKind.RBrace);
        return m;
    }

    private void ParseSwitchInto(Dictionary<string, string> switches)
    {
        var name = Expect(TokenKind.Identifier).Text;
        Expect(TokenKind.Equals);
        var value = Cur.Kind switch
        {
            TokenKind.StringLiteral => Take().Text,
            TokenKind.IntLiteral => Take().Text,
            TokenKind.Identifier => Take().Text,
            _ => throw new CordSyntaxException($"Invalid switch value '{Cur.Text}'", Cur.Line, Cur.Column),
        };
        switches[name] = value;
    }

    private Behavior ParseBehavior() => ParseParallelFamily();

    private Behavior ParseParallelFamily()
    {
        var left = ParseChoice();
        while (Is(TokenKind.BarBar) || Is(TokenKind.BarBarBar) || Is(TokenKind.SyncInterleave)
               || Is(TokenKind.Amp) || Is(TokenKind.Arrow))
        {
            var op = Take().Kind;
            var right = ParseChoice();
            left = op switch
            {
                TokenKind.BarBar => MkParallel("sync", left, right),
                TokenKind.BarBarBar => MkParallel("interleave", left, right),
                TokenKind.SyncInterleave => MkParallel("syncinterleave", left, right),
                TokenKind.Amp => MkPerm(left, right),
                TokenKind.Arrow => MkLoose(left, right),
                _ => left,
            };
        }

        return left;
    }

    private static Behavior MkParallel(string op, Behavior l, Behavior r)
    {
        var n = new ParallelBehavior { Op = op };
        n.Items.Add(l);
        n.Items.Add(r);
        return n;
    }

    private static Behavior MkPerm(Behavior l, Behavior r)
    {
        var n = new PermutationBehavior();
        n.Items.Add(l);
        n.Items.Add(r);
        return n;
    }

    private static Behavior MkLoose(Behavior l, Behavior r)
    {
        var n = new LooseSequenceBehavior();
        n.Items.Add(l);
        n.Items.Add(r);
        return n;
    }

    private Behavior ParseChoice()
    {
        var left = ParseSequence();
        if (!Is(TokenKind.Bar)) return left;
        var node = new ChoiceBehavior();
        node.Items.Add(left);
        while (Accept(TokenKind.Bar)) node.Items.Add(ParseSequence());
        return node;
    }

    private Behavior ParseSequence()
    {
        var left = ParsePostfix();
        if (!Is(TokenKind.Semicolon)) return left;
        var node = new SequenceBehavior();
        node.Items.Add(left);
        while (Accept(TokenKind.Semicolon))
        {
            if (Is(TokenKind.RBrace) || Is(TokenKind.RParen) || Is(TokenKind.EndOfFile)) break;
            node.Items.Add(ParsePostfix());
        }

        return node;
    }

    private Behavior ParsePostfix()
    {
        var e = ParsePrimary();
        while (true)
        {
            if (Accept(TokenKind.Star)) { e = new RepetitionBehavior { Inner = e, Op = "*" }; }
            else if (Accept(TokenKind.Plus)) { e = new RepetitionBehavior { Inner = e, Op = "+" }; }
            else if (Accept(TokenKind.Question)) { e = new RepetitionBehavior { Inner = e, Op = "?" }; }
            else if (Is(TokenKind.LBrace)) { e = ParseRepetitionCount(e); }
            else break;
        }

        return e;
    }

    private Behavior ParseRepetitionCount(Behavior inner)
    {
        Expect(TokenKind.LBrace);
        var min = int.Parse(Expect(TokenKind.IntLiteral).Text);
        int? max = min;
        if (Accept(TokenKind.Comma))
        {
            max = Is(TokenKind.IntLiteral) ? int.Parse(Take().Text) : (int?)null;
        }
        else if (Accept(TokenKind.DotDot))
        {
            max = int.Parse(Expect(TokenKind.IntLiteral).Text);
        }

        Expect(TokenKind.RBrace);
        return new RepetitionBehavior { Inner = inner, Op = "{}", Min = min, Max = max };
    }

    private Behavior ParsePrimary()
    {
        if (Accept(TokenKind.LParen))
        {
            var inner = ParseBehavior();
            Expect(TokenKind.RParen);
            return new GroupBehavior { Inner = inner };
        }

        if (Is(TokenKind.EmbeddedStmt))
        {
            var code = Take().Text;
            Expect(TokenKind.Colon);
            return new PreconstraintBehavior { Code = code, Inner = ParsePostfix() };
        }

        if (IsId("construct")) return ParseConstruct();
        if (IsId("let")) return ParseLet();
        if (IsId("bind")) return ParseBind();

        if (Is(TokenKind.Ellipsis))
        {
            Take();
            // "..." is any-sequence == _*
            return new RepetitionBehavior { Inner = new InvocationBehavior { Target = "_" }, Op = "*" };
        }

        return ParseInvocation();
    }

    private Behavior ParseConstruct()
    {
        Take(); // 'construct'

        if (AcceptId("model"))
        {
            RequireId("program");
            RequireId("from");
            var cb = new ConstructBehavior { Kind = ConstructKind.ModelProgram, Reference = ParseQualIdent() };
            if (AcceptId("where")) ParseWhereOpts(cb);
            return cb;
        }

        if (AcceptId("accepting"))
        {
            RequireId("paths");
            var cb = new ConstructBehavior { Kind = ConstructKind.AcceptingPaths };
            if (AcceptId("where")) ParseWhereOpts(cb);
            RequireId("for");
            return WithTarget(cb);
        }

        if (AcceptId("test"))
        {
            RequireId("cases");
            var cb = new ConstructBehavior { Kind = ConstructKind.TestCases };
            if (AcceptId("where")) ParseWhereOpts(cb);
            RequireId("for");
            return WithTarget(cb);
        }

        if (AcceptId("bounded"))
        {
            RequireId("exploration");
            var cb = new ConstructBehavior { Kind = ConstructKind.BoundedExploration };
            if (AcceptId("where")) ParseWhereOpts(cb);
            RequireId("for");
            return WithTarget(cb);
        }

        if (AcceptId("point"))
        {
            RequireId("shoot");
            var cb = new ConstructBehavior { Kind = ConstructKind.PointShoot };
            if (AcceptId("where")) ParseWhereOpts(cb);
            RequireId("for");
            return WithTarget(cb);
        }

        if (AcceptId("accept"))
        {
            RequireId("completion");
            var cb = new ConstructBehavior { Kind = ConstructKind.AcceptCompletion };
            if (AcceptId("where")) ParseWhereOpts(cb);
            RequireId("for");
            return WithTarget(cb);
        }

        if (AcceptId("requirement"))
        {
            RequireId("coverage");
            var cb = new ConstructBehavior { Kind = ConstructKind.RequirementCoverage };
            if (AcceptId("where")) ParseWhereOpts(cb);
            RequireId("for");
            return WithTarget(cb);
        }

        throw new CordSyntaxException($"Unknown construct form '{Cur.Text}'", Cur.Line, Cur.Column);
    }

    /// <summary>Parses the <c>for</c> target: a named machine, a parenthesised behavior, or a
    /// nested <c>construct</c>.</summary>
    private ConstructBehavior WithTarget(ConstructBehavior cb)
    {
        if (IsId("construct"))
        {
            cb.Target = ParseConstruct();
        }
        else if (Accept(TokenKind.LParen))
        {
            cb.Target = ParseBehavior();
            Expect(TokenKind.RParen);
        }
        else if (Is(TokenKind.Identifier))
        {
            cb.Reference = ParseQualIdent();
        }

        return cb;
    }

    /// <summary>Parses <c>where key = value {, key = value}</c> plus an optional
    /// <c>with (. expr .)</c>, stopping before <c>for</c>.</summary>
    private void ParseWhereOpts(ConstructBehavior cb)
    {
        do
        {
            if (!Is(TokenKind.Identifier)) break;
            var key = Take().Text;
            if (!Accept(TokenKind.Equals)) { cb.Params[key] = "true"; }
            else { cb.Params[key] = ParseOptValue(); }
        }
        while (Accept(TokenKind.Comma));

        if (AcceptId("with"))
        {
            if (Is(TokenKind.EmbeddedExpr) || Is(TokenKind.EmbeddedStmt)) cb.Params["with"] = Take().Text;
            else if (Is(TokenKind.LParen)) { SkipBalancedParens(); cb.Params["with"] = "(inline)"; }
        }
    }

    private string ParseOptValue()
    {
        return Cur.Kind switch
        {
            TokenKind.StringLiteral => Take().Text,
            TokenKind.IntLiteral => Take().Text,
            TokenKind.Identifier => ParseQualIdent(),
            _ => Take().Text,
        };
    }

    private Behavior ParseBind()
    {
        Take(); // 'bind'
        var bind = new BindBehavior();
        do
        {
            var clause = new BindClause { Action = ParseQualIdent() };
            if (Accept(TokenKind.LParen))
            {
                while (!Is(TokenKind.RParen) && !Is(TokenKind.EndOfFile))
                {
                    AcceptId("out"); AcceptId("ref");
                    clause.ArgDomains.Add(ParseArgDomain());
                    if (!Accept(TokenKind.Comma)) break;
                }

                Expect(TokenKind.RParen);
            }

            bind.Binds.Add(clause);
        }
        while (Accept(TokenKind.Comma));

        RequireId("in");
        bind.Inner = ParseBehavior();
        return bind;
    }

    /// <summary>Parses one bound-parameter domain: <c>_</c> (unbound), a set <c>{a, b}</c>,
    /// a single literal/qualident, or a structured value (captured as unbound).</summary>
    private List<string> ParseArgDomain()
    {
        var vals = new List<string>();
        if (Is(TokenKind.Underscore)) { Take(); vals.Add("_"); return vals; }

        if (Accept(TokenKind.LBrace))
        {
            while (!Is(TokenKind.RBrace) && !Is(TokenKind.EndOfFile))
            {
                vals.Add(ParseValueToken());
                if (!Accept(TokenKind.Comma)) break;
            }

            Expect(TokenKind.RBrace);
            return vals;
        }

        var v = ParseValueToken();
        if (Is(TokenKind.LParen))
        {
            // structured value, e.g. JobInfo(Command={...}, Time={...}) — captured as unbound.
            SkipBalancedParens();
            return new List<string> { "_" };
        }

        vals.Add(v);
        return vals;
    }

    private string ParseValueToken()
    {
        if (Is(TokenKind.StringLiteral) || Is(TokenKind.IntLiteral)) return Take().Text;
        if (Is(TokenKind.Identifier)) return ParseQualIdent();
        return Take().Text;
    }

    private Behavior ParseLet()
    {
        Take(); // 'let'
        var let = new LetBehavior();

        // Local variable declarations: Type name {, Type name}
        do
        {
            var type = ParseTypeName();
            var name = Expect(TokenKind.Identifier).Text;
            let.Vars.Add(new Parameter { Type = type, Name = name });
        }
        while (Accept(TokenKind.Comma));

        if (AcceptId("where"))
        {
            if (Is(TokenKind.EmbeddedStmt) || Is(TokenKind.EmbeddedExpr))
            {
                let.WhereCode = Take().Text;
            }
            else if (Is(TokenKind.LParen))
            {
                SkipBalancedParens();
            }
        }

        RequireId("in");
        let.Inner = ParseBehavior();
        return let;
    }

    private Behavior ParseInvocation()
    {
        var inv = new InvocationBehavior();
        if (Accept(TokenKind.Bang)) inv.Negated = true;

        if ((IsId("call") || IsId("return") || IsId("event")) && Ahead().Kind == TokenKind.Identifier)
        {
            inv.Qualifier = Take().Text;
        }

        if (Is(TokenKind.Underscore))
        {
            Take();
            inv.Target = "_";
            return inv;
        }

        inv.Target = ParseQualIdent();

        if (Accept(TokenKind.LParen))
        {
            inv.Args = new List<string>();
            if (!Is(TokenKind.RParen))
            {
                inv.Args.Add(ParseArg());
                while (Accept(TokenKind.Comma)) inv.Args.Add(ParseArg());
            }

            Expect(TokenKind.RParen);
        }

        if (Accept(TokenKind.Slash))
        {
            ParseArg();
        }

        return inv;
    }

    private string ParseArg()
    {
        var parts = new List<string>();
        var depth = 0;
        while (!Is(TokenKind.EndOfFile))
        {
            if (depth == 0 && (Is(TokenKind.Comma) || Is(TokenKind.RParen))) break;
            if (Is(TokenKind.LParen)) depth++;
            if (Is(TokenKind.RParen)) depth--;
            parts.Add(Take().Text);
        }

        return string.Join(string.Empty, parts);
    }

    private void RequireId(string text)
    {
        if (!AcceptId(text)) throw new CordSyntaxException($"Expected '{text}' but found '{Cur.Text}'", Cur.Line, Cur.Column);
    }

    private string ParseQualIdent()
    {
        var parts = new List<string> { Expect(TokenKind.Identifier).Text };
        while (Accept(TokenKind.Dot)) parts.Add(Expect(TokenKind.Identifier).Text);
        return string.Join(".", parts);
    }

    private string ParseTypeName()
    {
        var name = ParseQualIdent();
        if (Accept(TokenKind.Lt))
        {
            var depth = 1;
            while (depth > 0 && !Is(TokenKind.EndOfFile))
            {
                if (Is(TokenKind.Lt)) depth++;
                if (Is(TokenKind.Gt)) depth--;
                Take();
            }
        }

        while (Is(TokenKind.LBracket))
        {
            Take();
            while (!Is(TokenKind.RBracket) && !Is(TokenKind.EndOfFile)) Take();
            Expect(TokenKind.RBracket);
        }

        return name;
    }

    private void SkipBalancedParens()
    {
        Expect(TokenKind.LParen);
        var depth = 1;
        while (depth > 0 && !Is(TokenKind.EndOfFile))
        {
            if (Is(TokenKind.LParen)) depth++;
            else if (Is(TokenKind.RParen)) depth--;
            Take();
        }
    }
}
