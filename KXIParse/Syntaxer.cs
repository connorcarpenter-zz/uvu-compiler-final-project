using System;
using System.Collections.Generic;
using System.Linq;

namespace KXIParse
{
    class Syntaxer
    {
        private const bool DEBUG = true;
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
            if(DEBUG)
                Console.WriteLine("Line {0}: {1} - {2}", 
                    GetToken().LineNumber,
                    TokenData.Get()[GetToken().Type].Name,
                    GetToken().Value);
            if(DEBUG && GetToken().LineNumber == 21)//when you're stepping through code, this'll take you straight to where you want to go
                Console.WriteLine("Arrived");
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

        private static bool Peek(TokenType value,int lookahead = 1)
        {
            if(lookahead==1)
                return TokenData.EqualTo(GetToken().Type, value);
            return TokenData.EqualTo(_tokensClone[lookahead - 1].Type, value);
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

        private bool AssignmentExpression()
        {
            if (Accept(TokenType.This))
                return true;

            if (Accept(TokenType.New))
            {
                Expect(TokenType.Type);
                NewDeclaration();
                return true;
            }

            if (Accept(TokenType.Atoi))
            {
                Expect(TokenType.ParenBegin);
                Expression();
                Expect(TokenType.ParenEnd);
                return true;
            }

            if (Accept(TokenType.Itoa))
            {
                Expect(TokenType.ParenBegin);
                Expression();
                Expect(TokenType.ParenEnd);
                return true;
            }

            return Expression();
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

                if (PeekExpression.Contains(GetToken().Type))
                    ArgumentList();

                Expect(TokenType.ParenEnd);
            }
        }

        private void ArgumentList()
        {
            var token = GetToken();

            if (!Expression())
                throw new Exception(string.Format("Invalid argument at line {0}", token.LineNumber));

            token = GetToken();
            while (Accept(TokenType.Comma))
                if (!Expression())
                    throw new Exception(string.Format("Invalid argument at line {0}", token.LineNumber));
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
                else if (Accept(TokenType.Character))
                {
                }
                else if (Accept(TokenType.Identifier))
                {
                    if (Peek(TokenType.ParenBegin) || Peek(TokenType.ArrayBegin))
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

        private bool ExpressionZ()
        {
            if (Accept(TokenType.Assignment)) return AssignmentExpression();
            if (Accept(TokenType.And)) return Expression();
            if (Accept(TokenType.Or)) return Expression();
            if (Accept(TokenType.Equals)) return Expression();
            if (Accept(TokenType.NotEquals)) return Expression();
            if (Accept(TokenType.LessOrEqual)) return Expression();
            if (Accept(TokenType.MoreOrEqual)) return Expression();
            if (Accept(TokenType.Less)) return Expression();
            if (Accept(TokenType.More)) return Expression();
            if (Accept(TokenType.Add)) return Expression();
            if (Accept(TokenType.Subtract)) return Expression();
            if (Accept(TokenType.Multiply)) return Expression();
            if (Accept(TokenType.Divide)) return Expression();
            return false;
        }

        private void MethodBody()
        {
            Expect(TokenType.BlockBegin);
            while (Peek(TokenType.Type) && Peek(TokenType.Identifier,2))
                VariableDeclaration();
            while (PeekStatement.Contains(GetToken().Type) || PeekExpression.Contains(GetToken().Type))
                Statement();
            Expect(TokenType.BlockEnd);
        }

        private void VariableDeclaration()
        {
            Expect(TokenType.Type);
            Expect(TokenType.Identifier);
            if (Accept(TokenType.ArrayBegin))
                Expect(TokenType.ArrayEnd);
            if (Accept(TokenType.Assignment))
                AssignmentExpression();
            Expect(TokenType.Semicolon);
        }

        private void Statement()
        {
            if (Accept(TokenType.BlockBegin))
            {
                while (PeekStatement.Contains(GetToken().Type) || PeekExpression.Contains(GetToken().Type))
                    Statement();
                Expect(TokenType.BlockEnd);
                return;
            }
            if (Accept(TokenType.If))
            {
                Expect(TokenType.ParenBegin);
                Expression();
                Expect(TokenType.ParenEnd);
                Statement();
                if (Accept(TokenType.Else))
                    Statement();
                return;
            }
            if (Accept(TokenType.While))
            {
                Expect(TokenType.ParenBegin);
                Expression();
                Expect(TokenType.ParenEnd);
                Statement();
                return;
            }
            if (Accept(TokenType.Return))
            {
                Expression();
                Expect(TokenType.Semicolon);
                return;
            }
            if (Accept(TokenType.Cout))
            {
                Expect(TokenType.Extraction);
                Expression();
                Expect(TokenType.Semicolon);
                return;
            }
            if (Accept(TokenType.Cin))
            {
                Expect(TokenType.Insertion);
                Expression();
                Expect(TokenType.Semicolon);
                return;
            }
            if (Accept(TokenType.Spawn))
            {
                Expression();
                Expect(TokenType.Set);
                Expect(TokenType.Identifier);
                Expect(TokenType.Semicolon);
                return;
            }
            if (Accept(TokenType.Block))
            {
                Expect(TokenType.Semicolon);
                return;
            }
            if (Accept(TokenType.Lock))
            {
                Expect(TokenType.Identifier);
                Expect(TokenType.Semicolon);
                return;
            }
            if (Accept(TokenType.Release))
            {
                Expect(TokenType.Identifier);
                Expect(TokenType.Semicolon);
                return;
            }
            if (Expression())
            {
                Expect(TokenType.Semicolon);
                return;
            }

            throw new Exception(string.Format("Line {0}: Expression not valid",GetToken().LineNumber));
        }

        private static readonly TokenType[] PeekStatement =
        {
            TokenType.BlockBegin,
            TokenType.If,
            TokenType.While,
            TokenType.Return,
            TokenType.Cout,
            TokenType.Cin,
            TokenType.Spawn,
            TokenType.Block,
            TokenType.Lock,
            TokenType.Release
        };
        private static readonly TokenType[] PeekExpression =
        {
            TokenType.ParenBegin,
            TokenType.True,
            TokenType.False,
            TokenType.Null,
            TokenType.Number,
            TokenType.Character,
            TokenType.Identifier
        };
    }
}

