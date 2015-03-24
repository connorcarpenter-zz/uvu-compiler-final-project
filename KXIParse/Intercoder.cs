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
        private const bool DEBUG = true;
        public static List<Quad> IntercodeList;
        private static Stack<string> _tempVarNames;
        private static List<string> _labelNames;
        private static int labelNameIndex = -1;

        public Intercoder(List<Quad> intercodeList)
        {
            IntercodeList = intercodeList;
            _tempVarNames = new Stack<string>();
            _labelNames = new List<string>();
        }

        public string GetTempVarName()
        {
            var name = "t" + _tempVarNames.Count ;
            _tempVarNames.Push(name);
            return name;
        }

        public string GetLabelName(string description)
        {
            var name = description.ToUpper();
            var numb = 0;
            while (_labelNames.Contains(name + numb))
                numb += 1;
            _labelNames.Insert(labelNameIndex+1,name + numb);
            labelNameIndex += 1;
            return name + numb;
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
            var operand2 = ToOperand(r2);
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
                    if (r.Type == Semanter.RecordType.Reference)
                    {
                        var tempVar = r.TempVariable.ToString();
                        switch (r.LinkedSymbol.Kind)
                        {
                            case "ivar":
                            case "method":
                                return tempVar;
                            default:
                                throw new Exception("ToOperand(): Symbol is "+r.LinkedSymbol.Kind);
                        }
                    }
                }
            }
            return r.Value;
        }

        public void WriteIf(Record expressionSar)
        {
            var op1 = expressionSar.Value;
            WriteQuad("", "BF", op1, GetLabelName("SkipIf"),"", "if");
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
            var op1 = expressionSar.Value;
            WriteQuad("", "BF", op1, GetLabelName("EndWhile"), "", "while");
        }

        public void WriteEndWhile()
        {
            var laster = GetLabelBottom();
            var seconder = GetLabelBottom();
            WriteQuad("", "JMP", seconder, "", "","while");

            WriteQuad(laster, "", "", "", "OPENLABELSLOT", "while");
        }

        public void WriteReturn(string type,Record r)
        {
            if(type.Equals("void") || r==null)
                WriteQuad("","RTN","","","","return");
            else
                WriteQuad("", "RETURN", ToOperand(r), "", "", "return");
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
            var lastOrDefault = IntercodeList.LastOrDefault();
            if (lastOrDefault != null && lastOrDefault.Operand3 != null && lastOrDefault.Operand3.Equals("OPENLABELSLOT"))
                IntercodeList.Remove(lastOrDefault);
        }

        public void WriteFunctionCall(Record r1, Record r2)
        {
            var op2 = (r1 == null) ? "this" : ToOperand(r1);
            WriteQuad("","FRAME",ToOperand(r2),op2,"","function");
            if (r2.ArgumentList != null && r2.ArgumentList.Count > 0)
            {
                foreach (var a in r2.ArgumentList)
                    WriteQuad("","PUSH",ToOperand(a),"","","function");
            }
            WriteQuad("", "CALL", ToOperand(r2),"","","function");
        }

        public void WriteFunctionPeek(Record r)
        {
            var tempVar = GetTempVarName();
            WriteQuad("", "PEEK", tempVar, "", "", "function");
            _tempVarNames.Pop();
        }
    }
}

