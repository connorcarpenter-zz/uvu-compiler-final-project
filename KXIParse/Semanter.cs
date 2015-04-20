using System;
using System.Collections.Generic;
using System.Linq;

namespace KXIParse
{
    class Semanter
    {
        private const bool DEBUG = false;
        private static Dictionary<string, Symbol> _symbolTable;
        private static Stack<Operator> _operatorStack;
        private static Intercoder _intercoder; 

        public enum Operator
        {
            And,Or,Equals,NotEquals,LessOrEqual,MoreOrEqual,
            Less,More,Add,Subtract,Multiply,Divide,Assignment,
            ParenBegin,
            ArrayBegin,
            Comma
        }

        private static readonly Dictionary<Operator, int> OpPriority = new Dictionary<Operator, int> //this is golf rules here boys
            {
                { Operator.Multiply, 4 }, { Operator.Divide, 4 },
                { Operator.Add, 3 }, { Operator.Subtract, 3 },
                { Operator.Less, 2 }, { Operator.More, 2 },{ Operator.LessOrEqual, 2 }, { Operator.MoreOrEqual, 2 },{ Operator.Equals, 2 }, { Operator.NotEquals, 2 },
                { Operator.And, 1 }, { Operator.Or, 1 },
                { Operator.ParenBegin, 0 }, { Operator.ArrayBegin, 0 } , { Operator.Assignment, -1 } , {Operator.Comma,0}
            };
        private static Stack<Record> _recordStack;
        public enum RecordType
        {
            Identifier,Temporary,BAL,EAL,Func,Type,
            IVar,LVar,
            New,
            Literal,
            NewArray,
            Reference
        }

        public List<string> VariableTypes = new List<string> { "int", "char", "bool", "sym","void" };

        internal class Record
        {
            public RecordType Type { get; set; }
            public string Value { get; set; }
            public Symbol LinkedSymbol { get; set; }
            public Stack<Record> ArgumentList { get; set; }
            public object TempVariable { get; set; }//apparently this will be needed in icode for ref and id records

            public Record(RecordType type, string value,Symbol symbol)
            {
                Type = type;
                Value = value;
                LinkedSymbol = symbol;
            }

            public Record(Record other)
            {
                this.ArgumentList = other.ArgumentList;
                this.LinkedSymbol = other.LinkedSymbol;
                this.TempVariable = other.TempVariable;
                this.Type = other.Type;
                this.Value = other.Value;
            }
        }

        public Semanter(Dictionary<string, Symbol> st,List<Quad> iCodeList)
        {
            _operatorStack = new Stack<Operator>();
            _recordStack = new Stack<Record>();
            _symbolTable = st;
            _intercoder = new Intercoder(iCodeList);
        }

        public void EOE(int lineNumber) //end of expression
        {
            EvalOp("EOE",lineNumber);
            if(DEBUG)Console.WriteLine("   EOE");
        }

        public void BAL() //beginning of array push
        {
            _recordStack.Push(new Record(RecordType.BAL,null,null));
            if (DEBUG) Console.WriteLine("   BAL");
        }

        public void EAL() //ending of array pop
        {
            var endOfArgumentListRecord = new Record(RecordType.EAL, null, null);
            endOfArgumentListRecord.ArgumentList = new Stack<Record>();
            while (true)
            {
                var record = _recordStack.Pop();
                if (record.Type == RecordType.BAL)
                    break;
                endOfArgumentListRecord.ArgumentList.Push(record);
            }
            _recordStack.Push(endOfArgumentListRecord);
            if (DEBUG) Console.WriteLine("   EAL");
        }

        public void commaPop(int lineNumber) //comma pop
        {
            EvalOp("commaPop",lineNumber);
            if (DEBUG) Console.WriteLine("   #,");
        }

        public void newArray(int lineNumber,bool arrayInit) //new array, check that operator is an int
        {
            var index = _recordStack.Pop();
            if(!GetCompareString(index).Equals("int"))
                throw new Exception(string.Format("Semantic Error at line {0}: Cannot use a {1} as an array indexer, need an int",
                    lineNumber, index.LinkedSymbol.Data.Type));
            var typeSar = _recordStack.Pop();
            //supposed to test that an array of the type in typesar can be created... but I'm pretty sure any data type can be arrayed, so I'm skipping this til later
            var newSar = new Record(typeSar)
            {
                Type = RecordType.NewArray,
                TempVariable = _intercoder.GetTempVarName(typeSar)
            };
            

            if (arrayInit)
            {
                newSar.LinkedSymbol = GetSizeRecord(newSar);
                _recordStack.Push(newSar);
                _intercoder.WriteNewArray(index, typeSar, newSar);
            }
            else
            {
                //newSar.LinkedSymbol = GetSizeRecord(newSar);
                newSar.LinkedSymbol.Data.IsArray = false;
                _recordStack.Push(newSar);
                _intercoder.WriteArray(index, typeSar, newSar);
            }

            if (DEBUG) Console.WriteLine("   newArray");
        }

