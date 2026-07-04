using System.Text;
using Sek.Solver;

namespace Sek.Cord;

/// <summary>
/// Minimal recursive-descent parser for the boolean/arithmetic expressions used inside
/// <c>Condition.IsTrue(...)</c>. Produces a <see cref="Sek.Solver.Expr"/> tree and the
/// set of referenced identifiers. Best-effort: returns ok=false if it cannot parse.
/// </summary>
internal static class ExprParser
{
    public static (Expr? expr, HashSet<string> refs, bool ok) TryParse(string text)
    {
        var refs = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            var tokens = Lex(text);
            var p = new P(tokens, refs);
            var e = p.ParseOr();
            return p.AtEnd ? (e, refs, true) : (null, refs, false);
        }
        catch
        {
            return (null, refs, false);
        }
    }

    private enum K { Id, Int, Str, Op, LParen, RParen, End }

    private readonly record struct Tok(K Kind, string Text);

    private static List<Tok> Lex(string s)
    {
        var t = new List<Tok>();
        var i = 0;
        while (i < s.Length)
        {
            var c = s[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (char.IsLetter(c) || c == '_')
            {
                var sb = new StringBuilder();
                while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_' || s[i] == '.')) sb.Append(s[i++]);
                t.Add(new Tok(K.Id, sb.ToString()));
            }
            else if (char.IsDigit(c) || (c == '-' && i + 1 < s.Length && char.IsDigit(s[i + 1]) && (t.Count == 0 || t[^1].Kind == K.Op || t[^1].Kind == K.LParen)))
            {
                var sb = new StringBuilder();
                if (c == '-') sb.Append(s[i++]);
                while (i < s.Length && (char.IsDigit(s[i]) || s[i] == 'x' || (s[i] >= 'a' && s[i] <= 'f') || (s[i] >= 'A' && s[i] <= 'F'))) sb.Append(s[i++]);
                t.Add(new Tok(K.Int, sb.ToString()));
            }
            else if (c == '"')
            {
                var sb = new StringBuilder();
                i++;
                while (i < s.Length && s[i] != '"') sb.Append(s[i++]);
                i++;
                t.Add(new Tok(K.Str, sb.ToString()));
            }
            else if (c == '(') { t.Add(new Tok(K.LParen, "(")); i++; }
            else if (c == ')') { t.Add(new Tok(K.RParen, ")")); i++; }
            else
            {
                // operators (longest first)
                string[] ops = { "==", "!=", "<=", ">=", "&&", "||", "<", ">", "!", "&", "|", "+", "-", "*", "/", "%" };
                var matched = ops.FirstOrDefault(o => s.AsSpan(i).StartsWith(o));
                if (matched is null) throw new FormatException($"bad char '{c}'");
                t.Add(new Tok(K.Op, matched));
                i += matched.Length;
            }
        }

        t.Add(new Tok(K.End, string.Empty));
        return t;
    }

    private sealed class P
    {
        private readonly List<Tok> _t;
        private readonly HashSet<string> _refs;
        private int _i;

        public P(List<Tok> t, HashSet<string> refs) { _t = t; _refs = refs; }

        public bool AtEnd => _t[_i].Kind == K.End;
        private Tok Cur => _t[_i];
        private bool Op(string o) => Cur.Kind == K.Op && Cur.Text == o;
        private Tok Take() => _t[_i++];

        public Expr ParseOr() => Bin(ParseAnd, "||");
        private Expr ParseAnd() => Bin(ParseBitOr, "&&");
        private Expr ParseBitOr() => Bin(ParseBitAnd, "|");
        private Expr ParseBitAnd() => Bin(ParseEq, "&");
        private Expr ParseEq() => Bin(ParseRel, "==", "!=");
        private Expr ParseRel() => Bin(ParseAdd, "<", "<=", ">", ">=");
        private Expr ParseAdd() => Bin(ParseMul, "+", "-");
        private Expr ParseMul() => Bin(ParseUnary, "*", "/", "%");

        private Expr Bin(Func<Expr> next, params string[] ops)
        {
            var left = next();
            while (Cur.Kind == K.Op && ops.Contains(Cur.Text))
            {
                var op = Take().Text;
                var right = next();
                left = new BinExpr { Op = op, Left = left, Right = right };
            }

            return left;
        }

        private Expr ParseUnary()
        {
            if (Op("!") || Op("-"))
            {
                var op = Take().Text;
                return new UnExpr { Op = op, Operand = ParseUnary() };
            }

            return ParsePrimary();
        }

        private Expr ParsePrimary()
        {
            var tok = Cur;
            switch (tok.Kind)
            {
                case K.LParen:
                    Take();
                    var e = ParseOr();
                    if (Cur.Kind != K.RParen) throw new FormatException("expected )");
                    Take();
                    return e;
                case K.Int:
                    Take();
                    return new LitExpr { Value = ParseIntLiteral(tok.Text), Kind = ValueKind.Int };
                case K.Str:
                    Take();
                    return new LitExpr { Value = tok.Text, Kind = ValueKind.String };
                case K.Id:
                    Take();
                    if (tok.Text is "true" or "false")
                    {
                        return new LitExpr { Value = tok.Text == "true", Kind = ValueKind.Bool };
                    }

                    _refs.Add(tok.Text);
                    return new VarExpr { Name = tok.Text };
                default:
                    throw new FormatException($"unexpected token '{tok.Text}'");
            }
        }

        private static long ParseIntLiteral(string s)
        {
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || s.StartsWith("-0x", StringComparison.OrdinalIgnoreCase))
            {
                var neg = s[0] == '-';
                var hex = s[(neg ? 3 : 2)..];
                var val = Convert.ToInt64(hex, 16);
                return neg ? -val : val;
            }

            return long.Parse(s);
        }
    }
}
