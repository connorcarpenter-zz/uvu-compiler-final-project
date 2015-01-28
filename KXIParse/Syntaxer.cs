using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Xml;

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
            /*
            foreach (var t in tokens.Where(t => t.Type == TokenType.Unknown))
                throw new Exception(string.Format("Unknown symbol on line {0}: {1}",
                                    t.LineNumber,
                                    t.Value));*/
            return tokens;
        }
        public List<Symbol> ParseTokens()
        {
            InitTokens();
            var symbolTable = new List<Symbol>();
            
            StartSymbol();

            return symbolTable;
        }

        private static void EmptyMethod()
        {
        }
        private static bool Peek(TokenType value)
        {
            return TokenData.EqualTo(GetToken().Type, value);
        }

        private static bool Accept(TokenType value)
        {
            if (!TokenData.EqualTo(GetToken().Type, value))
                return false;
            NextToken();
            return true;
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

        private void StartSymbol()
        {
            while (Peek(TokenType.Class))
            {
                ClassDeclaration();
            };

            Expect(TokenType.Void);
            Expect(TokenType.Kxi2015);
            Expect(TokenType.Main);
            Expect(TokenType.ParenBegin);
            Expect(TokenType.ParenEnd);

            MethodBody();
        }

        private void ClassDeclaration()
        {
            Expect(TokenType.Class);
            ClassName();
            Expect(TokenType.BlockBegin);
            while (Peek(TokenType.Modifier))
            {
                ClassMemberDeclaration();
            }
            Expect(TokenType.BlockEnd);
        }

        private void ClassName()
        {
            Expect(TokenType.Identifier);
        }

        private void ClassMemberDeclaration()
        {
            if (Peek(TokenType.Modifier))
            {
                Expect(TokenType.Modifier);
                Expect(TokenType.Type);
                Expect(TokenType.Identifier);
                FieldDeclaration();
            }
            else
            {
                ConstructorDeclaration();
            }
        }

        private void FieldDeclaration()
        {
            if (Peek(TokenType.ArrayBegin) || Peek(TokenType.Assignment) || Peek(TokenType.Semicolon))
            {
                if (Peek(TokenType.ArrayBegin))
                {
                    Expect(TokenType.ArrayBegin);
                    Expect(TokenType.ArrayEnd);
                }
                if (Peek(TokenType.Assignment))
                {
                    Expect(TokenType.Assignment);
                    AssignmentExpression();
                }
                Expect(TokenType.Semicolon);
            }
            else
            {
                Expect(TokenType.ParenBegin);
                if (Peek(TokenType.Type))
                {
                    ParameterList();
                }
                Expect(TokenType.ParenEnd);
                MethodBody();
            }
        }

        private void ConstructorDeclaration()
        {
            ClassName();
            Expect(TokenType.ParenBegin);
            if (Peek(TokenType.Type))
            {
                ParameterList();
            }
            Expect(TokenType.ParenEnd);
            MethodBody();
        }

        private void AssignmentExpression()
        {
            if (Accept(TokenType.This))
                return;

            if (Accept(TokenType.New))
            {
                Expect(TokenType.Type);
                NewDeclaration();
                return;
            }

            if (Accept(TokenType.Atoi))
            {
                Expect(TokenType.ParenBegin);
                Expression();
                Expect(TokenType.ParenEnd);
                return;
            }

            if (Accept(TokenType.Itoa))
            {
                Expect(TokenType.ParenBegin);
                Expression();
                Expect(TokenType.ParenEnd);
                return;
            }

            Expression();
        }

        private void ParameterList()
        {
            Parameter();
            while (Accept(TokenType.Comma))
                Parameter();
        }

        private void Parameter()
        {
            Expect(TokenType.Type);
            Expect(TokenType.Identifier);
            if (Accept(TokenType.ArrayBegin))
                Expect(TokenType.ArrayEnd);
        }

        private void NewDeclaration()
        {
            if (Accept(TokenType.ArrayBegin))
            {
                Expression();
                Expect(TokenType.ArrayEnd);
            }
            else
            {
                Expect(TokenType.ParenBegin);

                var token = GetToken();
                if (!Expression())
                    throw new Exception(string.Format("Invalid argument at line {0}", token.LineNumber));

                token = GetToken();
                while(Accept(TokenType.Comma))
                    if (!Expression())
                        throw new Exception(string.Format("Invalid argument at line {0}",token.LineNumber));

                Expect(TokenType.ParenEnd);
            }
        }

        private bool Expression()
        {
            //put expects in a try catch, need to return true true/false here
            try
            {
                if (Accept(TokenType.ParenBegin))
                {
                    if (!Expression())
                        return false;
                    Expect(TokenType.ParenEnd);

                }
                else if (Accept(TokenType.Apostrophe))
                {
                    Character();
                    Expect(TokenType.Apostrophe);
                }
                else if (Accept(TokenType.Identifier))
                {
                    if (Peek(TokenType.ParenBegin))
                        FnArrMember();
                    if (Peek(TokenType.Period))
                        MemberRefz();
                }
                else if (!Accept(TokenType.True) &&
                         !Accept(TokenType.False) &&
                         !Accept(TokenType.Null) &&
                         !Accept(TokenType.Number))
                {
                    return false;
                }
                var backupList = new List<Token>(_tokensClone);
                if (!ExpressionZ())
                    _tokensClone = backupList;
            }
            catch
            {
                return false;
            }
            return true;
        }

        private void Character()
        {
            var success = false;
            var token = GetToken();
            if (Accept(TokenType.Unknown))
            {
                var value = token.Value;
                switch (value.Length)
                {
                    case 1:
                        var intValue = Convert.ToInt32(value[0]);
                        if (intValue >= 32 && intValue <= 126) success = true;
                        break;
                    case 2:
                        if (value.Equals("\n") || value.Equals("\r") || value.Equals("\t"))
                            success = true;
                        break;
                }
            }
            if(!success)
                throw new Exception(string.Format("Line {0}: Expected valid ascii character",token.LineNumber));
        }

        private void FnArrMember()
        {
            NewDeclaration();
        }

        private void MemberRefz()
        {
            Expect(TokenType.Period);
            Expect(TokenType.Identifier);
            if(Peek(TokenType.ParenBegin) || Peek(TokenType.ArrayBegin))
                FnArrMember();
            if(Peek(TokenType.Period))
                MemberRefz();
        }
    }
}
