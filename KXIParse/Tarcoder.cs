using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Operator = KXIParse.Semanter.Operator;
using Record = KXIParse.Semanter.Record;

namespace KXIParse
{
    class Triad
    {
        public string Label { get; set; }
        public string Operation { get; set; }
        public string Operand1 { get; set; }
        public string Operand2 { get; set; }
        public string Comment { get; set; }

        public Triad(string label, string op0, string op1, string op2,string comment = "")
        {
            Label = label;
            Operation = op0;
            Operand1 = op1;
            Operand2 = op2;
            Comment = comment;
        }

        public override string ToString()
        {
            var output = "";
            if (Label.Equals(""))
            {
                output =string.Format("{0} {1} {2}", Operation, Operand1, Operand2);
            }
            else
            {
                output =string.Format("{3}: {0} {1} {2}", Operation, Operand1, Operand2, Label);
            }
            if (Comment != null && !Comment.Equals(""))
                output += string.Format(" ; {0}", Comment);
            return output;
        }
    }

    public enum LocType { Register, Stack, Heap, Memory }
    class MemLoc
    {
        public LocType Type { get; set; }
        public int Offset { get; set; }
        public string Register { get; set; }
        public string Label { get; set; }
    }
    class Tarcoder
    {
        private Dictionary<string, Symbol> symbolTable;
        private List<Quad> icodeList;
        private List<Triad> tcodeList;

        //key is R0, R1, R2...
        //value is the list of symids currently associated with that register
        private Dictionary<string, List<string>> registers;

        //key is a symid
        //value is a list of locations that the symid can be found at
        private Dictionary<string, List<MemLoc>> locations;
        private const bool DEBUG =false;
        
        public Tarcoder(Dictionary<string,Symbol> _symbolTable, List<Quad> _icodeList)
        {
            symbolTable = _symbolTable;
            PostProcessSymTable(symbolTable, "param");
            PostProcessSymTable(symbolTable, "lvar");
            PostProcessSymTable(symbolTable, "ivar");
            icodeList = _icodeList;
            tcodeList = new List<Triad>();
            registers = new Dictionary<string, List<string>>();
            for(var i=0;i<7;i++)
                registers.Add("R"+i,new List<string>());
        }

        private static void PostProcessSymTable(Dictionary<string, Symbol> symTable, string kind)
        {
            foreach (var sym1 in symTable)
            {
                if (!sym1.Value.Kind.Equals(kind)) continue;

                var scope = sym1.Value.Scope.Split('.');
                var found = false;
                switch (sym1.Value.Kind)
                {
                    case "ivar":
                    {
                        //we're looking for a class
                        foreach (var sym2 in symTable.Where(sym2 => sym2.Value.Kind.Equals("Class") && sym2.Value.Value.Equals(scope[1])))
                        {
                            sym2.Value.Vars++;
                            sym1.Value.Offset = sym2.Value.Vars;
                            found = true;
                            break;
                        }
                        
                    }
                        break;
                    case "param":
                    case "lvar":
                    {
                        //we're looking for a method
                        foreach (var sym2 in symTable.Where(sym2 => (sym2.Value.Kind.Equals("Method") || sym2.Value.Kind.Equals("Constructor")) && sym2.Value.Value.Equals(scope.Last())))
                        {
                            sym2.Value.Vars++;
                            sym1.Value.Offset = sym2.Value.Vars;
                            found = true;
                            break;
                        }
                    }
                        break;
                }

                if(!found)
                    throw new Exception("Syntax error: can't find class/method to associate with new variable/parameter");
            }
        }

        public List<Triad> Generate()
        {
            Start();
            return tcodeList;
        }

        private void Start()
        {
            foreach (var q in icodeList)
            {
                switch (q.Operation)
                {
                    case "ADD":
                    case "SUB":
                    case "MUL":
                    case "DIV":
                        ConvertMathInstruction();
                        break;
                }
            }
        }

        private void ConvertMathInstruction(Quad q = null)
        {
            
        }

        private void addToRegister(string register, string symid)
        {
            registers[register].Add(symid);   
        }
        private string getRegister()
        {
            foreach (var r in registers)
            {
                if (r.Value.Count == 0)
                    return r.Key;
            }
            //need to figure out how to free up registers
            throw new Exception("TCode Error! You're out of free registers");
        }

        private string getLocation(string symid)
        {
            return "someLocation";
        }

        private void AddTriad(string label, string op1, string op2, string op3, string action, string comment)
        {
            var t = new Triad(label, op1, op2, op3, comment);
            tcodeList.Add(t);
        }

        public static string TCodeString(List<Triad> triads)
        {
            var sb = new StringBuilder();
            foreach (var t in triads)
            {
                sb.AppendLine(t.ToString());
            }
            return sb.ToString();
        }
    }
}

