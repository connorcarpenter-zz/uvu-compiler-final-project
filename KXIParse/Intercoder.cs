using System;
using System.Collections.Generic;
using System.Linq;
using Operator = KXIParse.Semanter.Operator;
using Record = KXIParse.Semanter.Record;

namespace KXIParse
{
    class Quad
    {
        public string Label { get; set; }
        public string Operation { get; set; }
        public string Operand1 { get; set; }
        public string Operand2 { get; set; }
        public string Operand3 { get; set; }

        public Quad(string label, string op0, string op1, string op2, string op3)
        {
            Label = label;
            Operation = op0;
            Operand1 = op1;
            Operand2 = op2;
            Operand3 = op3;
        }

        public override string ToString()
        {
            if (Label.Equals(""))
            {
                return string.Format("{0} {1} {2} {3}", Operation, Operand1, Operand2, Operand3);
            }
            else
            {
                return string.Format("{4}: {0} {1} {2} {3}", Operation, Operand1, Operand2, Operand3, Label);
            }
        }
    }
    class Intercoder
    {
        private const bool DEBUG = false;
        public static List<Quad> IntercodeList;
        private static Stack<string> _tempVarNames;
        private static List<string> _labelNames;
        private static int labelNameIndex = -1;
        private static Dictionary<string, Symbol> symbolTable
        {
            get { return Syntaxer._syntaxSymbolTable; }
        }

        public Intercoder(List<Quad> intercodeList)
        {
            IntercodeList = intercodeList;
            _tempVarNames = new Stack<string>();
            _labelNames = new List<string>();
        }

        public string GetTempVarName(Record r)
        {
            if (r == null || r.LinkedSymbol == null || r.LinkedSymbol.Scope == null || r.LinkedSymbol.Scope.Length == 0)
            {
                throw new Exception("Intercode Error: Trying to make a temp variable, but there's no scope to add it to");
            }
            var name = "_tmp" + _tempVarNames.Count ;
            _tempVarNames.Push(name);

            symbolTable.Add(name,new Symbol{Scope = r.LinkedSymbol.Scope,Kind="temp",SymId=name,Value=name});

            return name;
        }

        public string GetLabelName(string description)
        {
            var finalname = "";
            var name = description.ToUpper();
            if (!_labelNames.Contains(name)) finalname = name;
            else
            {
                var numb = 0;
                while (_labelNames.Contains(name + numb))
                    numb += 1;
                finalname = name + numb;
            }
            
            _labelNames.Insert(labelNameIndex+1,finalname);
            labelNameIndex += 1;
            return finalname;
        }

        public string GetLabelBottom()
        {
            if (labelNameIndex < 0)
            {
                if(_labelNames.Count>0)
                    throw new Exception("Label index is under-flowin");
                return null;
            }
            if (labelNameIndex > _labelNames.Count - 1)
            {
                throw new Exception("Label index is over-flowin");
            }
            var output = _labelNames[labelNameIndex];
            if(labelNameIndex!=0)
                labelNameIndex--;
            return output;
        }

        private static readonly Dictionary<Operator, string> OpMap = new Dictionary<Operator, string>
        {
            {Operator.Assignment,"MOV"},
            {Operator.Add,"ADD"},
            {Operator.Subtract,"SUB"},
            {Operator.Multiply,"MUL"},
            {Operator.Divide,"DIV"},
            {Operator.Less,"LT"},
            {Operator.LessOrEqual,"LE"},
            {Operator.More,"GT"},
            {Operator.MoreOrEqual,"GE"},
            {Operator.Equals,"EQ"},
            {Operator.NotEquals,"NE"},
            {Operator.And,"AND"},
            {Operator.Or,"OR"}
        };

        public void WriteReference(Record r1, Record r2,Record r3)
        {
            var operation = "REF";
            var operand1 = ToOperand(r1);
            var operand2 = r3.LinkedSymbol.SymId;
            var operand3 = ToOperand(r3);

            WriteQuad("", operation, operand1, operand2, operand3,"reference");
        }

        public void WriteOperation(Operator nextOp, Record r1, Record r2, Record r3 = null)
        {
            var operation = OpMap[nextOp];
            var operand1 = ToOperand(r1);
            if (r2.Type == Semanter.RecordType.IVar)
            {
                Console.WriteLine("Not yet implemented");
                return; //this is to weed out static initializers
            }
            var operand2 = ToOperand(r2);
            var operand3 = ToOperand(r3);

            if (nextOp == Operator.Assignment)
                WriteQuad("", operation, operand1, operand2, null, "operation");
            else
                WriteQuad("", operation, operand1, operand2, operand3, "operation");
        }

