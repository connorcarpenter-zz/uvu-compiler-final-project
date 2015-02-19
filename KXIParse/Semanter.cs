using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace KXIParse
{
    class Semanter
    {
        private const bool DEBUG = true;
        private Syntaxer syntaxer;
        private static Dictionary<string, Symbol> _symbolTable;
        private static Stack<Operator> _operatorStack;
        public enum Operator
        {
            And,Or,Equals,NotEquals,LessOrEqual,MoreOrEqual,
            Less,More,Add,Subtract,Multiply,Divide,Assignment
        }

        private static Dictionary<Operator, int> _opPriority = new Dictionary<Operator, int>
            {
                { Operator.Multiply, 3 }, { Operator.Divide, 3 },
                { Operator.Add, 2 }, { Operator.Subtract, 2 } 
            };
        private static Stack<Record> _recordStack;
        public enum RecordType
        {
            Identifier,Temporary
        }
        internal class Record
        {
            public RecordType Type { get; set; }
            public string Value { get; set; }
            public Symbol LinkedSymbol { get; set; }

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
            syntaxer = s;
            _symbolTable = st;
        }

        public void EOE(int lineNumber) //end of expression
        {
            evalOp(lineNumber);
            if(DEBUG)Console.WriteLine("EOE");
        }

        public void iPush(string iname) //identifier push
        {
            _recordStack.Push(new Record(RecordType.Identifier, iname,null));
            if (DEBUG) Console.WriteLine("iPush: "+iname);
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
                throw new Exception(string.Format("Error at line {0}: Identifier {1} does not exist", lineNumber,identifier.Value));
            }
            if (DEBUG) Console.WriteLine("iExist");
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
                throw new Exception(string.Format("Error at line {0}: Identifier {2}.{1} does not exist", lineNumber, childId.Value,parentId.Value));
            }
            if (DEBUG) Console.WriteLine("rExist");
        }

        public void oPush(Operator o,int lineNumber) //operator push
        {
            if (_opPriority.ContainsKey(o) && _opPriority.ContainsKey(_operatorStack.Peek()))
                if (_opPriority[o] < _opPriority[_operatorStack.Peek()])
                    evalOp(lineNumber);
            _operatorStack.Push(o);
            if (DEBUG) Console.WriteLine("oPush: "+o.ToString());
        }

        private void evalOp(int lineNumber)
        {
            switch (_operatorStack.Pop())
            {
                case Operator.Assignment:
                    {
                        var i1 = _recordStack.Pop();
                        var i2 = _recordStack.Pop();
                        checkRecordsAreSameType(i1, i2,lineNumber);
                    }
                    break;
                case Operator.Add:
                case Operator.Subtract:
                case Operator.Multiply:
                case Operator.Divide:
                    {
                        var i1 = _recordStack.Pop();
                        var i2 = _recordStack.Pop();
                        checkRecordsAreSameType(i1, i2, lineNumber);
                        var result = new Record(
                            RecordType.Temporary,
                            i1.Value + "." + i2.Value,
                            new Symbol {Data = new Data{Type = i1.LinkedSymbol.Data.Type}});
                        _recordStack.Push(result);
                    }
                    break;
            }
        }

        private static void checkRecordsAreSameType(Record r1,Record r2,int lineNumber)
        {
            if(r1.LinkedSymbol.Data.Type != r2.LinkedSymbol.Data.Type)
                            throw new Exception(string.Format("Error at line {0}: Trying to perform operation of variable '{1}' of type '{2}' to variable '{3}' of type '{4}'",
                                lineNumber,
                                r1.Value, r1.LinkedSymbol.Data.Type,
                                r2.Value, r2.LinkedSymbol.Data.Type));
        }
    }

}

