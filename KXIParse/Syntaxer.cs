using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace KXIParse
{
    class Syntaxer
    {
        private readonly List<Token> _tokens;
        private static List<Token> _tokensClone;

        private void InitTokens()
        {
            _tokensClone = new List<Token>(_tokens);
        }
        private static Token GetToken()
        {
            return _tokensClone.First();
        }
        private static void NextToken()
        {
            _tokensClone.RemoveAt(0);
        }

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
            InitTokens();
            var symbolTable = new List<Symbol>();
            
            var parseSuccess = StartSymbol();

            return symbolTable;
        }

        private static void EmptyMethod()
        {
        }
        private static bool Accept(TokenType value)
        {
            if(TokenData.Equals(GetToken(),value)
            {
                NextToken();
                return true;
            }
            return false;
        }
        private static bool Expect(TokenType value)
        {
            if (Accept(value))
                return true;
            throw new Exception(string.Format(
                            "Error at line {0}. Expected a token of type: {1}, but found a: {2}",
                            GetToken().LineNumber,
                            TokenData.Get()[value].Name,
                            TokenData.Get()[GetToken().Type].Name
                            ));
            return false;
        }

        private bool StartSymbol()
        {
            while (Accept(TokenType.Class))
            {
                ClassDeclaration();
            };

            Expect(TokenType.Void);
            Expect(TokenType.Kxi2015);
            Expect(TokenType.Main);
            Expect(TokenType.ParenBegin);
            Expect(TokenType.ParenEnd);

            MethodBody();

            return true;
        }



    }
}
