using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace KXIParse
{
    class Syntaxer
    {
        private readonly List<Token> _tokens;

        public Syntaxer(IEnumerable<Token> tokenList)
        {
            _tokens = CleanTokens(tokenList);
        }

        private static List<Token> CleanTokens(IEnumerable<Token> tokenList)
        {
            var tokens = new List<Token>(tokenList);

            //remove all comments
            tokens.RemoveAll(s => s.Type == TokenType.Comment);

            //throw exception if there's any unknowns
            foreach (var t in tokens.Where(t => t.Type == TokenType.Unknown))
                throw new Exception(string.Format("Unknown symbol on line {0}: {1}",
                                    t.LineNumber,
                                    t.Value));
            return tokens;
        }
        public List<Symbol> ParseTokens()
        {
            var symbolTable = new List<Symbol>();
            var tokens = new List<Token>(_tokens);

            var parseSuccess = StartSymbol(tokens);

            return symbolTable;
        }

        private static void EmptyMethod()
        {
        }
        private static bool Terminal(IList<Token> tokens,TokenType value)
        {
            var first = tokens.First();
            if (!TokenData.Equals(first.Type,value))
                throw new Exception(string.Format("Error at line {0}: Expected value of type [{1}] , but instead found value of type [{2}] with a value of [{3}]",
                    first.LineNumber,
                    TokenData.Get()[value].Name,
                    TokenData.Get()[first.Type].Name,
                    first.Value));
            tokens.RemoveAt(0);
            return true;
        }
        private bool StartSymbol(List<Token> tokens)
        {
            while (ClassDeclaration(tokens))
                EmptyMethod();

            Terminal(tokens, TokenType.Void);
            Terminal(tokens, TokenType.Kxi2015);
            Terminal(tokens, TokenType.Main);
            Terminal(tokens, TokenType.ParenBegin);
            Terminal(tokens, TokenType.ParenEnd);

            MethodBody(tokens);

            return true;
        }

        private bool ClassDeclaration(List<Token> tokens)
        {
            try
            {
                Terminal(tokens, TokenType.Class);
                Terminal(tokens, TokenType.Identifier);
                Terminal(tokens, TokenType.BlockBegin);

                while (ClassMemberDeclaration(tokens))
                    EmptyMethod();

                Terminal(tokens, TokenType.BlockEnd);
            }
            catch (Exception e)
            {
                return false;
            }
            
            return true;
        }

        private bool ClassMemberDeclaration(List<Token> tokens)
        {
            try
            {
                Terminal(tokens, TokenType.Modifier);
                Terminal(tokens, TokenType.Type);
                Terminal(tokens, TokenType.Identifier);//gotta do something with the symbol table here....

                if(!FieldDeclaration(tokens))
                    if (!ConstructorDeclaration(tokens))
                        return false;
            }
            catch (Exception e)
            {
                return false;
            }

            return true;
        }
    }
}
