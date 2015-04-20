using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Linq;

namespace KXIParse
{
    class Syntaxer
    {
        private const bool DEBUGTOKENS = false;
        private const bool DEBUGMETA = false;
        private static Token lastToken;
        private List<Token> _tokens;
        private static List<Token> _tokensClone;
        private static List<string> _scope;
        public static Dictionary<string,Symbol> _syntaxSymbolTable;
        private bool Syntaxing { get; set; }
        private bool Semanting { get; set; }
        private bool ConstructorCreated { get; set; }
        private static Semanter _semanter;
        private static List<Token> _recordTokens;
        private static List<Token> _insertTokens;
        private static bool _recording = true;
 

        public Syntaxer(IEnumerable<Token> tokenList)
        {
            _tokens = new List<Token>(tokenList);
        }

        public Dictionary<string, Symbol> SyntaxPass()
        {
            Syntaxing = true;
            Semanting = false;
            _recordTokens = new List<Token>();
            _insertTokens = new List<Token>();
            InitTokens();
            StartSymbol();
            _tokens = _recordTokens;

            return _syntaxSymbolTable;
        }

        public List<Quad> SemanticPass(Dictionary<string, Symbol> symbolTable)
        {
            Syntaxing = false;
            Semanting = true;
            _recordTokens = null;
            _insertTokens = null;
            var icodeList = new List<Quad>();
            _semanter = new Semanter(symbolTable,icodeList);
            InitTokens();
            _syntaxSymbolTable = new Dictionary<string, Symbol>(symbolTable);
            StartSymbol();

            //add Jump to Main call
            icodeList.Insert(0, new Quad("", "FRAME", "MAIN", "null", ""));
            icodeList.Insert(1, new Quad("", "CALL", "MAIN", "", ""));
            icodeList.Insert(2, new Quad("", "END", "", "", ""));

            return icodeList;
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

            if (output.Equals("g.")) return "g";

        return output;
        }

        private string GenerateSymId(string kind)
        {
            //var firstChar = "" + kind.ToUpper()[0];
            var firstChar = "_"+kind.ToLower().Replace(" ", "_").Substring(0,3);
            var i = 0;
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
            if(DEBUGTOKENS)
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

            if (_recordTokens!=null && _insertTokens!=null)
            {
                if (_recording) _recordTokens.Add(lastToken);
                else _insertTokens.Add(lastToken);
            }

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
            if(DEBUGMETA)Console.WriteLine("--Start Symbol");
            while (Peek(TokenType.Class))
            {
                ClassDeclaration();
            };

            Expect(TokenType.Void);
            Expect(TokenType.Kxi2015);
            Expect(TokenType.Main);
            Expect(TokenType.ParenBegin);
            Expect(TokenType.ParenEnd);

            if (Semanting)
            {
                //add label
                _semanter.AddMethodLabel("main");
            }

            //go into main scope
            _scope.Add("main");

            MethodBody();
            
            //go out of scope
            outScope("main");

            //add ending return statement if it wasn't there
            //if(Semanting)_semanter.End();
            AddImpliedReturnStatement();
        }

        private void AddImpliedReturnStatement()
        {
            if (_recordTokens == null || _insertTokens == null) return;

            if (_recording)
            {
                _recordTokens.Insert(_recordTokens.Count - 1,new Token(TokenType.Return,"return",lastToken.LineNumber));
                _recordTokens.Insert(_recordTokens.Count - 1, new Token(TokenType.Semicolon, ";", lastToken.LineNumber));
            }
            else
            {
                _insertTokens.Insert(_insertTokens.Count - 1, new Token(TokenType.Return, "return", lastToken.LineNumber));
                _insertTokens.Insert(_insertTokens.Count - 1, new Token(TokenType.Semicolon, ";", lastToken.LineNumber));
            }
        }

