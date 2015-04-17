using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
        private const int stackSize = 400;
        private const int heapSize = 400;

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

            //make main method
            symbolTable.Add("MAIN", new Symbol { Kind = "Method", Scope = "g", SymId = "MAIN", Value = "main", Vars = 0 });
            PostProcessSymTable(symbolTable, "param");
            PostProcessSymTable(symbolTable, "lvar");
            PostProcessSymTable(symbolTable, "ivar");
            PostProcessSymTable(symbolTable, "temp");

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
                    case "temp":
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
            GenerateStartCode();
            Start();
            GenerateEndCode();
            return tcodeList;
        }
        private void GenerateStartCode()
        {
            AddTriad("", "", "", "", "", "; General Initialization");
            AddTriad("", "LDA", "SL", "HEAP_START", "", "");
            AddTriad("", "STR", "SL", "FREE_HEAP_POINTER","","; Set up the free heap pointer");
            AddTriad("", "LDR", "R0", "HEAP_SIZE", "", "");
            AddTriad("", "ADD", "SL", "R0", "", "; Set up stack limit");
            AddTriad("", "MOV", "SB", "SL", "", "");
            AddTriad("", "LDR", "R0", "STACK_SIZE", "", "");
            AddTriad("", "ADD", "SB", "R0","", "; Set up stack base");
            AddTriad("", "MOV", "SP", "SB", "", "; Set up stack top pointer");
            AddTriad("", "MOV", "FP", "SB", "", "; Set up frame pointer");
        }
        private void GenerateEndCode()
        {
            AddTriad("END_PROGRAM","TRP","0","","","");
            AddTriad("", "", "", "", "", "; Set up global variables and heap/stack information");
            AddTriad("OVERFLOW", "TRP", "0", "", "","; Jump to this when there's overflow");
            AddTriad("UNDERFLOW", "TRP", "0", "", "", "; Jump to this when there's underflow");

            foreach (var sym in symbolTable.Where(sym => sym.Value.Kind == "literal"))
            {
                if(sym.Value.Data.Type.Equals("Number") || sym.Value.Data.Type.Equals("Boolean"))
                    AddTriad(sym.Key,".INT",sym.Value.Value,"","","");
                if (sym.Value.Data.Type.Equals("Character"))
                    AddTriad(sym.Key, ".BYT", sym.Value.Value, "", "", "");
            }

            
            AddTriad("FREE_HEAP_POINTER", ".INT", "0", "", "", "");
            AddTriad("STACK_SIZE", ".INT", Convert.ToString(stackSize), "", "", "");
            AddTriad("HEAP_SIZE", ".INT", Convert.ToString(heapSize), "", "", "");
            AddTriad("HEAP_START", "NOOP", "", "", "", "");
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

        private void Start()
        {
            AddTriad("", "", "", "", "", "; Main Program");

            foreach (var q in icodeList)
            {
                switch (q.Operation)
                {
                    case "ADD":
                    case "SUB":
                    case "MUL":
                    case "DIV":
                        ConvertMathInstruction(q);
                        break;
                    case "FRAME":
                        ConvertFrameInstruction(q);
                        break;
                    case "CALL":
                        ConvertCallInstruction(q);
                        break;
                }
            }
        }

        private void ConvertMathInstruction(Quad q)
        {
            
        }

        private void ConvertFrameInstruction(Quad q)
        {
            var rA = getRegister();
            //first test for overflow
            AddTriad("","MOV",rA,"SP","","; Settup up activation record for "+q.Operand1+" method");
            var methodSize = 8+(symbolTable[q.Operand1].Vars*4);
            methodSize *= -1;
            AddTriad("", "ADI", rA, "" + methodSize, "", "; Testing for overflow, this is the size of the method to be called");
            AddTriad("", "CMP", rA, "SL", "", "; Comparing new stack top to stack limit");
            AddTriad("", "BLT", rA, "OVERFLOW", "", "");
            
            //create the activation record
            AddTriad("", "MOV", rA, "FP", "", "; Save FP in "+rA+" this will be the PFP");
            AddTriad("", "MOV", "FP", "SP", "", "; Point at current activation record ; FP = SP");
            AddTriad("", "ADI", "SP", "-4", "", "; Adjust stack pointer for return address");
            AddTriad("", "STR", rA, "SP", "", "; PFP to Top of Stack");
            AddTriad("", "ADI", "SP", "-4", "", "; Adjust Stack pointer to new top");
        }

        private void ConvertCallInstruction(Quad q)
        {
            var rA = getRegister();

            //first make room for local variables
            var methodSize = symbolTable[q.Operand1].Vars;
            if(symbolTable[q.Operand1].Data!=null && symbolTable[q.Operand1].Data.Params!=null)
                methodSize -= symbolTable[q.Operand1].Data.Params.Count;
            for(int i=0;i<methodSize;i++)
                AddTriad("", "ADI", "SP","-4", "", "; Freein up space on stack");

            AddTriad("", "MOV", rA, "PC", "", "; Finding return address");
            AddTriad("", "ADI", rA, "16", "", "");
            AddTriad("", "STR", rA, "FP", "", "; Return address to the beginning of the frame");
            AddTriad("", "JMP", rA, q.Operand1, "", "");
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