        private void WriteQuad(string label, string op0, string op1, string op2, string op3,string action)
        {
            if (op0.Equals("BF") && op1.StartsWith("first"))
            {
                var x = 5;
            }

            var laster = IntercodeList.LastOrDefault();
            if(laster!=null && (laster.Operand3!=null && laster.Operand3.Equals("OPENLABELSLOT")))
            {
                laster.Operation = op0;
                laster.Operand1 = op1;
                laster.Operand2 = op2;
                laster.Operand3 = op3;

                if (label.Equals(""))
                {
                    if (DEBUG)
                        Console.WriteLine(laster);
                }
                else
                {
                    switch (action)
                    {
                        case "if":
                            LabelBackPatch(label, laster.Label);
                            break;
                        case "while":
                            LabelBackPatch(laster.Label, label);
                            break;
                        case "method":
                            WriteQuad("", "MOV", "R0", "R0", "", "empty");
                            WriteQuad(label, "", "", "", "OPENLABELSLOT", "method");
                            break;
                        default:
                            throw new Exception("Really don't think there should be backpatching outside an if or while");
                            break;
                    }
                }
            }
            else
            {
                var nextQuad = new Quad(label, op0, op1, op2, op3);
                IntercodeList.Add(nextQuad);

                if (DEBUG)
                    if((op3==null) || !op3.Equals("OPENLABELSLOT"))
                        Console.WriteLine(nextQuad);
            };
        }

        private void LabelBackPatch(string oldLabel, string newLabel)
        {
            //check if label is a method
            if (symbolTable.ContainsKey(oldLabel.ToLower()) && symbolTable[oldLabel.ToLower()].Kind.ToLower().Equals("method"))
            {
                WriteQuad("","MOV","R0","R0","","empty");
                WriteQuad(newLabel, "", "", "", "OPENLABELSLOT", "while");
                
                return;
            }
            foreach (var q in IntercodeList)
            {
                if (q.Label != null && q.Label.Equals(oldLabel))
                    q.Label = newLabel;
                if (q.Operand1 != null && q.Operand1.Equals(oldLabel))
                    q.Operand1 = newLabel;
                if (q.Operand2 != null && q.Operand2.Equals(oldLabel))
                    q.Operand2 = newLabel;
                if (q.Operand3 != null && q.Operand3.Equals(oldLabel))
                    q.Operand3 = newLabel;
                if (q.Operation != null && q.Operation.Equals(oldLabel))
                    throw new Exception("Label got into the operation type? Que isso?");
            }
            for (var i = 0; i < _labelNames.Count; i++)
            {
                if (!_labelNames[i].Equals(oldLabel)) continue;
                if (i <= labelNameIndex) labelNameIndex--;
                _labelNames.RemoveAt(i);
                break;
            }
            if(_labelNames.Contains(oldLabel))
                throw new Exception("Somehow you got double labels in your list man");
        }

        private string ToOperand(Record r)
        {
            if (r == null) return "";
            if (r.LinkedSymbol != null)
            {
                if (r.LinkedSymbol.Kind != null)
                {
                    if (r.LinkedSymbol.Kind.Equals("literal"))
                        return r.LinkedSymbol.SymId;
                    switch (r.Type)
                    {
                        case Semanter.RecordType.Reference:
                        case Semanter.RecordType.New:
                        case Semanter.RecordType.NewArray:
                            return r.TempVariable.ToString();
                            break;
                        case Semanter.RecordType.LVar:
                        case Semanter.RecordType.Identifier:
                            if (r.LinkedSymbol.Kind.ToLower().Equals("method") && r.TempVariable != null &&
                                r.TempVariable.ToString().Length != 0)
                                return r.TempVariable.ToString();
                            return r.LinkedSymbol.SymId;
                            break;
                        default:
                            throw new Exception("In ToOperand(), trying to convert a non-supported recordtype");
                    }
                }
            }
            if (r.Type == Semanter.RecordType.NewArray)
            {
                return r.TempVariable.ToString();
            }
            return r.Value;
        }

        public void WriteIf(Record expressionSar)
        {
            var op1 = GetExpressionOp(expressionSar);
            WriteQuad("", "BF", op1, GetLabelName("SkipIf"),"", "if");
        }

        private string GetExpressionOp(Record expressionSar)
        {
            var op1 = "";
            switch (expressionSar.Type)
            {
                case Semanter.RecordType.Temporary:
                    op1 = expressionSar.Value;
                    break;
                case Semanter.RecordType.Identifier:
                    if (expressionSar.LinkedSymbol != null)
                        op1 = expressionSar.LinkedSymbol.SymId;
                    break;
                case Semanter.RecordType.Reference:
                    op1 = expressionSar.TempVariable.ToString();
                    break;
            }
            if (op1.Length == 0)
                throw new Exception("ICODE Error: Trying to make expression instruction for unimplemented type");
            return op1;
        }