        private void ClassDeclaration()
        {
            if (DEBUGMETA) Console.WriteLine("--Class Declaration");
            Expect(TokenType.Class);

            var className = ClassName();
            if (Syntaxing)
            {
                //Put class into symbol table
                var symId = GenerateSymId("Class");
                _syntaxSymbolTable.Add(symId,
                    new Symbol()
                    {
                        Data = new Data {Type = className},
                        Kind = "Class",
                        Scope = GetScopeString(),
                        SymId = symId,
                        Value = className,
                        Vars = 0
                    });
            }

            Expect(TokenType.BlockBegin);

            //go into class scope
            _scope.Add(className);

            if(Syntaxing)
                ConstructorCreated = false;

            //instance variables, ect.
            while (Peek(TokenType.Modifier) || Peek(TokenType.Identifier))
            {
                ClassMemberDeclaration();
            }

            if (Syntaxing)
            {
                if (!ConstructorCreated)
                {
                    _recordTokens.Add(new Token(TokenType.Identifier,className,lastToken.LineNumber));
                    _recordTokens.Add(new Token(TokenType.ParenBegin, "(", lastToken.LineNumber));
                    _recordTokens.Add(new Token(TokenType.ParenEnd, ")", lastToken.LineNumber));

                    var symId = GenerateSymId("Constructor");
                    _syntaxSymbolTable.Add(symId,
                        new Symbol()
                        {
                            Data = new Data { Type = className },
                            Kind = "Constructor",
                            Scope = GetScopeString(),
                            SymId = symId,
                            Value = className
                        });

                    _recordTokens.Add(new Token(TokenType.BlockBegin, "{", lastToken.LineNumber));
                    foreach(var r in _insertTokens)
                        _recordTokens.Add(r);
                    _recordTokens.Add(new Token(TokenType.Return, "return", lastToken.LineNumber));
                    _recordTokens.Add(new Token(TokenType.Semicolon, ";", lastToken.LineNumber));
                    _recordTokens.Add(new Token(TokenType.BlockEnd, "}", lastToken.LineNumber));

                    _insertTokens.Clear();

                    ConstructorCreated = true;
                }
            }

            //go out of scope
            outScope(className);

            Expect(TokenType.BlockEnd);
        }

