using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Runtime.CompilerServices;

namespace KXIParse
{
    class Semanter
    {
        private const bool DEBUG = true;
        private static Dictionary<string, Symbol> _symbolTable;
        private static Stack<Operator> _operatorStack;
        public enum Operator
        {
            And,Or,Equals,NotEquals,LessOrEqual,MoreOrEqual,
            Less,More,Add,Subtract,Multiply,Divide,Assignment,
            ParenBegin,
            ArrayBegin,
            Comma
        }

        private static Dictionary<Operator, int> _opPriority = new Dictionary<Operator, int> //this is golf rules here boys
            {
                { Operator.Multiply, 3 }, { Operator.Divide, 3 },
                { Operator.Add, 2 }, { Operator.Subtract, 2 },
                { Operator.Less, 1 }, { Operator.More, 1 },{ Operator.LessOrEqual, 1 }, { Operator.MoreOrEqual, 1 },
                { Operator.Equals, 1 }, { Operator.NotEquals, 1 },{ Operator.And, 1 }, { Operator.Or, 1 },
                { Operator.ParenBegin, 0 }, { Operator.ArrayBegin, 0 } , { Operator.Assignment, -1 } , {Operator.Comma,0}
            };
        private static Stack<Record> _recordStack;
        public enum RecordType
        {
            Identifier,Temporary,BAL,EAL,Func,Type,
            Variable,
            New,
            Literal,
            NewArray
        }

        public List<string> VariableTypes = new List<string> { "int", "char", "bool", "sym" };

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
        }

        public Semanter(Syntaxer s,Dictionary<string, Symbol> st)
        {
            _operatorStack = new Stack<Semanter.Operator>();
            _recordStack = new Stack<Record>();
            _symbolTable = st;
        }

        public void EOE(int lineNumber) //end of expression
        {
            evalOp(lineNumber);
            if(DEBUG)Console.WriteLine("EOE");
        }

        public void BAL() //beginning of array push
        {
            _recordStack.Push(new Record(RecordType.BAL,null,null));
            if (DEBUG) Console.WriteLine("BAL");
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
            if (DEBUG) Console.WriteLine("EAL");
        }

        public void commaPop(int lineNumber) //comma pop
        {
            evalOp(lineNumber);
            if (DEBUG) Console.WriteLine("#,");
        }

