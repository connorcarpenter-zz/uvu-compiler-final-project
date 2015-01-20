namespace KXIParse
{
    internal enum TokenType
    {
        Number,
        Character,
        Identifier,
        Punctuation,
        Keyword,
        Comment,
        Modifier,
        Type,
        MathOperator,
        RelationalOperator,
        BooleanOperator,
        AssignmentOperator,
        ArrayBegin,
        ArrayEnd,
        BlockBegin,
        BlockEnd,
        ParenBegin,
        ParenEnd,
        Unknown,
        EOT
    };

    class Token
    {
        public TokenType Type;
        public string Value;

        public Token(TokenType type, string value)
        {
            Type = type;
            Value = value;
        }
    }
}