        private string ClassName()
        {
            if (DEBUGMETA) Console.WriteLine("--Class Name");

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
            if (DEBUGMETA) Console.WriteLine("--Class Member Declaration");

            if (Peek(TokenType.Modifier))
            {
                var modifier = GetToken().Value;
                Expect(TokenType.Modifier);

                var type = GetToken().Value;
                Expect(TokenType.Type);

                if (Semanting)
                {
                    _semanter.tPush(lastToken.Value, GetScopeString());
                    _semanter.tExist(lastToken.LineNumber);
                }

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
            if(Semanting)//remove this
                if (DEBUGMETA)
                    Console.WriteLine("Field Declaration");

            if (Peek(TokenType.ArrayBegin) || Peek(TokenType.Assignment) || Peek(TokenType.Semicolon))
            {
                var isArray = false;
                if (Peek(TokenType.ArrayBegin))
                {
                    isArray = true;
                    Expect(TokenType.ArrayBegin);
                    Expect(TokenType.ArrayEnd);
                }
                if (Semanting)
                    _semanter.vPush(GetScopeString(), name, isArray,true);
                if (Peek(TokenType.Assignment))
                {
                    if (Syntaxing)
                    {
                        _recording = false;
                        _insertTokens.Add(lastToken);
                    }

                    Expect(TokenType.Assignment);
                    if(Semanting)
                        _semanter.oPush(Semanter.Operator.Assignment,lastToken.LineNumber);
                    AssignmentExpression();
                }
                Expect(TokenType.Semicolon);
                if (Syntaxing)
                {
                    if (!_recording)
                    {
                        _recording = true;
                        _recordTokens.Add(lastToken);
                    }
                }
                if(Semanting)
                    _semanter.EOE(lastToken.LineNumber);

                //add to symbol table
                if (Syntaxing)
                {
                    var symId = GenerateSymId("Variable");
                    var scope = GetScopeString();
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
                            Scope = scope,
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
                            Vars = 0,
                            Data = new Data()
                            {
                                Type = type,
                                AccessMod = modifier,
                                Params = paramList
                            }
                        });
                }

                if (Semanting)
                {
                    //add label
                    var symId = _semanter.FindSymId("method", GetScopeString(), name);
                    _semanter.AddMethodLabel(symId);
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
            if (DEBUGMETA) Console.WriteLine("--Constructor Declaration");

            var name = ClassName();

            if (Semanting)
                _semanter.CD(name, GetScopeString(),lastToken.LineNumber);

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

            //add to symbol table
            if (Syntaxing)
            {
                var symId = GenerateSymId("Constructor");
                _syntaxSymbolTable.Add(symId,
                    new Symbol()
                    {
                        Data = new Data {Params = paramList,Type = name},
                        Kind = "Constructor",
                        Scope = GetScopeString(),
                        SymId = symId,
                        Value = name,
                        Vars = 0
                    });
            }

            if (Semanting)
            {
                //add label
                var symId = _semanter.FindSymId("Constructor", GetScopeString(), name);
                _semanter.AddMethodLabel(symId);
            }

            //go into method's scope
            _scope.Add(name);

            MethodBody();

            //leave
            outScope(name);

            //add ending return statement
            AddImpliedReturnStatement();
        }

        private bool AssignmentExpression()
        {
            if (DEBUGMETA) Console.WriteLine("--Assignment Expression");

            if (Accept(TokenType.This))
            {
                if (Semanting)
                {
                    _semanter.iPush(GetScopeString(), lastToken.Value,lastToken.LineNumber);
                    _semanter.iExist(GetScopeString(),lastToken.LineNumber);
                }
                return true;
            }

            if (Accept(TokenType.New))
            {
                Expect(TokenType.Type);
                if (Semanting)
                    _semanter.tPush(lastToken.Value, GetScopeString());
                NewDeclaration();
                
                return true;
            }

            if (Accept(TokenType.Atoi))
            {
                Expect(TokenType.ParenBegin);
                if(Semanting)
                    _semanter.oPush(Semanter.Operator.ParenBegin,lastToken.LineNumber);
                Expression();
                Expect(TokenType.ParenEnd);
                if (Semanting)
                {
                    _semanter.parenEnd(lastToken.LineNumber);
                    _semanter.checkAtoi(lastToken.LineNumber);
                }
                return true;
            }

            if (Accept(TokenType.Itoa))
            {
                Expect(TokenType.ParenBegin);
                if (Semanting)
                    _semanter.oPush(Semanter.Operator.ParenBegin, lastToken.LineNumber);
                Expression();
                Expect(TokenType.ParenEnd);
                if (Semanting)
                {
                    _semanter.parenEnd(lastToken.LineNumber);
                    _semanter.checkItoa(lastToken.LineNumber);
                }
                return true;
            }

            return Expression();
        }

        private void ParameterList(List<string> paramList = null)
        {
            if (DEBUGMETA) Console.WriteLine("--Parameter List");

            Parameter(paramList);
            while (Accept(TokenType.Comma))
                Parameter(paramList);
        }

        private void Parameter(List<string> paramList)
        {
            var type = GetToken().Value;
            Expect(TokenType.Type);

            if (Semanting)
            {
                _semanter.tPush(lastToken.Value, GetScopeString());
                _semanter.tExist(lastToken.LineNumber);
            }

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
                var scope = GetScopeString();

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
                        Scope = scope,
                        SymId = symId,
                        Value = name
                    });
                if (paramList != null)
                    paramList.Add(symId);
            }
        }

        private void NewDeclaration()
        {
            if (DEBUGMETA) Console.WriteLine("--New Declaration");

            if (Accept(TokenType.ArrayBegin))
            {
                if(Semanting)
                    _semanter.oPush(Semanter.Operator.ArrayBegin,lastToken.LineNumber);
                Expression();
                if (Semanting)
                {
                    _semanter.arrayEnd(lastToken.LineNumber);
                    _semanter.newArray(lastToken.LineNumber,true);
                }
                Expect(TokenType.ArrayEnd);
            }
            else
            {
                Expect(TokenType.ParenBegin);

                if (Semanting)
                {
                    _semanter.oPush(Semanter.Operator.ParenBegin, lastToken.LineNumber);
                    _semanter.BAL();
                }

                if (PeekExpression.Contains(GetToken().Type))
                    ArgumentList();

                Expect(TokenType.ParenEnd);

                if (Semanting)
                {
                    _semanter.parenEnd(lastToken.LineNumber);
                    _semanter.EAL();
                    _semanter.newObj(lastToken.LineNumber);
                }
            }
        }

        private void ArgumentList()
        {
            if (DEBUGMETA) Console.WriteLine("--Argument List");

            var token = GetToken();

            if (!Expression())
                throw new Exception(string.Format("Invalid argument at line {0}", token.LineNumber));

            token = GetToken();
            while (Accept(TokenType.Comma))
            {
                if (Semanting)
                    _semanter.commaPop(token.LineNumber);
                if (!Expression())
                    throw new Exception(string.Format("Invalid argument at line {0}", token.LineNumber));
            }
        }

        private bool Expression()
        {
            if (DEBUGMETA) Console.WriteLine("--Expression");

            //put expects in a try catch, need to return true true/false here
            try
            {
                if (Accept(TokenType.ParenBegin))
                {
                    if (Semanting)
                        _semanter.oPush(Semanter.Operator.ParenBegin, lastToken.LineNumber);
                    if (!Expression())
                        return false;
                    Expect(TokenType.ParenEnd);
                    if (Semanting)
                        _semanter.parenEnd(lastToken.LineNumber);
                }
                else if (Accept(TokenType.Character))
                {
                    if (Syntaxing)
                    {
                        var symId = GenerateSymId("Literal");
                        var symbol = new Symbol()
                        {
                            Data = new Data()
                            {
                                Type = TokenData.Get()[lastToken.Type].Name,
                                AccessMod = "unprotected",
                                IsArray = false
                            },
                            Kind = "literal",
                            Scope = GetScopeString(),
                            SymId = symId,
                            Value = lastToken.Value
                        };
                        _syntaxSymbolTable.Add(symId, symbol);
                    }
                    if (Semanting)
                    {
                        var symId = _semanter.FindSymId("literal", GetScopeString(), lastToken.Value);
                        _semanter.lPush(lastToken.Type,_syntaxSymbolTable[symId]);
                    }
                        
                }
                else if (Accept(TokenType.Identifier))
                {
                    if(Semanting)
                        _semanter.iPush(GetScopeString(), lastToken.Value, lastToken.LineNumber);
                    if (Peek(TokenType.ParenBegin) || Peek(TokenType.ArrayBegin))
                        FnArrMember();
                    if (Semanting)
                        _semanter.iExist(GetScopeString(),lastToken.LineNumber);
                    if (Peek(TokenType.Period))
                        MemberRefz();
                }
                else if (Accept(TokenType.True) ||
                         Accept(TokenType.False) ||
                         Accept(TokenType.Null) ||
                         Accept(TokenType.Number))
                {
                    if (Syntaxing)
                    {
                        var symId = GenerateSymId("Literal");
                        var symbol = new Symbol()
                        {
                            Data = new Data()
                            {
                                Type = TokenData.Get()[lastToken.Type].Name,
                                AccessMod = "unprotected",
                                IsArray = false
                            },
                            Kind = "literal",
                            Scope = GetScopeString(),
                            SymId = symId,
                            Value = lastToken.Value
                        };
                        _syntaxSymbolTable.Add(symId, symbol);
                    }
                    if (Semanting)
                    {
                        var symId = _semanter.FindSymId("literal", GetScopeString(), lastToken.Value);
                        _semanter.lPush(lastToken.Type, _syntaxSymbolTable[symId]);
                    }
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
            if (DEBUGMETA) Console.WriteLine("--Fn Arr Member");

            if (Accept(TokenType.ArrayBegin))
            {
                if (Semanting)
                    _semanter.oPush(Semanter.Operator.ArrayBegin, lastToken.LineNumber);
                Expression();
                Expect(TokenType.ArrayEnd);
                if (Semanting)
                {
                    _semanter.arrayEnd(lastToken.LineNumber);
                    _semanter.newArray(lastToken.LineNumber,false);
                }
            }
            else
            {
                Expect(TokenType.ParenBegin);

                if (Semanting)
                {
                    _semanter.oPush(Semanter.Operator.ParenBegin, lastToken.LineNumber);
                    _semanter.BAL();
                }

                if (PeekExpression.Contains(GetToken().Type))
                    ArgumentList();

                Expect(TokenType.ParenEnd);

                if (Semanting)
                {
                    _semanter.parenEnd(lastToken.LineNumber);
                    _semanter.EAL();
                    _semanter.func();
                    if (!Peek(TokenType.Semicolon))
                        _semanter.funcPeek();
                }
            }
        }

        private void MemberRefz()
        {
            if (DEBUGMETA) Console.WriteLine("--Member Refz");

            Expect(TokenType.Period);
            Expect(TokenType.Identifier);
            if (Semanting)
                _semanter.iPush(GetScopeString(), lastToken.Value, lastToken.LineNumber);
            
            if(Peek(TokenType.ParenBegin) || Peek(TokenType.ArrayBegin))
                FnArrMember();
            if (Semanting)
                _semanter.rExist(lastToken.LineNumber);
            if (Peek(TokenType.Period))
                MemberRefz();
        }

        private bool ExpressionZ()
        {
            if (DEBUGMETA) Console.WriteLine("--Expression Z");

            if (Accept(TokenType.Assignment))
            {
                if (Semanting)
                {
                    _semanter.checkArrayIndexAssignment();
                    _semanter.oPush(Semanter.Operator.Assignment, lastToken.LineNumber);
                }
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
                        case TokenType.More:
                            _semanter.oPush(Semanter.Operator.More, lastToken.LineNumber);
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
            if (DEBUGMETA) Console.WriteLine("--Method Body");

            Expect(TokenType.BlockBegin);

            if (Syntaxing)
            {
                if (_insertTokens != null && _insertTokens.Count > 0 && !ConstructorCreated && _scope.Count()>=3 && _scope[1].Equals(_scope[2]))
                {
                    foreach(var r in _insertTokens)
                        _recordTokens.Add(r);
                    _insertTokens.Clear();
                    ConstructorCreated = true;

                    //update literals in symtable
                    foreach (var s in _syntaxSymbolTable.Where(s => s.Value.Kind.Equals("literal") && s.Value.Scope.Equals(_scope[0] + "." + _scope[1])))
                        s.Value.Scope += "." + _scope[1];
                }
            }

            while (Peek(TokenType.Type) && Peek(TokenType.Identifier,2))
                VariableDeclaration();
            while (PeekStatement.Contains(GetToken().Type) || PeekExpression.Contains(GetToken().Type))
                Statement();
            Expect(TokenType.BlockEnd);
        }

        private void VariableDeclaration()
        {
            if (DEBUGMETA) Console.WriteLine("--Variable Declaration");

            var type = GetToken().Value;
            Expect(TokenType.Type);

            if (Semanting)
            {
                _semanter.tPush(lastToken.Value, GetScopeString());
                _semanter.tExist(lastToken.LineNumber);
            }

            var name = GetToken().Value;
            Expect(TokenType.Identifier);

            var isArray = false;
            if (Accept(TokenType.ArrayBegin))
            {
                Expect(TokenType.ArrayEnd);
                isArray = true;
            }

            if (Semanting)
                _semanter.vPush(GetScopeString(), name,isArray,false);

            if (Accept(TokenType.Assignment))
            {
                if(Semanting)
                    _semanter.oPush(Semanter.Operator.Assignment, lastToken.LineNumber);

                AssignmentExpression();

                if (Semanting)
                    _semanter.EOE(lastToken.LineNumber);
            }
            Expect(TokenType.Semicolon);

            if (Syntaxing)
            {
                var symId = GenerateSymId("Local Variable");
                var scope = GetScopeString();
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
                        Scope = scope,
                        SymId = symId,
                        Value = name
                    });
            }
        }

        private void Statement()
        {
            if (DEBUGMETA) Console.WriteLine("--Statement");

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
                if(Semanting)
                    _semanter.oPush(Semanter.Operator.ParenBegin,lastToken.LineNumber);
                Expression();
                Expect(TokenType.ParenEnd);
                if (Semanting)
                {
                    _semanter.parenEnd(lastToken.LineNumber);
                    _semanter.checkIf(lastToken.LineNumber);
                }
                Statement();
                if (Accept(TokenType.Else))
                {
                    if (Semanting)
                    {
                        _semanter.writeSkipIf(true);
                    }

                    Statement();

                    if (Semanting)
                    {
                        _semanter.writeElse();
                    }
                }
                else //this is ironic
                {
                    if (Semanting)
                    {
                        _semanter.writeSkipIf(false);
                    }
                }
                return;
            }
            if (Accept(TokenType.While))
            {
                Expect(TokenType.ParenBegin);
                if (Semanting)
                {
                    _semanter.oPush(Semanter.Operator.ParenBegin, lastToken.LineNumber);
                    _semanter.beginWhile();
                }
                Expression();
                Expect(TokenType.ParenEnd);
                if (Semanting)
                {
                    _semanter.parenEnd(lastToken.LineNumber);
                    _semanter.checkWhile(lastToken.LineNumber);
                }
                Statement();
                if (Semanting)
                {
                    _semanter.endWhile();
                }
                return;
            }
            if (Accept(TokenType.Return))
            {
                if (Accept(TokenType.Semicolon))
                {
                    if (Semanting)
                        _semanter.checkReturn(GetScopeString(), lastToken.LineNumber, false);
                }
                else
                {
                    Expression();
                    Expect(TokenType.Semicolon);
                    if (Semanting)
                        _semanter.checkReturn(GetScopeString(), lastToken.LineNumber,true);
                }
                
                return;
            }
            if (Accept(TokenType.Cout))
            {
                Expect(TokenType.Extraction);
                Expression();
                Expect(TokenType.Semicolon);
                if (Semanting)
                    _semanter.checkCout(lastToken.LineNumber);
                return;
            }
            if (Accept(TokenType.Cin))
            {
                Expect(TokenType.Insertion);
                //Expression();

                //this makes more sense to me, because reading into an expression makes no sense
                Expect(TokenType.Identifier);

                if (Semanting)
                    _semanter.iPush(GetScopeString(), lastToken.Value, lastToken.LineNumber);
                if (Peek(TokenType.ParenBegin) || Peek(TokenType.ArrayBegin))
                    FnArrMember();
                if (Semanting)
                    _semanter.iExist(GetScopeString(), lastToken.LineNumber);
                if (Peek(TokenType.Period))
                    MemberRefz();
                if (Semanting)
                    _semanter.checkCin(lastToken.LineNumber);

                Expect(TokenType.Semicolon);
                
                return;
            }
            if (Accept(TokenType.Spawn))
            {
                Expression();
                Expect(TokenType.Set);
                Expect(TokenType.Identifier);
                if (Semanting)
                {
                    _semanter.iPush(GetScopeString(), lastToken.Value, lastToken.LineNumber);
                    _semanter.iExist(GetScopeString(),lastToken.LineNumber);
                    _semanter.checkSpawn(GetScopeString(), lastToken.LineNumber);
                }
                Expect(TokenType.Semicolon);
                return;
            }
            if (Accept(TokenType.Block))
            {
                Expect(TokenType.Semicolon);
                if (Semanting)
                    _semanter.checkBlock(lastToken.LineNumber);
                return;
            }
            if (Accept(TokenType.Lock))
            {
                Expect(TokenType.Identifier);
                if (Semanting)
                    _semanter.iPush(GetScopeString(), lastToken.Value, lastToken.LineNumber);
                Expect(TokenType.Semicolon);
                if (Semanting)
                    _semanter.checkLock(lastToken.LineNumber);
                return;
            }
            if (Accept(TokenType.Release))
            {
                Expect(TokenType.Identifier);
                if (Semanting)
                    _semanter.iPush(GetScopeString(), lastToken.Value, lastToken.LineNumber);
                Expect(TokenType.Semicolon);
                if (Semanting)
                    _semanter.checkRelease(lastToken.LineNumber);
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
            if(DEBUGTOKENS)
            if (GetToken().LineNumber == -1)//when you're stepping through code, this'll take you straight to where you want to go
                Console.WriteLine("Arrived");
        }
    }

}

