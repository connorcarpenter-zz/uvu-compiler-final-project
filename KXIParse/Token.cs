using System.Collections.Generic;
using KXIParse;

namespace KXIParse
{
    internal enum TokenType
    {
        Unknown,
        EOT,
        Identifier,
        Comment,
        Number,
        Atoi,
        Bool,
        Class,
        Char,
        Cin,
        Cout,
        Else,
        False,
        If,
        Int,
        Itoa,
        Main,
        New,
        Null,
        Object,
        Public,
        Private,
        Return,
        String,
        This,
        True,
        Void,
        While,
        Spawn,
        Lock,
        Release,
        Block,
        Sym,
        Kxi2015,
        Protected,
        Unprotected,
        And,
        Or,
        Equals,
        NotEquals,
        LessOrEqual,
        MoreOrEqual,
        Extraction,
        Insertion,
        Apostrophe,
        BlockBegin,
        BlockEnd,
        ArrayBegin,
        ArrayEnd,
        ParenBegin,
        ParenEnd,
        Assignment,
        Add,
        Subtract,
        Semicolon,
        Comma,
        Period,
        More,
        Less,
        Multiply,
        Divide
    };

    static class TokenDictionary
    {
        public static Dictionary<TokenType, string> Dictionary = new Dictionary<TokenType, string>
        {
            {TokenType.Comment, "^//[^#]*#N#"},
            {TokenType.Number, "^[\\+|\\-]?[0-9]+"},
            {TokenType.Atoi, "^atoi"},
            {TokenType.Bool, "^bool"},
            {TokenType.Class, "^class"},
            {TokenType.Char, "^char"},
            {TokenType.Cin, "^cin"},
            {TokenType.Cout, "^cout"},
            {TokenType.Else, "^else"},
            {TokenType.False, "^false"},
            {TokenType.If, "^if"},
            {TokenType.Int, "^int"},
            {TokenType.Itoa, "^itoa"},
            {TokenType.Main, "^main"},
            {TokenType.New, "^new"},
            {TokenType.Null, "^null"},
            {TokenType.Object, "^object"},
            {TokenType.Public, "^public"},
            {TokenType.Private, "^private"},
            {TokenType.Return, "^return"},
            {TokenType.String, "^string"},
            {TokenType.This, "^this"},
            {TokenType.True, "^true"},
            {TokenType.Void, "^void"},
            {TokenType.While, "^while"},
            {TokenType.Spawn, "^spawn"},
            {TokenType.Lock, "^lock"},
            {TokenType.Release, "^release"},
            {TokenType.Block, "^block"},
            {TokenType.Sym, "^sym"},
            {TokenType.Kxi2015, "^kxi2015"},
            {TokenType.Protected, "^protected"},
            {TokenType.Unprotected, "^unprotected"},
            {TokenType.And, "^&&"},
            {TokenType.Or, "^||"},
            {TokenType.Equals, "^=="},
            {TokenType.NotEquals, "^!="},
            {TokenType.LessOrEqual, "^<="},
            {TokenType.MoreOrEqual, "^>="},
            {TokenType.Extraction, "^<<"},
            {TokenType.Insertion, "^>>"},
            {TokenType.Apostrophe, "^'"},
            {TokenType.BlockBegin, "^{"},
            {TokenType.BlockEnd, "^}"},
            {TokenType.ArrayBegin, "^\\["},
            {TokenType.ArrayEnd, "^\\]"},
            {TokenType.ParenBegin, "^\\("},
            {TokenType.ParenEnd, "^\\)"},
            {TokenType.Assignment, "^="},
            {TokenType.Add, "^\\+"},
            {TokenType.Subtract, "^-"},
            {TokenType.Semicolon, "^;"},
            {TokenType.Comma, "^,"},
            {TokenType.Period, "^\\."},
            {TokenType.More, "^>"},
            {TokenType.Less, "^<"},
            {TokenType.Multiply, "^\\*"},
            {TokenType.Divide, "^/"},
            {TokenType.Unknown, ""},
            {TokenType.EOT, "^(#E#)+"},
            {TokenType.Identifier, "^[a-zA-Z][a-zA-Z0-9]{0,79}"}
        };

        public static Dictionary<TokenType, string> Get()
        {
            return Dictionary;
        }
    }

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