        private Symbol GetSizeRecord(Record newSar)
        {
            //////make the sizes in here constants man!!!!
            if (newSar.Value.Equals("int"))
                return new Symbol {Data = new Data {Size = 4}};
            if (newSar.Value.Equals("char"))
                return new Symbol { Data = new Data { Size = 1 } };
            if (newSar.Value.Equals("bool"))
                return new Symbol { Data = new Data { Size = 1 } };
            var symbol = _symbolTable.FirstOrDefault(s => s.Value.Kind == "Class" && s.Value.Value == newSar.Value);
            return symbol.Value;
        }

        public void checkArrayIndexAssignment()
        {
            var id = _recordStack.Pop();
            if (id.LinkedSymbol.Data.IsArray)
                id.LinkedSymbol.Data.IsArray = false;
            _recordStack.Push(id);
        }

        public void parenEnd(int lineNumber) //go through the operator stack until you pop parenBegin
        {
            while (true)
            {
                var nextOp = _operatorStack.Peek();
                if (nextOp == Operator.ParenBegin)
                {
                    _operatorStack.Pop();
                    break;
                }
                EvalOp("parenEnd",lineNumber);
            }
            if (DEBUG) Console.WriteLine("   #)");
        }

        public void arrayEnd(int lineNumber)
        {
            while (true)
            {
                var nextOp = _operatorStack.Peek();
                if (nextOp == Operator.ArrayBegin)
                {
                    _operatorStack.Pop();
                    break;
                }
                EvalOp("arrayEnd",lineNumber);
            }


            if (DEBUG) Console.WriteLine("   #]");
        }

        public void func() //function
        {
            var argumentList = _recordStack.Pop();
            var functionName = _recordStack.Pop();
            var peekRecord = (_recordStack.Count > 0) ? _recordStack.Peek() : null;
            
            var newRecord = new Record(functionName)
            {
                Type = RecordType.Func,
                ArgumentList = argumentList.ArgumentList
            };


            if (newRecord.LinkedSymbol == null || newRecord.LinkedSymbol.SymId == null ||
                newRecord.LinkedSymbol.SymId.Length == 0)
            {
                var symbol = (from s in _symbolTable
                              where s.Value.Scope == "g." + peekRecord.LinkedSymbol.Data.Type &&
                                  s.Value.Value == functionName.Value &&
                                  s.Value.Data.AccessMod.Equals("unprotected") &&
                                  s.Value.Kind.Equals("method")
                              select s.Value).FirstOrDefault();
                newRecord.LinkedSymbol = symbol;
            }
            _intercoder.WriteFunctionCall(newRecord, argumentList, peekRecord, functionName,true);

            _recordStack.Push(newRecord);

            if (DEBUG) Console.WriteLine("   Func: " + functionName.Value);
        }

        public void funcPeek()
        {
            var rec = _recordStack.Peek();
            _intercoder.WriteFunctionPeek(rec);
        }

        public void checkSpawn(string scope, int lineNumber)
        {
            var sar = _recordStack.Pop();//supposed to test whether this exists in the current scope, but i kinda already did this with the previous iExist so...
            var refSar = _recordStack.Pop();//still not quite sure what this is supposed to do...
            if (DEBUG) Console.WriteLine("   checkSpawn: " + refSar.Value);
        }

        public void checkIf(int lineNumber)
        {
            var expression_sar = _recordStack.Pop();
            if(!GetCompareString(expression_sar).Equals("bool"))
                throw new Exception(string.Format("Semantic error at line {0}: 'If' expression does not evaluate to a boolean", lineNumber));

            _intercoder.WriteIf(expression_sar);

            if (DEBUG) Console.WriteLine("   checkIf");
        }

        public void writeSkipIf(bool isElse)
        {
            _intercoder.WriteSkipIf(isElse);
        }

        public void writeElse()
        {
            _intercoder.WriteElse();
        }

