namespace Sek.Cord.Lexing;

public enum TokenKind
{
    // literals / names
    Identifier,
    IntLiteral,
    StringLiteral,

    // punctuation & operators
    LBrace,        // {
    RBrace,        // }
    LParen,        // (
    RParen,        // )
    LBracket,      // [
    RBracket,      // ]
    Semicolon,     // ;
    Comma,         // ,
    Colon,         // :
    Dot,           // .
    DotDot,        // ..
    Equals,        // =
    Bar,           // |
    BarBar,        // ||
    BarBarBar,     // ||| (interleaved parallel)
    SyncInterleave,// |?| (sync-interleaved parallel)
    Amp,           // & (permutation)
    Ellipsis,      // ... (any sequence)
    Star,          // *
    Plus,          // +
    Question,      // ?
    Bang,          // !
    Slash,         // /
    Lt,            // <
    Gt,            // >
    Arrow,         // ->
    Underscore,    // _

    // embedded C#
    EmbeddedExpr,  // (. ... .)
    EmbeddedStmt,  // {. ... .}

    EndOfFile,
}

public sealed record Token(TokenKind Kind, string Text, int Line, int Column)
{
    public override string ToString() => $"{Kind}('{Text}') @ {Line}:{Column}";
}
