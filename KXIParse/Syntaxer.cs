using System;
using System.Collections.Generic;
using System.Linq;

namespace KXIParse
{
    class Syntaxer
    {
        private const bool DEBUG = false;
        private static Token lastToken;
        private readonly List<Token> _tokens;
        private static List<Token> _tokensClone;
        private static List<string> _scope;
        private static Dictionary<string,Symbol> _syntaxSymbolTable;
        private bool Syntaxing { get; set; }
        private bool Semanting { get; set; }
        private static Semanter _semanter;

        public Syntaxer(IEnumerable<Token> tokenList)
        {
            _tokens = new List<Token>(tokenList);
        }

        public Dictionary<string, Symbol> SyntaxPass()
        {
            Syntaxing = true;
            Semanting = false;
            InitTokens();
            StartSymbol();

            return _syntaxSymbolTable;
        }

        public void SemanticPass(Dictionary<string, Symbol> symbolTable)
        {
            Syntaxing = false;
            Semanting = true;
            _semanter = new Semanter(this,symbolTable);
            InitTokens();
            StartSymbol();
        }

        private void InitTokens()
        {
            _tokensClone = new List<Token>(_tokens);
            _syntaxSymbolTable = new Dictionary<string, Symbol>();
            _scope = new List<string> {"g"};
        }

        private string GetScopeString()
        {
            var output = "" + _scope[0] + ".";
            for (var index = 1; index < _scope.Count; index++)
            {
                output += _scope[index];
                if (index < _scope.Count - 1)
                    output += ".";
            }

        return output;
        }

        private string GenerateSymId(string kind)
        {
            var firstChar = "" + kind.ToUpper()[0];
            var i = 100;
            while (_syntaxSymbolTable.ContainsKey(firstChar + i))
                i++;
            return firstChar + i;
        }

        private static Token GetToken()
        {
            return _tokensClone.Count == 0 ? null : _tokensClone.First();
        }

        private static void NextToken()
        {
            if(DEBUG)
                Console.WriteLine("Line {0}: {1} - {2}", 
                    GetToken().LineNumber,
                    TokenData.Get()[GetToken().Type].Name,
                    GetToken().Value);
            _tokensClone.RemoveAt(0);
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
            lastToken = GetToken();
            NextToken();
            DebugTracking();
            return true;
        }