        public void beginWhile()
        {
            _intercoder.WriteBeginWhile();
        }

        public void checkWhile(int lineNumber)
        {
            var expression_sar = _recordStack.Pop();
            if (!GetCompareString(expression_sar).Equals("bool"))
                throw new Exception(string.Format("Semantic error at line {0}: 'While' expression does not evaluate to a boolean", lineNumber));

            _intercoder.WriteMiddleWhile(expression_sar);

            if (DEBUG) Console.WriteLine("   checkWhile");
        }

        public void endWhile()
        {
            _intercoder.WriteEndWhile();
        }

        public void CD(string name,string scope, int lineNumber)
        {
            var scopes = scope.Split('.');
            if(name!=scopes[scopes.Length-1])
                throw new Exception(string.Format("Semantic error at line {0}: Constructor name '{1}' does not match class name of scope '{2}'",lineNumber,name,scopes[scopes.Length-1]));
            if (DEBUG) Console.WriteLine("   CD: {0} in {1}", name, scope);
        }
        
        public void checkAtoi(int lineNumber)
        {
            var expression_sar = _recordStack.Pop();
            if (!GetCompareString(expression_sar).Equals("char"))
                throw new Exception(string.Format("Semantic error at line {0}: 'Atoi' expression does not evaluate to char", lineNumber));
            expression_sar.LinkedSymbol.Data.Type = "int";
            _recordStack.Push(expression_sar);
            if (DEBUG) Console.WriteLine("   checkAtoi");
        }

        public void checkItoa(int lineNumber)
        {
            var expression_sar = _recordStack.Pop();
            if (!GetCompareString(expression_sar).Equals("int"))
                throw new Exception(string.Format("Semantic error at line {0}: 'Itoa' expression does not evaluate to int", lineNumber));
            expression_sar.LinkedSymbol.Data.Type = "char";
            _recordStack.Push(expression_sar);
            if (DEBUG) Console.WriteLine("   checkItoa");
        }

        public void checkBlock(int lineNumber)
        {
            //check that the block is on the main thread
            if (DEBUG) Console.WriteLine("   block");
        }

        //These are the ones left to code:
        public void checkCin(int lineNumber)
        {
            EvalOp("checkCin",lineNumber);

            var expression_sar = _recordStack.Pop();
            var compare_str = GetCompareString(expression_sar);
            if (!compare_str.Equals("int") && !compare_str.Equals("char"))
                throw new Exception(string.Format("Semantic error at line {0}: Variable cannot get input from Cin", lineNumber));

            _intercoder.WriteCin(expression_sar);

            if (DEBUG) Console.WriteLine("   checkCin");
        }

        public void checkCout(int lineNumber)
        {
            EvalOp("checkCout",lineNumber);

            var expression_sar = _recordStack.Pop();
            var compare_str = GetCompareString(expression_sar);
            if (!compare_str.Equals("int") && !compare_str.Equals("char"))
                throw new Exception(string.Format("Semantic error at line {0}: Variable cannot be outputted to Cout", lineNumber));

            _intercoder.WriteCout(expression_sar);

            if (DEBUG) Console.WriteLine("   checkCout");
        }
        
        public void checkLock(int lineNumber)
        {
            var expression_sar = _recordStack.Pop();
            var compare_str = GetCompareString(expression_sar);
            if (!compare_str.Equals("sym"))
                throw new Exception(string.Format("Semantic error at line {0}: Lock variable needs to be of type sym, but is of type '{1}'", lineNumber,compare_str));
            if (DEBUG) Console.WriteLine("   checkLock");
        }

        public void checkRelease(int lineNumber)
        {
            var expression_sar = _recordStack.Pop();
            var compare_str = GetCompareString(expression_sar);
            if (!compare_str.Equals("sym"))
                throw new Exception(string.Format("Semantic error at line {0}: Release variable needs to be of type sym, but is of type '{1}'", lineNumber, compare_str));
            if (DEBUG) Console.WriteLine("   checkRelease");
        }