        public void WriteSkipIf(bool isElse)
        {
            var laster = GetLabelBottom();
            if (isElse)
                WriteQuad("", "JMP", GetLabelName("SkipElse"), "", "","if");
            WriteQuad(laster,"","","","OPENLABELSLOT","if");
        }

        public void WriteElse()
        {
            var laster = GetLabelBottom();
            WriteQuad(laster, "", "", "", "OPENLABELSLOT", "if");
        }

        public void WriteBeginWhile()
        {
            WriteQuad(GetLabelName("BeginWhile"), "", "", "", "OPENLABELSLOT","while");
        }

        public void WriteMiddleWhile(Record expressionSar)
        {
            var op1 = GetExpressionOp(expressionSar);
            WriteQuad("", "BF", op1, GetLabelName("EndWhile"), "", "while");
        }

        public void WriteEndWhile()
        {
            var laster = GetLabelBottom();
            var seconder = GetLabelBottom();
            WriteQuad("", "JMP", seconder, "", "","while");

            WriteQuad(laster, "", "", "", "OPENLABELSLOT", "while");
        }

        public void WriteReturn(string type, Record r, bool returnThis)
        {
            if (returnThis)
            {
                WriteQuad("", "RETURN", "this", "", "", "return");
            }
            else
            {
                if (type.Equals("void") || r == null)
                    WriteQuad("", "RTN", "", "", "", "return");
                else
                    WriteQuad("", "RETURN", ToOperand(r), "", "", "return");
            }
    }

        public void WriteCout(Record r)
        {
            WriteQuad("", "WRITE", r.LinkedSymbol.Data.Type.Equals("int") ? "1" : "2", ToOperand(r), "", "return");
        }

        public void WriteCin(Record r)
        {
            WriteQuad("", "READ", r.LinkedSymbol.Data.Type.Equals("int") ? "1" : "2", ToOperand(r), "", "return");
        }

        public void End()
        {
            //WriteReturn("void",null);
            //var lastOrDefault = IntercodeList.LastOrDefault();
           // if (lastOrDefault != null && lastOrDefault.Operand3 != null && lastOrDefault.Operand3.Equals("OPENLABELSLOT"))
           //     IntercodeList.Remove(lastOrDefault);
        }

        public void WriteFunctionCall(Record r1, Record r2,Record r3, Record r4,bool isThis = false)
        {
            var tempVariable = "this";
            if (!isThis)
            {
                tempVariable = r4.TempVariable.ToString();
            }
            else
            {
                if(r3.LinkedSymbol!=null)
                    tempVariable = r3.LinkedSymbol.SymId;
            }
            WriteQuad("","FRAME",r1.LinkedSymbol.SymId,tempVariable,"","function");
            if (r2.ArgumentList != null && r2.ArgumentList.Count > 0)
            {
                foreach (var a in r2.ArgumentList)
                    WriteQuad("","PUSH",ToOperand(a),"","","function");
            }
            WriteQuad("", "CALL", r1.LinkedSymbol.SymId, "", "", "function");
        }

        public void WriteFunctionPeek(Record r)
        {
            var tempVar = GetTempVarName(r);
            WriteQuad("", "PEEK", tempVar, "", "", "function");
            r.TempVariable = tempVar;
            //_tempVarNames.Pop();
        }

        public void WriteArray(Record r1, Record r2, Record r3)
        {
            WriteQuad("", "AEF", ToOperand(r2), ToOperand(r1), ToOperand(r3), "array");
        }

        public void WriteNewArray(Record r1, Record r2, Record r3)
        {
            var firstTemp = GetTempVarName(r1);
            var secondTemp = GetTempVarName(r1);
            WriteQuad("","MOVI",""+r3.LinkedSymbol.Data.Size,firstTemp,"","array");
            WriteQuad("", "MUL", firstTemp, ToOperand(r1), secondTemp, "array");
            WriteQuad("","NEW",secondTemp,ToOperand(r3),"","array");
        }

        public void WriteNewObj(Record r1,Record r2)
        {
            var newTemp = GetTempVarName(r1);
            WriteQuad("", "NEWI", ""+r2.LinkedSymbol.SymId, newTemp,"", "newobj");
            WriteQuad("", "FRAME", r1.LinkedSymbol.SymId, newTemp, "", "newobj");
            if (r1.ArgumentList != null && r1.ArgumentList.Count > 0)
            {
                foreach (var a in r1.ArgumentList)
                    WriteQuad("", "PUSH", ToOperand(a), "", "", "function");
            }
            WriteQuad("", "CALL", r1.LinkedSymbol.SymId, "", "", "newobj");
            WriteQuad("", "PEEK", r1.TempVariable.ToString(), "", "", "function");
        }

        public void AddMethodLabel(string symId)
        {
            WriteQuad(GetLabelName(symId), "", "", "", "OPENLABELSLOT", "method");
        }
    }
}

