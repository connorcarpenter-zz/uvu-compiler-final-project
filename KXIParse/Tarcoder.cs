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
        private int compareLabels = 0;

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
            symbolTable.Add("MAIN", new Symbol { Kind = "method", Scope = "g", SymId = "MAIN", Value = "main", Vars = 0 });
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
                        foreach (var sym2 in symTable.Where(sym2 => (sym2.Value.Kind.ToLower().Equals("method") || sym2.Value.Kind.Equals("Constructor")) && sym2.Value.Value.Equals(scope.Last())))
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
                    throw new Exception("Tarcode error: can't find class/method to associate with new variable/parameter");
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
        private void Start()
        {
            AddTriad("", "", "", "", "", "; Main Program");

            foreach (var q in icodeList)
            {
                if (q.Label.Length != 0)//if there's a label on the next line
                {
                    DeallocAllRegisters();
                    AddTriad(q.Label, "REPLACENEXT", "", "", "", "");
                }
                switch (q.Operation)
                {
                    case "ADD":
                    case "SUB":
                    case "MUL":
                    case "DIV":
                        ConvertMathInstruction(q);
                        break;
                    case "AEF":
                    case "REF":
                        ConvertRefInstruction(q);
                        break;
                    case "EQ":
                    case "LT":
                    case "GT":
                    case "NE":
                    case "LE":
                    case "GE":
                        ConvertBoolInstruction(q);
                        break;
                    case "AND":
                    case "OR":
                        ConvertLogicInstruction(q);
                        break;
                    case "MOV":
                        ConvertMoveInstruction(q);
                        break;
                    case "MOVI":
                    {
                        var rA = GetRegister(q.Operand2);
                        AddTriad("", "SUB", rA, rA, "", string.Format("; move {0} into {1}", q.Operand1, rA));
                        AddTriad("", "ADI", rA, q.Operand1, "", "");
                    }
                        break;
                    case "WRITE":
                        ConvertWriteInstruction(q);
                        break;
                    case "READ":
                        ConvertReadInstruction(q);
                        break;
                    case "FRAME":
                        ConvertFrameInstruction(q);
                        break;
                    case "CALL":
                        DeallocAllRegisters();
                        ConvertCallInstruction(q);
                        break;
                    case "RTN":
                    case "RETURN":
                        DeallocAllRegisters();
                        ConvertRtnInstruction(q);
                        break;
                    case "END":
                        AddTriad("END_PROGRAM", "TRP", "0", "", "", "");
                        break;
                    case "BF":
                    {
                        DeallocAllRegisters();
                        var rA = GetRegister(q.Operand1);
                        AddTriad("", "BRZ", rA, q.Operand2, "", "; if "+rA+" == FALSE, GOTO " + q.Operand2);
                    }
                        break;
                    case "BT":
                    {
                        DeallocAllRegisters();
                        var rA = GetRegister(q.Operand1);
                        AddTriad("", "BNZ", rA, q.Operand2, "", "; if " + rA + " == TRUE, GOTO " + q.Operand2);
                    }
                        break;
                    case "JMP":
                        DeallocAllRegisters();
                        AddTriad("","JMP",q.Operand1,"","","; GOTO "+q.Operand1);
                        break;
                    case "PUSH":
                        {
                            var rA = GetRegister(q.Operand1);
                            AddTriad("", "STR", rA, "SP", "", string.Format("; Push {0} on the stack; {0} is in {1}",q.Operand1,rA));
                            AddTriad("", "ADI", "SP", "-4", "", "");
                        }
                        break;
                    case "POP":
                        {
                            var rA = GetRegister(q.Operand1);
                            AddTriad("", "ADI", "SP", "4", "", "");
                            AddTriad("", "LDR", rA, "SP", "", string.Format("; Pop top of stack into {0} ({1})", q.Operand1, rA));
                        }
                        break;
                    case "PEEK":
                        {
                            var rA = GetRegister(q.Operand1);
                            AddTriad("", "LDR", rA, "SP", "", string.Format("; Peek the stack into {0} ({1})", q.Operand1, rA));
                        }
                        break;
                    case "NEW":
                    case "NEWI":
                        ConvertNewInstruction(q);
                        break;
                    default:
                        throw new Exception("TCODE: Can't process unimplemented ICODE instruction");
                        break;
                }

                CleanInUseRegisters();
            }
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

                if(label.Length!=0)
                    throw new Exception("TCODE: in AddTriad(), double label all the way!");
            }
            else
            {
                var t = new Triad(label, operation, operand1, operand2, comment);
                tcodeList.Add(t);
            }
        }
        private string GetRegister(string symId)
        {
            //check if this symid has data in a register already
            CheckLocationInit(symId);
            foreach (var l in locations[symId].Where(l => l.Type == LocType.Register))
            {
                LastUsedRegister(l.Register);
                RegisterAddSym(l.Register,"%inuse%");
                return l.Register;
            }

            //find out where to get the symbol
            var symbol = symbolTable[symId];
            switch(symbol.Kind)
            {
                case "literal":
                {
                    var register = GetEmptyRegister();
                    var loadOp = "LDR";
                    if (symbol.Data.Type == "Character")
                        loadOp = "LDB";
                    AddTriad("", loadOp, register, symId, "", "; Symbol " + symId + " is now in " + register);
                    RegisterAddSym(register,symId);
                    RegisterAddSym(register, "%inuse%");
                    locations[symId].Add(new MemLoc() {Type = LocType.Register, Register = register});
                    CleanTempRegister(register);
                    return register;
                }
                case "temp":
                case "lvar":
                case "param":
                {
                    var register1 = GetEmptyRegister();
                    var register2 = GetEmptyRegister();
                    AddTriad("", "MOV", register1, "FP", "", "");
                    var offset = "" + ((symbol.Offset+2)*-4);
                    AddTriad("", "ADI", register1, offset, "", "");
                    AddTriad("", "LDR", register2, register1, "", "; Symbol "+symId+" is now in "+register2);
                    RegisterAddSym(register2,symId);
                    RegisterAddSym(register2, "%inuse%");
                    locations[symId].Add(new MemLoc() { Type = LocType.Register, Register = register2 });
                    CleanTempRegister(register1);
                    CleanTempRegister(register2);
                    return register2;
                }
                case "ivar":
                {
                    //don't know what to put here
                    var register1 = GetEmptyRegister();
                    var register2 = GetEmptyRegister();
                    AddTriad("", "MOV", register1, "FP", "", "");
                    AddTriad("", "ADI", register1, "-8", "", "");
                    AddTriad("", "LDR", register1, register1, "", "; the pointer to the THIS pointer on the stack is now in " + register1);
                    var offset = "" + ((symbol.Offset-1) * 4);
                    AddTriad("", "ADI", register1, offset, "", "");
                    AddTriad("", "LDR", register2, register1, "", "; Symbol " + symId + " is now in " + register2);
                    RegisterAddSym(register2, symId);
                    RegisterAddSym(register2, "%inuse%");
                    locations[symId].Add(new MemLoc() { Type = LocType.Register, Register = register2 });
                    CleanTempRegister(register1);
                    CleanTempRegister(register2);
                    return register2;
                }
                    break;
                default:
                    throw new Exception("TCODE: Trying to get location of unknown symbol type");
                    break;
            }
        }
        private string GetEmptyRegister(bool useReg0 = false)//only useReg0 when it's going to be in a small block with no other registers
        {
            foreach (var r in registers)
            {
                if (r.Value.Count == 0 && (r.Key != "R0" || useReg0))
                {
                    RegisterAddSym(r.Key, "%temp%");
                    return r.Key;
                }
            }

            //need to figure out how to free up registers
            return GetAnyDeallocRegister();
        }

        private void LastUsedRegister(string register)
        {
            lastUsedRegister.RemoveAll(s => s.Equals(register));
            lastUsedRegister.Add(register);
        }
        private void RegisterAddSym(string register, string symId)
        {
            registers[register].Add(symId);
            LastUsedRegister(register);
        }

        private void CleanTempRegister(string register)
        {
            registers[register].RemoveAll(s => s.Equals("%temp%"));
        }
        private void CleanInUseRegisters()
        {
            foreach (var register in registers)
                register.Value.RemoveAll(s => s.Equals("%inuse%"));
        }
        private string GetAnyDeallocRegister()
        {
            var register = lastUsedRegister.First();
            var i = 0;
            while (registers[register].Contains("%inuse%"))
            {
                LastUsedRegister(register);
                register = lastUsedRegister.First();
                i++;
                if(i>10)
                    throw new Exception("TCODE: All registers are currently in use...");
            }
            if (DeallocRegister(register))
            {
                RegisterAddSym(register,"%temp%");
                return register;
            }

            throw new Exception("TCODE: Error trying to dealloc register in GetAnyDeallocRegister()");
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
                    case "param":
                    {
                        var register2 = GetEmptyRegister(true);
                        AddTriad("", "MOV", register2, "FP", "", "");
                        var offset = "" + ((symbol.Offset+2) * -4);
                        AddTriad("", "ADI", register2, offset, "", "");
                        AddTriad("", "STR", register, register2, "", "; "+register+" is now in "+sym);
                        CleanTempRegister(register2);
                    }
                        break;
                    case "ivar":
                        {
                            var register2 = GetEmptyRegister(true);
                            AddTriad("", "MOV", register2, "FP", "", "");
                            AddTriad("", "ADI", register2, "-8", "", "; the pointer to the THIS pointer on the stack is now in "+register2);

                            AddTriad("", "LDR", register2, register2, "", "; the THIS pointer on the stack is now in " + register2);
                            var offset = "" + ((symbol.Offset-1) * 4);
                            AddTriad("", "ADI", register2, offset, "", "");
                            AddTriad("", "STR", register, register2, "", "; " + register + " is now in " + sym);
                            CleanTempRegister(register2);
                        }
                        break;
                    case "literal":
                        break;
                    default:
                        throw new Exception("TCODE: Trying to deallocate a register into an unknown symbol type");
                }
                locations[sym].RemoveAll(s => s.Register.Equals(register));
            }

            registers[register].Clear();
            return true;
        }
        private void DeallocAllRegisters()
        {
            foreach (var r in registers)
            {
                if (!DeallocRegister(r.Key))
                {
                    throw new Exception("TCODE: in DeallocAllRegisters(), for some reason register "+r.Key+" can't deallocate");
                }
            }
        }
        private void CheckLocationInit(string symId)
        {
            if(!locations.ContainsKey(symId))
                locations.Add(symId,new List<MemLoc>());
        }

        private string GetNewCompareLabel()
        {
            compareLabels++;
            return "COMPARE" + compareLabels;
        }

        private void ConvertNewInstruction(Quad q)
        {
            var rA = GetRegister(q.Operand2);
            var rB = q.Operand1;
            if(q.Operation.Equals("NEW")) rB = GetRegister(q.Operand1);
            var rC = GetEmptyRegister();

            AddTriad("", "LDR", rC, "FREE_HEAP_POINTER", "", "; Load address of free heap");
            AddTriad("", "MOV", rA, rC, "", "; save address into "+rA);
            AddTriad("", q.Operation.Equals("NEW") ? "ADD" : "ADI", rC, rB, "", "");
            AddTriad("", "STR", rC, "FREE_HEAP_POINTER", "", "; Update free heap pointer");
            //AddTriad("", q.Operation, rC, rB, "", "");

            CleanTempRegister(rC);
        }
        private void ConvertLogicInstruction(Quad q)
        {
            var rA = GetRegister(q.Operand1);
            var rB = GetRegister(q.Operand2);
            var rC = GetRegister(q.Operand3);

            AddTriad("", "MOV", rC, rA, "", "");
            AddTriad("", q.Operation, rC, rB, "", "");
        }
        private void ConvertBoolInstruction(Quad q)
        {
            var rA = GetRegister(q.Operand1);
            var rB = GetRegister(q.Operand2);
            var rC = GetRegister(q.Operand3);

            //I switched rA and rB here, seems like that
            AddTriad("", "MOV", rC, rA, "", "");
            AddTriad("", "CMP", rC, rB, "", "");

            var boolOp = "";
            if (q.Operation.Equals("EQ")) boolOp = "BRZ";
            if (q.Operation.Equals("NE")) boolOp = "BNZ";
            if (q.Operation.Equals("LT") || q.Operation.Equals("LE")) boolOp = "BLT";
            if (q.Operation.Equals("GT") || q.Operation.Equals("GE")) boolOp = "BGT";
            if(boolOp.Length==0)throw new Exception("TCODE: in ConvertBoolInstruction(), noop!");
            var compareLabel1 = GetNewCompareLabel();

            AddTriad("", boolOp, rC, compareLabel1, "", string.Format("; if {0} {1} {2} GOTO {3}",q.Operand1,q.Operation,q.Operand2,compareLabel1));

            if (q.Operation.Equals("LE") || q.Operation.Equals("GE"))
            {
                AddTriad("", "MOV", rC, rA, "", string.Format("; Test {0} == {1}", q.Operand1, q.Operand2));
                AddTriad("", "CMP", rC, rB, "", "");
                AddTriad("", "BRZ", rC, compareLabel1, "", string.Format("; if {0} == {1} GOTO {2}", q.Operand1, q.Operand2, compareLabel1));
            }

            AddTriad("", "CMP", rC, rC, "", "; Set "+rC+" to FALSE");

            var compareLabel2 = GetNewCompareLabel();

            AddTriad("", "JMP", compareLabel2, "", "", "");
            AddTriad(compareLabel1, "CMP", rC, rC, "", "");
            AddTriad("", "ADI", rC, "1", "", "; Set " + rC + " to TRUE");
            AddTriad(compareLabel2, "REPLACENEXT", "", "", "", "");
        }
        private void ConvertRtnInstruction(Quad q)
        {
            var rA = GetEmptyRegister();
            var rB = GetEmptyRegister();

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
                if (q.Operand1.Equals("this"))
                {
                    var retReg = GetEmptyRegister();
                    AddTriad("", "ADI", rB, "-4", "", "");
                    AddTriad("", "LDR", retReg, rB, "", "; loading this pointer into "+retReg);
                    AddTriad("", "STR", retReg, "SP", "", "; store return value");
                    CleanTempRegister(retReg);
                }
                else
                {
                    var retReg = GetRegister(q.Operand1);
                    AddTriad("", "STR", retReg, "SP", "", "; store return value");
                }
            }

            AddTriad("", "JMR", rA, "", "", "");

            CleanTempRegister(rA);
            CleanTempRegister(rB);
        }
        private void ConvertMoveInstruction(Quad q)
        {
            var rA = GetRegister(q.Operand1);
            var rB = GetRegister(q.Operand2);

            AddTriad("", "MOV", rB, rA, "", "");
        }
        private void ConvertWriteInstruction(Quad q)
        {
            var rA = GetRegister(q.Operand2);
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
        private void ConvertReadInstruction(Quad q)
        {
            var rA = GetRegister(q.Operand2);

            if (!DeallocRegister("R0"))
            {
                //this block only triggers when DeallocRegister fails
                throw new Exception(
                    "TCODE: Trying to deallocate temp variable in R0... If this happens consider making it so R0 can't hold temp variables?");
            }

            if (q.Operand1.Equals("2"))
            {
                AddTriad("", "TRP", "4", "", "", "");
            }
            else
            {
                AddTriad("", "TRP", "2", "", "", "");
            }

            AddTriad("", "MOV", rA, "R0", "", "; Reading user input into "+rA);
        }
        private void ConvertRefInstruction(Quad q)
        {
            var rA = GetRegister(q.Operand1);
            var rC = GetRegister(q.Operand3);
            var rB = GetEmptyRegister();//needs to be the offset of operand2

            AddTriad("", "CMP", rB, rB, "", "");
            var offset = symbolTable[q.Operand2].Offset-1;
            for (var i = 0; i < offset; i++)
            {
                AddTriad("", "ADI", rB, "4", "", "");
            }

            
            AddTriad("", "MOV", rC, rA, "", "");
            AddTriad("", "ADD", rC, rB, "", "");
            //AddTriad("", "LDR", rB, "FREE_HEAP_POINTER", "", "; Load address of free heap");
            //AddTriad("", "ADD", rC, rB, "", "");
            AddTriad("", "LDR", rC, rC, "", string.Format("; ref: {0}'s {1} is now in {2}",rA,rB,rC));
            CleanTempRegister(rB);
        }
        private void ConvertMathInstruction(Quad q)
        {
            var rA = GetRegister(q.Operand1);
            var rB = GetRegister(q.Operand2);
            var rC = GetRegister(q.Operand3);
            var op = q.Operation;

            AddTriad("", "MOV", rC, rA, "", "");
            AddTriad("", op, rC, rB, "", "");
        }
        private void ConvertFrameInstruction(Quad q)
        {
            var rA = GetEmptyRegister();
            var rB = rA;
            if (!q.Operand2.Equals("null"))
            {
                if (q.Operand2.Equals("this"))
                {
                    rB = GetEmptyRegister();
                    AddTriad("", "MOV", rB, "FP", "", "");
                    AddTriad("", "ADI", rB, "-8", "", "");
                    AddTriad("", "LDR", rB, rB, "", "; this pointer should now be in "+rB);
                }
                else
                    rB = GetRegister(q.Operand2);
            }

            //first test for overflow
            AddTriad("","MOV",rA,"SP","","; Settup up activation record for "+q.Operand1+" method");
            var methodSize = ((symbolTable[q.Operand1].Vars+3)*4);
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
            AddTriad("", "STR", rB, "SP", "", "; this pointer to the top of the stack");
            AddTriad("", "ADI", "SP", "-4", "", "; Adjust Stack pointer to new top");

            CleanTempRegister(rA);
            if (q.Operand2.Equals("this"))
                CleanTempRegister(rB);
        }
        private void ConvertCallInstruction(Quad q)
        {
            var rA = GetEmptyRegister();

            //first make room for local variables
            var methodSize = symbolTable[q.Operand1].Vars;
            if(symbolTable[q.Operand1].Data!=null && symbolTable[q.Operand1].Data.Params!=null)
                methodSize -= symbolTable[q.Operand1].Data.Params.Count;
            for(int i=0;i<methodSize;i++)
                AddTriad("", "ADI", "SP","-4", "", "; Freein up space on stack");

            AddTriad("", "MOV", rA, "PC", "", "; Finding return address");
            AddTriad("", "ADI", rA, "16", "", "");
            AddTriad("", "STR", rA, "FP", "", "; Return address to the beginning of the frame");
            AddTriad("", "JMP", q.Operand1.ToUpper(), "", "", "");

            CleanTempRegister(rA);
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