        public void checkReturn(string scope,int lineNumber,bool hasValue,bool returnThis)
        {
            var scopes = scope.Split('.');
            var methodName = scopes[scopes.Length - 1];
            Record expressionSar = null;
            string expCompare;
            string methodCompare;
            string methodType;
            if (hasValue)
            {
                EvalOp("checkReturn", lineNumber);
                expressionSar = _recordStack.Pop();
                expCompare = (methodName.Equals("main") && scope.Equals("g.main")) ? "void" : GetCompareString(expressionSar);
                var methodScope = scope.Remove(scope.Length - methodName.Length - 1, methodName.Length + 1);
                var method =
                    _symbolTable.Where(s => s.Value.Scope == methodScope && s.Value.Kind == "method" && s.Value.Value == methodName)
                        .Select(s => s.Value)
                        .FirstOrDefault();
                if (method == null)
                    throw new Exception(
                        string.Format("Semantic error at line {0}: Cannot find method defined for this scope",
                            lineNumber));
                methodCompare = GetCompareString(new Record(RecordType.Identifier, "", method));
                methodType = method.Data.Type;
            }
            else
            {
                expCompare = "void";
                methodCompare = "void";
                methodType = "void";
            }

            if (!expCompare.Equals(methodCompare))
                throw new Exception(string.Format("Semantic error at line {0}: Trying to return a value of type '{1}' from method '{2}' which is set to return value of type '{3}'",
                    lineNumber, expCompare, methodName, methodType));

            _intercoder.WriteReturn(methodType,expressionSar,returnThis);

            if (DEBUG) Console.WriteLine("   checkReturn");
        }

        public void iPush(string scope,string iname,int lineNumber) //identifier push
        {
            var record = new Record(RecordType.Identifier, iname, new Symbol(){Scope=scope});
            var symbol = GetSymbol(scope, record);
            if (symbol != null)
                record.LinkedSymbol = symbol;
            _recordStack.Push(record);
            if (DEBUG) Console.WriteLine("   iPush: " + iname);
        }

        public void iExist(string scope,int lineNumber) //identifier exists
        {
            var identifier = _recordStack.Pop();

            //figure out if this is in a method

            var symbol = GetSymbol(scope, identifier);
            if (symbol!=null)
            {
                var newSar = new Record(identifier)
                {
                    LinkedSymbol = symbol,
                    Type = RecordType.Identifier
                };
                if (identifier.Type == RecordType.NewArray)
                {
                    //what to do here?
                    newSar.LinkedSymbol.Data.IsArray = false;
                    newSar.Value = newSar.TempVariable.ToString();
                    //this might cause problems, not fully tested
                }
                _recordStack.Push(newSar);
            }
            else
            {
                throw new Exception(string.Format("Semantic error at line {0}: Identifier {1} does not exist", lineNumber,identifier.Value));
            }
            if (DEBUG) Console.WriteLine("   iExist: " + identifier.Value);
        }

        private Symbol GetSymbol(string scope,Record identifier)
        {
            var methodName = scope.Split('.').Last();
            var methodScope = methodName.Equals("g") ? "g" : scope.Remove(scope.Length - methodName.Length - 1, methodName.Length + 1);
            var inMethod = _symbolTable.Any(
                s => (s.Value.Kind == "method" || s.Value.Kind == "Constructor") && s.Value.Value == methodName && s.Value.Scope == methodScope);

            var symbol = (from s in _symbolTable where s.Value.Scope == scope && s.Value.Value == identifier.Value && (s.Value.Kind == "lvar" || s.Value.Kind == "param" || s.Value.Kind == "Class") select s.Value).FirstOrDefault();
            if (symbol == null && inMethod)
                symbol = (from s in _symbolTable where s.Value.Scope == methodScope && s.Value.Value == identifier.Value && (s.Value.Kind == "ivar" || s.Value.Kind == "method" || s.Value.Kind == "Constructor") select s.Value).FirstOrDefault();

            return symbol;
        }

        public void rExist(int lineNumber) //member reference identifier exists
        {
            var childId = _recordStack.Pop();
            var parentId = _recordStack.Pop();
            var symbol = (from s in _symbolTable where s.Value.Scope == "g."+parentId.LinkedSymbol.Data.Type &&
                              s.Value.Value == childId.Value &&
                              s.Value.Data.AccessMod.Equals("unprotected")
                          select s.Value).FirstOrDefault();
            if (symbol != null)
            {
                var newRecord = new Record(childId)
                {
                    Type = RecordType.Reference,
                    LinkedSymbol = symbol,
                    TempVariable = _intercoder.GetTempVarName(childId)
                };
                _recordStack.Push(newRecord);

                if (childId.Type == RecordType.Func)
                {
                    //_intercoder.WriteFunctionCall((_recordStack.Count > 0) ? _recordStack.Peek() : null, newRecord,childId,parentId);
                }
                else
                {
                    _intercoder.WriteReference(parentId, childId, newRecord);
                }
            }
            else
            {
                throw new Exception(string.Format("Semantic error at line {0}: Identifier {2}.{1} does not exist", lineNumber, childId.Value, parentId.Value));
            }
            if (DEBUG) Console.WriteLine("   rExist");
        }