        private static bool Expect(TokenType value)
        {
            if (Accept(value))
                return true;
            throw new Exception(string.Format(
                            "Syntax error at line {0}. Expected a token of type: {1}, but found a: {2}",
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

            var className = ClassName();
            if (Syntaxing)
            {
                //Put class into symbol table
                var symId = GenerateSymId("Class");
                _syntaxSymbolTable.Add(symId,
                    new Symbol()
                    {
                        Data = null,
                        Kind = "Class",
                        Scope = GetScopeString(),
                        SymId = symId,
                        Value = className
                    });
            }

            Expect(TokenType.BlockBegin);

            //go into class scope
            _scope.Add(className);

            //instance variables, ect.
            while (Peek(TokenType.Modifier))
            {
                ClassMemberDeclaration();
            }

            //go out of scope
            outScope(className);

            Expect(TokenType.BlockEnd);
        }

        private string ClassName()
        {
            var className = GetToken().Value;
            Expect(TokenType.Identifier);
            return className;
        }

        private void outScope(string name)
        {
            if (_scope.Last().Equals(name))
            {
                _scope.RemoveAt(_scope.Count - 1);
            }
            else
            {
                var t = GetToken();
                throw new Exception(string.Format("Syntax error at line {0}: scope tracking got out of sync somehow?",
                                    t.LineNumber));
            }
        }

        private void ClassMemberDeclaration()
        {
            if (Peek(TokenType.Modifier))
            {
                var modifier = GetToken().Value;
                Expect(TokenType.Modifier);

                var type = GetToken().Value;
                Expect(TokenType.Type);

                var name = GetToken().Value;
                Expect(TokenType.Identifier);

                FieldDeclaration(modifier,type,name);
            }
            else
            {
                ConstructorDeclaration();
            }
        }

        private void FieldDeclaration(string modifier, string type, string name)
        {
            if (Peek(TokenType.ArrayBegin) || Peek(TokenType.Assignment) || Peek(TokenType.Semicolon))
            {
                var isArray = false;
                if (Peek(TokenType.ArrayBegin))
                {
                    isArray = true;
                    Expect(TokenType.ArrayBegin);
                    Expect(TokenType.ArrayEnd);
                }
                if (Peek(TokenType.Assignment))
                {
                    Expect(TokenType.Assignment);
                    AssignmentExpression();
                }
                Expect(TokenType.Semicolon);

                //add to symbol table
                if (Syntaxing)
                {
                    var symId = GenerateSymId("Variable");
                    _syntaxSymbolTable.Add(symId,
                        new Symbol()
                        {
                            Data = new Data()
                            {
                                Type = type,
                                AccessMod = modifier,
                                IsArray = isArray,
                            },
                            Kind = "ivar",
                            Scope = GetScopeString(),
                            SymId = symId,
                            Value = name
                        });
                }
            }
            else
            {
                var paramList = new List<string>();

                Expect(TokenType.ParenBegin);
                if (Peek(TokenType.Type))
                {
                    //go into method's scope
                    _scope.Add(name);

                    //add parameters
                    ParameterList(paramList);

                    //leave scope
                    outScope(name);
                }
                Expect(TokenType.ParenEnd);

                //add method to symbol table
                if (Syntaxing)
                {
                    var symId = GenerateSymId("Method");
                    _syntaxSymbolTable.Add(symId,
                        new Symbol()
                        {
                            Kind = "method",
                            Scope = GetScopeString(),
                            SymId = symId,
                            Value = name,
                            Data = new Data()
                            {
                                Type = type,
                                AccessMod = modifier,
                                Params = paramList
                            }
                        });
                }

                //go into method's scope
                _scope.Add(name);

                MethodBody();

                //leave
                outScope(name);
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

        private void ParameterList(List<string> paramList = null)
        {
            Parameter(paramList);
            while (Accept(TokenType.Comma))
                Parameter(paramList);
        }

        private void Parameter(List<string> paramList)
        {
            var type = GetToken().Value;
            Expect(TokenType.Type);

            var name = GetToken().Value;
            Expect(TokenType.Identifier);

            var isArray = false;
            if (Accept(TokenType.ArrayBegin))
            {
                Expect(TokenType.ArrayEnd);
                isArray = true;
            }

            //add to symbol table
            if (Syntaxing)
            {
                var symId = GenerateSymId("Parameter");
                _syntaxSymbolTable.Add(symId,
                    new Symbol()
                    {
                        Data = new Data()
                        {
                            Type = type,
                            AccessMod = "protected",
                            IsArray = isArray,
                        },
                        Kind = "param",
                        Scope = GetScopeString(),
                        SymId = symId,
                        Value = name
                    });
                if (paramList != null)
                    paramList.Add(symId);
            }
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
            if(Semanting)
                _semanter.BAL();

            var token = GetToken();

            if (!Expression())
                throw new Exception(string.Format("Invalid argument at line {0}", token.LineNumber));

            token = GetToken();
            while (Accept(TokenType.Comma))
            {
                if (Semanting)
                    _semanter.commaPop();
                if (!Expression())
                    throw new Exception(string.Format("Invalid argument at line {0}", token.LineNumber));
            }

            if (Semanting)
            {
                if (Peek(TokenType.ParenEnd))
                {
                    _semanter.parenBeginPop();
                    _semanter.EAL();
                }
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
                else if (Accept(TokenType.Character))
                {
                }
                else if (Accept(TokenType.Identifier))
                {
                    if(Semanting)
                        _semanter.iPush(lastToken.Value);
                    if (Peek(TokenType.ParenBegin) || Peek(TokenType.ArrayBegin))
                        FnArrMember();
                    if (Semanting) _semanter.iExist(GetScopeString(),lastToken.LineNumber);
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
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
            return true;
        }

        private void FnArrMember()
        {
            if (Accept(TokenType.ArrayBegin))
            {
                Expression();
                Expect(TokenType.ArrayEnd);
            }
            else
            {
                Expect(TokenType.ParenBegin);

                if (Semanting)
                    _semanter.oPush(Semanter.Operator.ParenBegin, lastToken.LineNumber);

                if (PeekExpression.Contains(GetToken().Type))
                    ArgumentList();

                Expect(TokenType.ParenEnd);

                if(Semanting)
                    _semanter.func();
            }
        }

        private void MemberRefz()
        {
            Expect(TokenType.Period);
            Expect(TokenType.Identifier);
            if (Semanting) _semanter.iPush(lastToken.Value);
            
            if(Peek(TokenType.ParenBegin) || Peek(TokenType.ArrayBegin))
                FnArrMember();
            if (Semanting)
                _semanter.rExist(lastToken.LineNumber);
            if (Peek(TokenType.Period))
                MemberRefz();
        }

        private bool ExpressionZ()
        {
            if (Accept(TokenType.Assignment))
            {
                if (Semanting)
                    _semanter.oPush(Semanter.Operator.Assignment, lastToken.LineNumber);
                return AssignmentExpression();
            }
            if (Accept(TokenType.And) || Accept(TokenType.Or) || Accept(TokenType.Equals) ||
                Accept(TokenType.NotEquals) || Accept(TokenType.LessOrEqual) || Accept(TokenType.MoreOrEqual) ||
                Accept(TokenType.Less) || Accept(TokenType.More) || Accept(TokenType.Add) ||
                Accept(TokenType.Subtract) || Accept(TokenType.Multiply) || Accept(TokenType.Divide))
            {
                if (Semanting)
                {
                    switch (lastToken.Type)
                    {
                        case TokenType.And:
                            _semanter.oPush(Semanter.Operator.And,lastToken.LineNumber);
                            break;
                        case TokenType.Or:
                            _semanter.oPush(Semanter.Operator.Or, lastToken.LineNumber);
                            break;
                        case TokenType.Equals:
                            _semanter.oPush(Semanter.Operator.Equals, lastToken.LineNumber);
                            break;
                        case TokenType.NotEquals:
                            _semanter.oPush(Semanter.Operator.NotEquals, lastToken.LineNumber);
                            break;
                        case TokenType.LessOrEqual:
                            _semanter.oPush(Semanter.Operator.LessOrEqual, lastToken.LineNumber);
                            break;
                        case TokenType.MoreOrEqual:
                            _semanter.oPush(Semanter.Operator.MoreOrEqual, lastToken.LineNumber);
                            break;
                        case TokenType.Less:
                            _semanter.oPush(Semanter.Operator.Less, lastToken.LineNumber);
                            break;
                        case TokenType.Add:
                            _semanter.oPush(Semanter.Operator.Add, lastToken.LineNumber);
                            break;
                        case TokenType.Subtract:
                            _semanter.oPush(Semanter.Operator.Subtract, lastToken.LineNumber);
                            break;
                        case TokenType.Multiply:
                            _semanter.oPush(Semanter.Operator.Multiply, lastToken.LineNumber);
                            break;
                        case TokenType.Divide:
                            _semanter.oPush(Semanter.Operator.Divide, lastToken.LineNumber);
                            break;
                    }
                }
                return Expression();
            }
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
            var type = GetToken().Value;
            Expect(TokenType.Type);

            var name = GetToken().Value;
            Expect(TokenType.Identifier);

            var isArray = false;
            if (Accept(TokenType.ArrayBegin))
            {
                Expect(TokenType.ArrayEnd);
                isArray = true;
            }
            if (Accept(TokenType.Assignment))
                AssignmentExpression();
            Expect(TokenType.Semicolon);

            if (Syntaxing)
            {
                var symId = GenerateSymId("Local Variable");
                _syntaxSymbolTable.Add(symId,
                    new Symbol()
                    {
                        Data = new Data()
                        {
                            Type = type,
                            AccessMod = "protected",
                            IsArray = isArray,
                        },
                        Kind = "lvar",
                        Scope = GetScopeString(),
                        SymId = symId,
                        Value = name
                    });
            }
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
                if(Semanting)
                    _semanter.EOE(lastToken.LineNumber);
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

        private static void DebugTracking()
        {
            if (GetToken() == null) return;
            if(DEBUG)
            if (GetToken().LineNumber == 8)//when you're stepping through code, this'll take you straight to where you want to go
                Console.WriteLine("Arrived");
        }
    }

}

