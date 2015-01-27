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
        private static bool Terminal(TokenType value,bool optional=false)
        {
            var first = _firstToken;
            if (!TokenData.Equals(first.Type, value))
            {
                if (optional)
                {
                    return false;
                }
                else
                {
                    throw new Exception(
                        string.Format(
                            "Error at line {0}: Expected value of type [{1}] , but instead found value of type [{2}] with a value of [{3}]",
                            first.LineNumber,
                            TokenData.Get()[value].Name,
                            TokenData.Get()[first.Type].Name,
                            first.Value));
                }
            }
            RemoveTopToken();
            return true;
        }
        private bool StartSymbol(List<Token> tokens)
        {
            while (ClassDeclaration(tokens))
                EmptyMethod();

            Terminal(TokenType.Void);
            Terminal(TokenType.Kxi2015);
            Terminal(TokenType.Main);
            Terminal(TokenType.ParenBegin);
            Terminal(TokenType.ParenEnd);

            MethodBody();

            return true;
        }

        private bool ClassDeclaration(List<Token> tokens)
        {
            try
            {
                Terminal(TokenType.Class);
                Terminal(TokenType.Identifier);
                Terminal(TokenType.BlockBegin);

                while (ClassMemberDeclaration())
                    EmptyMethod();

                Terminal(TokenType.BlockEnd);
            }
            catch (Exception e)
            {
                return false;
            }
            
            return true;
        }

        private bool ClassMemberDeclaration()
        {
            try
            {
                Terminal(TokenType.Modifier);
                Terminal(TokenType.Type);
                Terminal(TokenType.Identifier);//gotta do something with the symbol table here....

                if(!FieldDeclaration())
                    if (!ConstructorDeclaration())
                        return false;
            }
            catch (Exception e)
            {
                return false;
            }

            return true;
        }

        private bool FieldDeclaration()
        {
            if (!FieldDeclarationValue())
                if (!FieldDeclarationMethod())
                    return false;

            return true;
        }

        private bool FieldDeclarationValue()
        {
            try
            {
                if (Terminal(TokenType.ArrayBegin, true) != Terminal(TokenType.ArrayEnd, true))
                    throw new Exception(string.Format(
                            "Error at line {0}: Expected \"[]\", but found either [ or ] alone",
                            _firstToken.LineNumber));

                if (Terminal(TokenType.Assignment, true) != AssignmentExpression())
                    throw new Exception(string.Format(
                            "Error at line {0}: Expected an assignment expression.",
                            _firstToken.LineNumber));

                Terminal(TokenType.Semicolon);
            }
            catch (Exception e)
            {
                return false;
            }

            return true;
        }

        private bool FieldDeclarationMethod()
        {
            try
            {
                Terminal(TokenType.ParenBegin);

                ParameterList();

                Terminal(TokenType.ParenEnd);

                if (!MethodBody()) return false;

            }
            catch (Exception e)
            {
                return false;
            }

            return true;
        }

        private bool ConstructorDeclaration()
        {
            try
            {
                Terminal(TokenType.Identifier);
                Terminal(TokenType.ParenBegin);

                ParameterList();

                Terminal(TokenType.ParenEnd);

                if (!MethodBody()) return false;

            }
            catch (Exception e)
            {
                return false;
            }

            return true;
        }

        private bool ParameterList()
        {
            try
            {
                if (!Parameter()) return false;



                if(Terminal(TokenType.Comma,true) != Parameter())
                    throw new Exception(string.Format(
                            "Error at line {0}: Expected another parameter",
                            _firstToken.LineNumber));
            }
            catch (Exception e)
            {
                return false;
            }

            return true;
        }

        private bool Parameter()
        {
            try
            {
                Terminal(TokenType.Type);
                Terminal(TokenType.Identifier);
            }
            catch (Exception e)
            {
                return false;
            }

            return true;
        }
    }
}