        public void oPush(Operator o,int lineNumber) //operator push
        {
            if (OpPriority[o]!=0)
            if (_operatorStack.Count > 0)
            {
                var nextOp = _operatorStack.Peek();
                if (OpPriority.ContainsKey(o) && OpPriority.ContainsKey(nextOp))
                    if (OpPriority[o] < OpPriority[_operatorStack.Peek()])
                        EvalOp("oPush",lineNumber);
            }
            _operatorStack.Push(o);
            if (DEBUG) Console.WriteLine("   oPush: " + o.ToString());
        }

        public void newObj(int lineNumber)
        {
            var alSar = _recordStack.Pop();
            var typeSar = _recordStack.Pop();

            Symbol sym;
            if (_symbolTable.Any(s => s.Value.Kind == "Constructor" && s.Value.Value == typeSar.Value))
                sym = _symbolTable.First(s => s.Value.Kind == "Constructor" && s.Value.Value == typeSar.Value).Value;
            else
                throw new Exception(string.Format("Semantic error at line {0}: No constructor exists for class '{1}')",
                    lineNumber, typeSar.Value));

            var constructorScope = "g." + typeSar.Value + "." + typeSar.Value;
            var newSar = new Record(typeSar)
            {
                ArgumentList = new Stack<Record>(),
                LinkedSymbol = sym,
                Type = RecordType.New,
                TempVariable = _intercoder.GetTempVarName(typeSar)
            };
            foreach (var a in sym.Data.Params)
            {
                var argRecord = alSar.ArgumentList.Pop();
                var argName = argRecord.Value;
                var symbol =
                    (from p in _symbolTable
                        where p.Value.Scope == constructorScope && p.Key == a
                        select p.Value).FirstOrDefault();
                if (symbol == null || symbol.Data.Type != _symbolTable[a].Data.Type)
                    throw new Exception(
                        string.Format(
                            "Semantic error at line {0}: Constructor for class '{1}' does not have a param '{2}' of type '{3}', expected a value of type '{4}' instead",
                            lineNumber, typeSar.Value, argName, symbol.Data.Type ?? "null", _symbolTable[a].Data.Type));
                newSar.ArgumentList.Push(argRecord);
            }
            _recordStack.Push(newSar);

            var classSymbol = GetSymbol("g", typeSar);
            if (classSymbol != null)
                typeSar.LinkedSymbol = classSymbol;
            _intercoder.WriteNewObj(newSar,typeSar);

            if(DEBUG)Console.WriteLine("   newObj: " + typeSar.Value);
        }

        public void vPush(string scope,string vName,bool isArray,bool isField)
        {
            var sym = (from s in _symbolTable where s.Value.Scope == scope && s.Value.Value == vName select s.Value).FirstOrDefault();
            if(sym==null)throw new Exception("Semantic Error: write some better error text here, but this vpush should be getting an associated symbol");
            if(isArray != !(sym.Data==null || sym.Data.IsArray==false))
                throw new Exception("Semantic Error: loaded symbol and scanned symbol do not match, array-wise....");
            _recordStack.Push(new Record(isField ? RecordType.IVar : RecordType.LVar, vName, sym));
            if (DEBUG) Console.WriteLine("   vPush: " + vName);
        }

        public void lPush(TokenType type, Symbol symbol)
        {
            var name = TokenData.Get()[type].Name;
            var record = new Record(RecordType.Literal, name, symbol);
            _recordStack.Push(record);
            if (DEBUG) Console.WriteLine("   lPush: " + name);
        }

        public void tPush(string tname, string scope)
        {
            _recordStack.Push(new Record(RecordType.Type, tname,new Symbol(){Scope=scope}));
            if (DEBUG) Console.WriteLine("   tPush: " + tname);
        }

        public void tExist(int lineNumber)
        {
            var type = _recordStack.Pop();
            var done = false;
            if (VariableTypes.Contains(type.Value)) done = true;
            else if (_symbolTable.Any(s => s.Value.Kind == "Class" && s.Value.Value == type.Value))
                done = true;
            if (done)
            {
                if (DEBUG) Console.WriteLine("   tExist: " + type.Value);
            }
            else
                throw new Exception(string.Format("Semantic error at line {0}: Type {1} does not exist", lineNumber, type.Value));
        }

