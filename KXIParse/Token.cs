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
            {TokenType.Atoi, new TokenTypeData("Atoi","^atoi")},
            {TokenType.Bool, new TokenTypeData("Bool","^bool",TokenType.Type)},
            {TokenType.Class, new TokenTypeData("Class","^class")},
            {TokenType.Char, new TokenTypeData("Char","^char",TokenType.Type)},
            {TokenType.Cin, new TokenTypeData("Cin","^cin")},
            {TokenType.Cout, new TokenTypeData("Cout","^cout")},
            {TokenType.Else, new TokenTypeData("Else","^else")},
            {TokenType.False, new TokenTypeData("False","^false")},
            {TokenType.If, new TokenTypeData("If","^if")},
            {TokenType.Int, new TokenTypeData("Int","^int",TokenType.Type)},
            {TokenType.Itoa, new TokenTypeData("Itoa","^itoa")},
            {TokenType.Main, new TokenTypeData("Main","^main")},
            {TokenType.New, new TokenTypeData("New","^new")},
            {TokenType.Null, new TokenTypeData("Null","^null")},
            {TokenType.Object, new TokenTypeData("Object","^object")},
            {TokenType.Public, new TokenTypeData("Public","^public")},
            {TokenType.Private, new TokenTypeData("Private","^private")},
            {TokenType.Return, new TokenTypeData("Return","^return")},
            {TokenType.String, new TokenTypeData("String","^string")},
            {TokenType.Set, new TokenTypeData("String","^set")},
            {TokenType.This, new TokenTypeData("This","^this")},
            {TokenType.True, new TokenTypeData("True","^true")},
            {TokenType.Void, new TokenTypeData("Void","^void",TokenType.Type)},
            {TokenType.While, new TokenTypeData("While","^while")},
            {TokenType.Spawn, new TokenTypeData("Spawn","^checkSpawn")},
            {TokenType.Lock, new TokenTypeData("Lock","^lock")},
            {TokenType.Release, new TokenTypeData("Release","^release")},
            {TokenType.Block, new TokenTypeData("Block","^block")},
            {TokenType.Sym, new TokenTypeData("Sym","^sym",TokenType.Type)},
            {TokenType.Kxi2015, new TokenTypeData("Kxi2015","^kxi2015")},
            {TokenType.Protected, new TokenTypeData("Protected","^protected",TokenType.Modifier)},
            {TokenType.Unprotected, new TokenTypeData("Unprotected","^unprotected",TokenType.Modifier)},
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
            {TokenType.Identifier, new TokenTypeData("Identifier","^[a-zA-Z][a-zA-Z0-9]{0,79}",TokenType.Type)},//Make sure this is at the end of the dictionary so it's regex is evaluated last
            {TokenType.Modifier,new TokenTypeData("Modifier","")},
            {TokenType.Type,new TokenTypeData("Type","")},
        };
    }
}