        public void newArray(int lineNumber) //new array, check that operator is an int
        {
            var index = _recordStack.Pop();
            if(!GetCompareString(index).Equals("int"))
                throw new Exception(string.Format("Semantic Error at line {0}: Cannot use a {1} as an array indexer, need an int",
                    lineNumber, index.LinkedSymbol.Data.Type));
            var typeSar = _recordStack.Pop();
            //supposed to test that an array of the type in typesar can be created... but I'm pretty sure any data type can be arrayed, so I'm skipping this til later
            typeSar.Type = RecordType.NewArray;
            _recordStack.Push(typeSar);
            if (DEBUG) Console.WriteLine("newArray");
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
                evalOp(lineNumber);
            }
            if (DEBUG) Console.WriteLine("#)");
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
                evalOp(lineNumber);
            }


            if (DEBUG) Console.WriteLine("#]");
        }

        public void func() //function
        {
            var argumentList = _recordStack.Pop();
            var functionName = _recordStack.Pop();
            functionName.Type = RecordType.Func;
            functionName.ArgumentList = argumentList.ArgumentList;
            _recordStack.Push(functionName);
            if (DEBUG) Console.WriteLine("Func: "+functionName.Value);
        }

        public void checkSpawn(string scope, int lineNumber)
        {
            var sar = _recordStack.Pop();//supposed to test whether this exists in the current scope, but i kinda already did this with the previous iExist so...
            var refSar = _recordStack.Pop();//still not quite sure what this is supposed to do...
            if (DEBUG) Console.WriteLine("checkSpawn: " + refSar.Value);
        }

        public void checkIf(int lineNumber)
        {
            var expression_sar = _recordStack.Pop();
            if(!GetCompareString(expression_sar).Equals("bool"))
                throw new Exception(string.Format("Semantic error at line {0}: 'If' expression does not evaluate to a boolean", lineNumber));
            if (DEBUG) Console.WriteLine("checkIf");
        }

        public void checkWhile(int lineNumber)
        {
            var expression_sar = _recordStack.Pop();
            if (!GetCompareString(expression_sar).Equals("bool"))
                throw new Exception(string.Format("Semantic error at line {0}: 'While' expression does not evaluate to a boolean", lineNumber));
            if (DEBUG) Console.WriteLine("checkWhile");
        }

        //These are the ones left to code:
        public void CD(string scope, int lineNumber)
        {
        }
        public void checkAtoi(int lineNumber)
        {
        }
        public void checkBlock(int lineNumber)
        {
        }
        public void checkCin(int lineNumber)
        {
        }
        public void checkCout(int lineNumber)
        {
        }
        public void checkItoa(int lineNumber)
        {
        }
        public void checkLock(int lineNumber)
        {
        }
        public void checkRelease(int lineNumber)
        {
        }
        public void checkReturn(int lineNumber)
        {
        }
        /////////////////////////////////////////

        public void iPush(string iname) //identifier push
        {
            _recordStack.Push(new Record(RecordType.Identifier, iname, null));
            if (DEBUG) Console.WriteLine("iPush: " + iname);
        }

        public void iExist(string scope,int lineNumber) //identifier exists
        {
            var identifier = _recordStack.Pop();
            var symbol = (from s in _symbolTable where s.Value.Scope == scope && s.Value.Value == identifier.Value select s.Value).FirstOrDefault();
            if (symbol!=null)
            {
                identifier.LinkedSymbol = symbol;
                _recordStack.Push(identifier);
            }
            else
            {
                throw new Exception(string.Format("Semantic error at line {0}: Identifier {1} does not exist", lineNumber,identifier.Value));
            }
            if (DEBUG) Console.WriteLine("iExist: "+identifier.Value);
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
                childId.LinkedSymbol = symbol;
                _recordStack.Push(childId);
            }
            else
            {
                throw new Exception(string.Format("Semantic error at line {0}: Identifier {2}.{1} does not exist", lineNumber, childId.Value, parentId.Value));
            }
            if (DEBUG) Console.WriteLine("rExist");
        }

        public void oPush(Operator o,int lineNumber) //operator push
        {
            if (_operatorStack.Count > 0)
            {
                var nextOp = _operatorStack.Peek();
                if (_opPriority.ContainsKey(o) && _opPriority.ContainsKey(nextOp))
                    if (_opPriority[o] < _opPriority[_operatorStack.Peek()])
                        evalOp(lineNumber);
            }
            _operatorStack.Push(o);
            if (DEBUG) Console.WriteLine("oPush: "+o.ToString());
        }

        public void newObj(string scope,int lineNumber)
        {
            var alSar = _recordStack.Pop();
            var typeSar = _recordStack.Pop();

            var sym = _symbolTable.First(s => s.Value.Kind == "Constructor" && s.Value.Value == typeSar.Value).Value;
            if(sym==null)
                throw new Exception("need better text for this, but theres no constructor for the class your instantiating.");
            typeSar.ArgumentList=new Stack<Record>();
            foreach (var a in sym.Data.Params)
            {
                var argRecord = alSar.ArgumentList.Pop();
                var argName = argRecord.Value;
                var symbol =
                    (from p in _symbolTable
                        where p.Value.Scope == scope && p.Value.Value == argName
                        select p.Value).FirstOrDefault();
                if (symbol == null || symbol.Data.Type != _symbolTable[a].Data.Type)
                    throw new Exception(
                        string.Format(
                            "Semantic error at line {0}: Constructor for class '{1}' does not have a param '{2}' of type '{3}', expected a value of type '{4}' instead",
                            lineNumber, typeSar.Value, argName, symbol.Data.Type ?? "null", _symbolTable[a].Data.Type));
                typeSar.ArgumentList.Push(argRecord);
            }

            typeSar.Type = RecordType.New;
            typeSar.LinkedSymbol = sym;
            _recordStack.Push(typeSar);
            Console.WriteLine("newObj: " + typeSar.Value);
        }

        public void vPush(string scope,string vName,bool isArray)
        {
            var sym = (from s in _symbolTable where s.Value.Scope == scope && s.Value.Value == vName select s.Value).FirstOrDefault();
            if(sym==null)throw new Exception("Semantic Error: write some better error text here, but this vpush should be getting an associated symbol");
            if(isArray != !(sym.Data==null || sym.Data.IsArray==false))
                throw new Exception("Semantic Error: loaded symbol and scanned symbol do not match, array-wise....");
            _recordStack.Push(new Record(RecordType.Variable, vName, sym));
            if (DEBUG) Console.WriteLine("vPush: " + vName);
        }

        public void lPush(TokenType type)
        {
            var name = TokenData.Get()[type].Name;
            _recordStack.Push(new Record(RecordType.Literal, TokenData.Get()[type].Name, null));
            if (DEBUG) Console.WriteLine("lPush: " + name);
        }

        public void tPush(string tname)
        {
            _recordStack.Push(new Record(RecordType.Type, tname,null));
            if (DEBUG) Console.WriteLine("tPush: " + tname);
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
                if (DEBUG) Console.WriteLine("tExist: " + type.Value);
            }
            else
                throw new Exception(string.Format("Semantic error at line {0}: Type {1} does not exist", lineNumber, type.Value));
        }

        private void evalOp(int lineNumber)
        {
            while (_operatorStack.Count > 0)
            {
                var nextOp = _operatorStack.Peek();
                if (_opPriority[nextOp] == 0) break;

                switch (_operatorStack.Pop())
                {
                    case Operator.Assignment:
                    {

                        var i1 = _recordStack.Pop();
                        var i2 = _recordStack.Pop();
                        checkRecordsAreSameType(GetCompareString(i1), GetCompareString(i2), lineNumber);
                    }
                        break;
                    case Operator.Add:
                    case Operator.Subtract:
                    case Operator.Multiply:
                    case Operator.Divide:
                    {
                        var i1 = _recordStack.Pop();
                        var i2 = _recordStack.Pop();
                        checkRecordsAreSameType(GetCompareString(i1), GetCompareString(i2), lineNumber);
                        var result = new Record(
                            RecordType.Temporary,
                            i1 + "." + i2,
                            new Symbol {Data = new Data {Type = GetCompareString(i1)}});
                        _recordStack.Push(result);
                    }
                        break;
                    case Operator.Less:
                    case Operator.More:
                    case Operator.LessOrEqual:
                    case Operator.MoreOrEqual:
                    case Operator.Equals:
                    case Operator.NotEquals:
                    case Operator.And:
                    case Operator.Or:
                        {
                            var i1 = _recordStack.Pop();
                            var i2 = _recordStack.Pop();
                            checkRecordsAreSameType(GetCompareString(i1), GetCompareString(i2), lineNumber);
                            var result = new Record(
                                RecordType.Temporary,
                                i1 + "." + i2,
                                new Symbol { Data = new Data { Type = "bool" } });
                            _recordStack.Push(result);
                        }
                        break;
                }
            }
        }

        private static void checkRecordsAreSameType(string v1,string v2,int lineNumber)
        {
            if(!v1.Equals(v2))
                throw new Exception(string.Format("Semantic error at line {0}: Trying to perform operation between types '{1}' and '{2}'",
                                lineNumber,
                                v1,
                                v2));
        }

        private static string GetCompareString(Record r)
        {
            if (r.Type == RecordType.New || r.Type == RecordType.Variable || r.Type == RecordType.Identifier || r.Type == RecordType.Temporary)
            {
                return r.LinkedSymbol.Data.IsArray ? r.LinkedSymbol.Data.Type + "[]" : r.LinkedSymbol.Data.Type;
            }
            if (r.Type == RecordType.Literal) return valueMap[r.Value];
            if (r.Type == RecordType.NewArray) return r.Value + "[]";
            return "";
        }

        private static Dictionary<string,string> valueMap = new Dictionary<string,string>
        {
            {"Number","int"},
            {"Character","char"},
            {"Bool","bool"}
        };
    }

}

