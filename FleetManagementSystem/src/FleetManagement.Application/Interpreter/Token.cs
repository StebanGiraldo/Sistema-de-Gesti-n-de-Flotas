namespace FleetManagement.Application.Interpreter;

public enum TokenKind
{
    Number,
    Text,
    Identifier,
    Operator,
    And,
    Or,
    Not,
    OpenParen,
    CloseParen,
    End
}

/// <summary>Una pieza de la regla: su clase, su texto (sin las comillas, si es un texto) y su posición (el primer carácter es el 1).</summary>
public sealed record Token(TokenKind Kind, string Text, int Position);
