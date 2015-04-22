using System.Collections.Generic;

namespace KXIParse
{
    public enum TokenType
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
        Set,
        Int,
        Itoa,
        Main,
        New,
        Null,
        Object,
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
        Character,
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
        Divide,
        Modifier,
        Global,
        Type
    };

    class Token
    {
        public TokenType Type;
        public string Value;
        public int LineNumber;

        public Token(TokenType type, string value,int lineNumber)
        {
            Type = type;
            Value = value;
            LineNumber = lineNumber;
        }
    }

    public class TokenTypeData
    {
        public string Regex { get; set; }
        public string Name { get; set; }
        public TokenType Parent { get; set; }

        internal TokenTypeData(string name, string regex,TokenType parent = TokenType.Global)
        {
            Name = name;
            Regex = regex;
            Parent = parent;
        }
    }
    public static class TokenData
    {
        public static bool EqualTo(TokenType type1, TokenType type2)
        {
            var type = type1;
            while (true)
            {
                if (type == type2) return true;
                if (type == TokenType.Global) return false;
                type = TokenData.Get()[type].Parent;
            }
        }

        public static Dictionary<TokenType, TokenTypeData> Get()
        {
            return Dictionary;
        }
        private static readonly Dictionary<TokenType, TokenTypeData> Dictionary = new Dictionary<TokenType, TokenTypeData>
        {
            {TokenType.Comment, new TokenTypeData("Comment","^//[^#]*")},
            {TokenType.Number, new TokenTypeData("Number","^[\\+|\\-]?[0-9]+")},
            {TokenType.Character, new TokenTypeData("Character","^'[\\\\]?[\x20-\x7E]'")},
            {TokenType.Atoi, new TokenTypeData("Atoi","\batoi\b")},
            {TokenType.Bool, new TokenTypeData("Bool","\bbool\b",TokenType.Type)},
            {TokenType.Class, new TokenTypeData("Class",@"\bclass\b")},
            {TokenType.Char, new TokenTypeData("Char","\bchar\b",TokenType.Type)},
            {TokenType.Cin, new TokenTypeData("Cin","\bcin\b")},
            {TokenType.Cout, new TokenTypeData("Cout","\bcout\b")},
            {TokenType.Else, new TokenTypeData("Else","\belse\b")},
            {TokenType.False, new TokenTypeData("False","\bfalse\b")},
            {TokenType.If, new TokenTypeData("If","\bif\b")},
            {TokenType.Int, new TokenTypeData("Int","\bint\b",TokenType.Type)},
            {TokenType.Itoa, new TokenTypeData("Itoa","\bitoa\b")},
            {TokenType.Main, new TokenTypeData("Main","\bmain\b")},
            {TokenType.New, new TokenTypeData("New","\bnew\b")},
            {TokenType.Null, new TokenTypeData("Null","\bnull\b")},
            {TokenType.Object, new TokenTypeData("Object","\bobject\b")},
            {TokenType.Return, new TokenTypeData("Return","\breturn\b")},
            {TokenType.String, new TokenTypeData("String","\bstring\b")},
            {TokenType.Set, new TokenTypeData("String","\bset\b")},
            {TokenType.This, new TokenTypeData("This","\bthis\b")},
            {TokenType.True, new TokenTypeData("True","\btrue\b")},
            {TokenType.Void, new TokenTypeData("Void","\bvoid\b",TokenType.Type)},
            {TokenType.While, new TokenTypeData("While","\bwhile\b")},
            {TokenType.Spawn, new TokenTypeData("Spawn","\bspawn\b")},
            {TokenType.Lock, new TokenTypeData("Lock","\block\b")},
            {TokenType.Release, new TokenTypeData("Release","\brelease\b")},
            {TokenType.Block, new TokenTypeData("Block","\bblock\b")},
            {TokenType.Sym, new TokenTypeData("Sym","\bsym\b",TokenType.Type)},
            {TokenType.Kxi2015, new TokenTypeData("Kxi2015","\bkxi2015\b")},
            {TokenType.Protected, new TokenTypeData("Protected","\bprotected\b",TokenType.Modifier)},
            {TokenType.Unprotected, new TokenTypeData("Unprotected","\bunprotected\b",TokenType.Modifier)},
            {TokenType.And, new TokenTypeData("And","^&&")},
            {TokenType.Or, new TokenTypeData("Or","^\\|\\|")},
            {TokenType.Equals, new TokenTypeData("Equals","^==")},
            {TokenType.NotEquals, new TokenTypeData("NotEquals","^!=")},
            {TokenType.LessOrEqual, new TokenTypeData("LessOrEqual","^<=")},
            {TokenType.MoreOrEqual, new TokenTypeData("MoreOrEqual","^>=")},
            {TokenType.Extraction, new TokenTypeData("Extraction","^<<")},
            {TokenType.Insertion, new TokenTypeData("Insertion","^>>")},
            {TokenType.BlockBegin, new TokenTypeData("BlockBegin","^{")},
            {TokenType.BlockEnd, new TokenTypeData("BlockEnd","^}")},
            {TokenType.ArrayBegin, new TokenTypeData("ArrayBegin","^\\[")},
            {TokenType.ArrayEnd, new TokenTypeData("ArrayEnd","^\\]")},
            {TokenType.ParenBegin, new TokenTypeData("ParenBegin","^\\(")},
            {TokenType.ParenEnd, new TokenTypeData("ParenEnd","^\\)")},
            {TokenType.Assignment, new TokenTypeData("Assignment","^=")},
            {TokenType.Add, new TokenTypeData("Add","^\\+")},
            {TokenType.Subtract, new TokenTypeData("Subtract","^-")},
            {TokenType.Semicolon, new TokenTypeData("Semicolon","^;")},
            {TokenType.Comma, new TokenTypeData("Comma","^,")},
            {TokenType.Period, new TokenTypeData("Period","^\\.")},
            {TokenType.More, new TokenTypeData("More","^>")},
            {TokenType.Less, new TokenTypeData("Less","^<")},
            {TokenType.Multiply, new TokenTypeData("Multiply","^\\*")},
            {TokenType.Divide, new TokenTypeData("Divide","^/")},
            {TokenType.Unknown, new TokenTypeData("Unknown","")},
            {TokenType.EOT, new TokenTypeData("End of Tokens","^(#E#)+")},
            {TokenType.Identifier, new TokenTypeData("Identifier","\b[a-zA-Z][a-zA-Z0-9]{0,79}\b",TokenType.Type)},//Make sure this is at the end of the dictionary so it's regex is evaluated last
            {TokenType.Modifier,new TokenTypeData("Modifier","")},
            {TokenType.Type,new TokenTypeData("Type","")},
        };
    }
}
