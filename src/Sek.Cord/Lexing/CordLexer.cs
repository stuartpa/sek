using System.Text;

namespace Sek.Cord.Lexing;

/// <summary>
/// Hand-written lexer for Cord. Produces a token stream, skipping whitespace and
/// comments (<c>// ...</c> and <c>/* ... */</c>) and capturing embedded C# blocks
/// <c>(. ... .)</c> and <c>{. ... .}</c> as single tokens. Handles the full Cord
/// operator set including <c>||</c>, <c>|||</c>, <c>|?|</c>, <c>&amp;</c>, <c>-&gt;</c>,
/// <c>...</c>.
/// </summary>
public sealed class Lexer
{
    private readonly string _src;
    private int _pos;
    private int _line = 1;
    private int _col = 1;

    public Lexer(string source) => _src = source;

    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();
        Token t;
        do
        {
            t = Next();
            tokens.Add(t);
        }
        while (t.Kind != TokenKind.EndOfFile);
        return tokens;
    }

    private char Cur => _pos < _src.Length ? _src[_pos] : '\0';
    private char Peek(int n = 1) => _pos + n < _src.Length ? _src[_pos + n] : '\0';

    private void Advance()
    {
        if (Cur == '\n') { _line++; _col = 1; }
        else { _col++; }
        _pos++;
    }

    private Token Next()
    {
        SkipTrivia();

        var line = _line;
        var col = _col;

        if (_pos >= _src.Length)
        {
            return new Token(TokenKind.EndOfFile, string.Empty, line, col);
        }

        var c = Cur;

        if (c == '(' && Peek() == '.')
        {
            return ReadEmbedded("(.", ".)", TokenKind.EmbeddedExpr, line, col);
        }

        if (c == '{' && Peek() == '.')
        {
            return ReadEmbedded("{.", ".}", TokenKind.EmbeddedStmt, line, col);
        }

        if (char.IsLetter(c) || c == '_')
        {
            var sb = new StringBuilder();
            while (char.IsLetterOrDigit(Cur) || Cur == '_')
            {
                sb.Append(Cur);
                Advance();
            }

            var text = sb.ToString();
            return text == "_"
                ? new Token(TokenKind.Underscore, text, line, col)
                : new Token(TokenKind.Identifier, text, line, col);
        }

        if (char.IsDigit(c) || (c == '-' && char.IsDigit(Peek()) && Peek() != '>'))
        {
            var sb = new StringBuilder();
            if (Cur == '-') { sb.Append(Cur); Advance(); }
            while (char.IsDigit(Cur)) { sb.Append(Cur); Advance(); }
            return new Token(TokenKind.IntLiteral, sb.ToString(), line, col);
        }

        if (c == '"')
        {
            return ReadString(line, col);
        }

        // Multi-char operators (longest first).
        switch (c)
        {
            case '|' when Peek() == '|' && Peek(2) == '|': Advance(); Advance(); Advance(); return new Token(TokenKind.BarBarBar, "|||", line, col);
            case '|' when Peek() == '?' && Peek(2) == '|': Advance(); Advance(); Advance(); return new Token(TokenKind.SyncInterleave, "|?|", line, col);
            case '|' when Peek() == '|': Advance(); Advance(); return new Token(TokenKind.BarBar, "||", line, col);
            case '.' when Peek() == '.' && Peek(2) == '.': Advance(); Advance(); Advance(); return new Token(TokenKind.Ellipsis, "...", line, col);
            case '.' when Peek() == '.': Advance(); Advance(); return new Token(TokenKind.DotDot, "..", line, col);
            case '-' when Peek() == '>': Advance(); Advance(); return new Token(TokenKind.Arrow, "->", line, col);
        }

        Advance();
        return c switch
        {
            '{' => new Token(TokenKind.LBrace, "{", line, col),
            '}' => new Token(TokenKind.RBrace, "}", line, col),
            '(' => new Token(TokenKind.LParen, "(", line, col),
            ')' => new Token(TokenKind.RParen, ")", line, col),
            '[' => new Token(TokenKind.LBracket, "[", line, col),
            ']' => new Token(TokenKind.RBracket, "]", line, col),
            ';' => new Token(TokenKind.Semicolon, ";", line, col),
            ',' => new Token(TokenKind.Comma, ",", line, col),
            ':' => new Token(TokenKind.Colon, ":", line, col),
            '.' => new Token(TokenKind.Dot, ".", line, col),
            '=' => new Token(TokenKind.Equals, "=", line, col),
            '|' => new Token(TokenKind.Bar, "|", line, col),
            '&' => new Token(TokenKind.Amp, "&", line, col),
            '*' => new Token(TokenKind.Star, "*", line, col),
            '+' => new Token(TokenKind.Plus, "+", line, col),
            '?' => new Token(TokenKind.Question, "?", line, col),
            '!' => new Token(TokenKind.Bang, "!", line, col),
            '/' => new Token(TokenKind.Slash, "/", line, col),
            '<' => new Token(TokenKind.Lt, "<", line, col),
            '>' => new Token(TokenKind.Gt, ">", line, col),
            _ => throw new CordSyntaxException($"Unexpected character '{c}'", line, col),
        };
    }

    private Token ReadEmbedded(string open, string close, TokenKind kind, int line, int col)
    {
        Advance(); Advance();
        var sb = new StringBuilder();
        while (_pos < _src.Length && !(Cur == close[0] && Peek() == close[1]))
        {
            sb.Append(Cur);
            Advance();
        }

        if (_pos >= _src.Length)
        {
            throw new CordSyntaxException($"Unterminated embedded block; expected '{close}'", line, col);
        }

        Advance(); Advance();
        return new Token(kind, sb.ToString().Trim(), line, col);
    }

    private Token ReadString(int line, int col)
    {
        Advance();
        var sb = new StringBuilder();
        while (_pos < _src.Length && Cur != '"')
        {
            if (Cur == '\\')
            {
                Advance();
                sb.Append(Cur switch
                {
                    'n' => '\n',
                    't' => '\t',
                    'r' => '\r',
                    '"' => '"',
                    '\\' => '\\',
                    _ => Cur,
                });
                Advance();
            }
            else
            {
                sb.Append(Cur);
                Advance();
            }
        }

        if (_pos >= _src.Length)
        {
            throw new CordSyntaxException("Unterminated string literal", line, col);
        }

        Advance();
        return new Token(TokenKind.StringLiteral, sb.ToString(), line, col);
    }

    private void SkipTrivia()
    {
        while (_pos < _src.Length)
        {
            var c = Cur;
            if (char.IsWhiteSpace(c))
            {
                Advance();
            }
            else if (c == '/' && Peek() == '/')
            {
                while (_pos < _src.Length && Cur != '\n')
                {
                    Advance();
                }
            }
            else if (c == '/' && Peek() == '*')
            {
                Advance(); Advance();
                while (_pos < _src.Length && !(Cur == '*' && Peek() == '/'))
                {
                    Advance();
                }

                if (_pos < _src.Length) { Advance(); Advance(); }
            }
            else
            {
                break;
            }
        }
    }
}

public sealed class CordSyntaxException : Exception
{
    public int Line { get; }
    public int Column { get; }

    public CordSyntaxException(string message, int line, int column)
        : base($"{message} (line {line}, col {column})")
    {
        Line = line;
        Column = column;
    }
}
