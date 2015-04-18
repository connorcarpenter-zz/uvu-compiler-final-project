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
                output =string.Format("{3} {0} {1} {2}", Operation, Operand1, Operand2, Label);
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
        private List<string> lastUsedRegister; 

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
            lastUsedRegister = new List<string>();
            registers = new Dictionary<string, List<string>>();
            for (var i = 0; i <= 7; i++)
            {
                registers.Add("R" + i, new List<string>());
                if(i!=0)lastUsedRegister.Add("R"+i);
            }
            locations = new Dictionary<string, List<MemLoc>>();
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

        private void addSymToReg(string register,string symId)
        {
            registers[register].Add(symId);
            lastUsedRegister.RemoveAll(s => s.Equals(register));
            lastUsedRegister.Add(register);
        }
        private string getEmptyRegister()
        {
            foreach (var r in registers)
            {
                if (r.Value.Count == 0 && r.Key!="R0")
                {
                    r.Value.Add("%temp%");
                    return r.Key;
                }
            }

            //need to figure out how to free up registers
            throw new Exception("TCode Error! You're out of free registers");
        }
        private string getRegister(string symId = "")
        {
            if (symId == "")
                throw new Exception("TCODE: Trying to get data for empty symbol id");

            //check if this symid has data in a register already
            checkLocationInit(symId);
            foreach (var l in locations[symId].Where(l => l.Type == LocType.Register))
                return l.Register;

            //find out where to get the symbol
            var symbol = symbolTable[symId];
            switch(symbol.Kind)
            {
                case "literal":
                {
                    var register = getEmptyRegister();
                    var loadOp = "LDR";
                    if (symbol.Data.Type == "Character")
                        loadOp = "LDB";
                    AddTriad("", loadOp, register, symId, "", "; Symbol " + symId + " is now in " + register);
                    addSymToReg(register,symId);
                    locations[symId].Add(new MemLoc() {Type = LocType.Register, Register = register});
                    CleanTempRegisters();
                    return register;
                }
                case "temp":
                case "lvar":
                {
                    var register1 = getEmptyRegister();
                    var register2 = getEmptyRegister();
                    AddTriad("", "MOV", register1, "FP", "", "");
                    var offset = "" + (symbol.Offset*-4);
                    AddTriad("", "ADI", register1, offset, "", "");
                    AddTriad("", "LDR", register2, register1, "", "; Symbol "+symId+" is now in "+register2);
                    addSymToReg(register2,symId);
                    locations[symId].Add(new MemLoc() { Type = LocType.Register, Register = register2 });
                    CleanTempRegisters();
                    return register2;
                }
                default:
                    throw new Exception("TCODE: Trying to get location of unknown symbol type");
                    break;
            }
        }

        private void CleanTempRegisters()
        {
            foreach (var register in registers)
                register.Value.RemoveAll(s => s.Equals("%temp%"));
        }

        private bool DeallocRegister(string register)
        {
            if (registers[register].Contains("%temp%")) return false;
            foreach (var sym in registers[register])
            {
                if(!symbolTable.ContainsKey(sym))
                    throw new Exception("TCODE: Trying to deallocate register associated with unknown symbol: "+sym);
                var symbol = symbolTable[sym];
                switch (symbol.Kind)
                {
                    case "lvar":
                    case "temp":
                    {
                        var register2 = getEmptyRegister();
                        AddTriad("", "MOV", register2, "FP", "", "");
                        var offset = "" + (symbol.Offset * -4);
                        AddTriad("", "ADI", register2, offset, "", "");
                        AddTriad("", "STR", register, register2, "", "; "+register+" is now in "+sym);
                        CleanTempRegisters();
                    }
                        break;
                    case "literal":
                        break;
                    default:
                        throw new Exception("TCODE: Trying to deallocate a register into an unknown symbol type");
                }
            }

            registers[register].Clear();
            return true;
        }

        private void checkLocationInit(string symId)
        {
            if(!locations.ContainsKey(symId))
                locations.Add(symId,new List<MemLoc>());
        }
        private string getLocation(string symid)
        {
            return "someLocation";
        }

        private void AddTriad(string label, string operation, string operand1, string operand2, string action, string comment)
        {
            if (tcodeList.LastOrDefault() != null && tcodeList.LastOrDefault().Operation.Equals("REPLACENEXT"))
            {
                var t = tcodeList.LastOrDefault();
                t.Operation = operation;
                t.Operand1 = operand1;
                t.Operand2 = operand2;
                t.Comment = comment;
            }
            else
            {
                var t = new Triad(label, operation, operand1, operand2, comment);
                tcodeList.Add(t);
            }
        }

        private void Start()
        {
            AddTriad("", "", "", "", "", "; Main Program");

            foreach (var q in icodeList)
            {
                if(q.Label.Length!=0)
                    AddTriad(q.Label, "REPLACENEXT", "", "", "", "");
                switch (q.Operation)
                {
                    case "ADD":
                    case "SUB":
                    case "MUL":
                    case "DIV":
                        ConvertMathInstruction(q);
                        break;
                    case "MOV":
                        ConvertMoveInstruction(q);
                        break;
                    case "WRITE":
                        ConvertWriteInstruction(q);
                        break;
                    case "FRAME":
                        ConvertFrameInstruction(q);
                        break;
                    case "CALL":
                        ConvertCallInstruction(q);
                        break;
                    case "RTN":
                    case "RETURN":
                        ConvertRtnInstruction(q);
                        break;
                    case "END":
                        AddTriad("END_PROGRAM", "TRP", "0", "", "", "");
                        break;
                }
            }
        }

        private void ConvertRtnInstruction(Quad q)
        {
            var rA = getEmptyRegister();
            var rB = getEmptyRegister();

            AddTriad("", "MOV", "SP", "FP", "", "; Checking for underflow");
            AddTriad("", "MOV", rA, "SP", "", "");
            AddTriad("", "CMP", rA, "SB", "", "");
            AddTriad("", "BGT", rA, "UNDERFLOW", "", "");

            AddTriad("", "LDR", rA, "FP", "", "; set previous frame to current frame and return");
            AddTriad("", "MOV", rB, "FP", "", "");
            AddTriad("", "ADI", rB, "-4", "", "");
            AddTriad("", "LDR", "FP", rB, "", "");

            //check if there's a return value
            if(q.Operation.Equals("RETURN"))
            {
                var retReg = getRegister(q.Operand1);
                AddTriad("", "STR", retReg, "SP", "", "; store return value");
            }

            AddTriad("", "JMR", rA, "", "", "");

            CleanTempRegisters();
        }

        private void ConvertMoveInstruction(Quad q)
        {
            var rA = getRegister(q.Operand1);
            var rB = getRegister(q.Operand2);

            AddTriad("", "MOV", rB, rA, "", "");
        }

        private void ConvertWriteInstruction(Quad q)
        {
            var rA = getRegister(q.Operand2);
            if (rA != "R0")
            {
                //put needed data into R0
                if (!DeallocRegister("R0"))
                {
                    //this block only triggers when DeallocRegister fails
                    throw new Exception(
                        "TCODE: Trying to deallocate temp variable in R0... If this happens consider making it so R0 can't hold temp variables?");
                }
                AddTriad("", "MOV", "R0", rA, "", "");
            }

            if (q.Operand1.Equals("2"))
            {
                AddTriad("", "TRP", "3","", "", "");
            }
            else
            {
                AddTriad("", "TRP", "1", "", "", "");
            }
        }

        private void ConvertMathInstruction(Quad q)
        {
            var rA = getRegister(q.Operand1);
            var rB = getRegister(q.Operand2);
            var rC = getRegister(q.Operand3);

            AddTriad("", "MOV", rC, rA, "", "");
            AddTriad("", q.Operation, rC, rB, "", "");
        }

        private void ConvertFrameInstruction(Quad q)
        {
            var rA = getEmptyRegister();
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

            CleanTempRegisters();
        }

        private void ConvertCallInstruction(Quad q)
        {
            var rA = getEmptyRegister();

            //first make room for local variables
            var methodSize = symbolTable[q.Operand1].Vars;
            if(symbolTable[q.Operand1].Data!=null && symbolTable[q.Operand1].Data.Params!=null)
                methodSize -= symbolTable[q.Operand1].Data.Params.Count;
            for(int i=0;i<methodSize;i++)
                AddTriad("", "ADI", "SP","-4", "", "; Freein up space on stack");

            AddTriad("", "MOV", rA, "PC", "", "; Finding return address");
            AddTriad("", "ADI", rA, "16", "", "");
            AddTriad("", "STR", rA, "FP", "", "; Return address to the beginning of the frame");
            AddTriad("", "JMP", q.Operand1, "", "", "");

            CleanTempRegisters();
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