        private static void EvalOp(string actionName, int lineNumber)
        {
            while (_operatorStack.Count > 0)
            {
                var nextOp = _operatorStack.Peek();
                if (OpPriority[nextOp] == 0) break;
                if (nextOp == Operator.Assignment && actionName == "oPush") break;

                _operatorStack.Pop();

                if(nextOp!=Operator.Assignment && nextOp!=Operator.Less && nextOp!=Operator.More && nextOp!=Operator.LessOrEqual && 
                   nextOp!=Operator.MoreOrEqual && nextOp!=Operator.Equals && nextOp!=Operator.NotEquals && nextOp!=Operator.And && 
                   nextOp!=Operator.Or && nextOp!=Operator.Add && nextOp!=Operator.Subtract && nextOp!=Operator.Multiply && nextOp!=Operator.Divide)
                        throw new Exception(string.Format("Semantic error at line {0}: Trying to evaluate invalid operator",lineNumber));

                var i1 = _recordStack.Pop();
                var i2 = _recordStack.Pop();
                Record result = null;
                if (i1.Value.Equals("Null"))
                {
                    var i2type = i2.LinkedSymbol.Data.Type;
                    if (_symbolTable.Any(s => s.Value.Kind == "Class" && s.Value.Value.Equals(i2type)))
                    {
                        i1.Type = RecordType.Identifier;
                        i1.LinkedSymbol.Data.Type = i2type;
                    }
                }
                CheckRecordsAreSameType(GetCompareString(i1), GetCompareString(i2), lineNumber);

                if (nextOp != Operator.Assignment)
                {
                    var type = "bool";
                    if (nextOp == Operator.Add || nextOp == Operator.Subtract || nextOp == Operator.Multiply ||
                        nextOp == Operator.Divide)
                        type = "int";
                    result = new Record(
                            RecordType.Temporary,
                            _intercoder.GetTempVarName(i2),
                            new Symbol { Data = new Data { Type = type }, Scope = i1.LinkedSymbol.Scope  });
                    result.TempVariable = i1.Value + "." + i2.Value;
                     _recordStack.Push(result);
                }

                if (nextOp == Operator.Less || nextOp == Operator.More || nextOp == Operator.LessOrEqual || nextOp == Operator.MoreOrEqual || nextOp == Operator.Subtract|| nextOp == Operator.Divide)
                    _intercoder.WriteOperation(nextOp, i2, i1, result);//CONNORWAZHERE
                else
                    _intercoder.WriteOperation(nextOp, i1, i2, result);//CONNORWAZHERE
            }
        }

        private static void CheckRecordsAreSameType(string v1,string v2,int lineNumber)
        {
            if(!v1.Equals(v2))
                throw new Exception(string.Format("Semantic error at line {0}: Trying to perform operation between types '{1}' and '{2}'",
                                lineNumber,
                                v1,
                                v2));
        }

        private static string GetCompareString(Record r)
        {
            switch (r.Type)
            {
                case RecordType.New:
                case RecordType.LVar:
                case RecordType.IVar:
                case RecordType.Identifier:
                case RecordType.Reference:
                case RecordType.Temporary:
                case RecordType.Func:
                    return r.LinkedSymbol.Data.IsArray ? r.LinkedSymbol.Data.Type + "[]" : r.LinkedSymbol.Data.Type;
                case RecordType.Literal:
                    return ValueMap[r.Value];
                case RecordType.NewArray:
                    return r.Value + "[]";
                default:
                    return "";
            }
        }

        private static readonly Dictionary<string,string> ValueMap = new Dictionary<string,string>
        {
            {"Null","null"},
            {"Number","int"},
            {"Character","char"},
            {"Bool","bool"},
            {"True","bool"},
            {"False","bool"}
        };


        public void End()
        {
            //_intercoder.End();
        }

        public string FindSymId(string kind, string scope, string value)
        {
            foreach (var sym in _symbolTable.Where(sym => sym.Value.Kind == kind && sym.Value.Scope == scope && sym.Value.Value == value))
                return sym.Value.SymId;

            throw new Exception(string.Format("Semantic Error: Can't find symbol of kind: {0}, and value: {1} in symbol table",kind,value));
        }

        public void AddMethodLabel(string symId)
        {
            _intercoder.AddMethodLabel(symId);
        }
    }

}

