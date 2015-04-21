using System;
using System.Collections.Generic;
using System.Linq;

namespace KXIParse
{
    class VMShell
    {
        static bool DEBUG = false;
        static List<byte> byteCode;
        static public VM[] threads;
        static public bool ENDPROGRAM = false;

        public static void Execute(string programString)
        {
            /*
            //load instructions
            string programString = "";
            if (args.Length == 0)
            {
                //we are in debug mode
                programString = System.IO.File.ReadAllText(@"..\..\proj4.asm");
                DEBUG = true;
            }
            else
            {
                programString = System.IO.File.ReadAllText(args[0]);
                DEBUG = false;
                //TEST THIS
            }
            */

            var programLines = FormatProgram(programString);

            //first pass, get symbol table
            var symbolTable = FirstPass(programLines);

            //second pass, generate byte code
            byteCode = SecondPass(programLines, symbolTable);

            //configure opcodeMapR
            opcodeMapR = opcodeMap.ToDictionary(x => x.Value, x => x.Key);

            //create main VM
            var vm = new VM(true);
            //incrementing the amount of nulls here will increase the amount of available threads
            threads = new VM[] { vm, null, null, null, null };
            //start the main VM
            vm.ExecuteProgram();

            return;
        }

        #region NonVMStuff
        static List<string> FormatProgram(string input)
        {
            input = input.Replace("\t", "");
            var seperator = new string[] { "\r\n" };
            var inputs = new List<string>(input.Split(seperator, StringSplitOptions.None));
            var newInputs = new List<string>();
            foreach (var i in inputs)
            {
                var j = i;
                var semicolonIndex = i.IndexOf(';');
                if (semicolonIndex != -1)
                    j = i.Remove(semicolonIndex);
                j = j.Trim();

                if (j.Length == 0)
                    continue;

                //check and parse a string .byt shortcut
                var tokens = j.Split(' ');
                if (tokens.Contains(".BYT"))
                {
                    var k = 0;
                    for (k = 0; k < tokens.Length; k++)
                    {
                        if (tokens[k].Equals(".BYT"))
                        {
                            k++;
                            break;
                        }
                    }
                    for (var m = k + 1; m < tokens.Length; m++)
                        tokens[k] += " " + tokens[m];
                    var bytChar = tokens[k].Trim('\'');
                    if (!bytChar.Equals("\\n") && !bytChar.Equals("\\r"))
                    {
                        if (bytChar.Length > 1)
                        {
                            var firstByt = "" + bytChar[0];
                            if (firstByt.Equals(" "))
                                firstByt = "\\s";
                            newInputs.Add(tokens[0] + " .BYT '" + firstByt + "'");
                            for (var m = 1; m < bytChar.Length; m++)
                            {
                                if (bytChar[m] == ' ')
                                    newInputs.Add(".BYT '\\s'");
                                else
                                    newInputs.Add(".BYT '" + bytChar[m] + "'");
                            }
                            newInputs.Add("NOOP");
                            continue;
                        }
                    }
                    if (bytChar.Equals(" "))
                    {
                        newInputs.Add(tokens[0] + " .BYT '\\s'"); continue;
                    }
                }
                /////////////////////////////////////////

                newInputs.Add(j);
            }
            return newInputs;
        }

        static readonly Dictionary<string, int> opcodeMap = new Dictionary<string, int>()
        {
            {"JMP",1}, {"JMR",2}, {"BNZ",3}, {"BGT",4},{"BLT",5},{"BRZ",6},
            {"MOV",7},{"LDA",8},{"STR",9},{"LDR",10},{"STB",11},{"LDB",12},
            {"ADD",13},{"ADI",14},{"SUB",15},{"MUL",16},{"DIV",17},
            {"AND",18},{"OR",19},{"CMP",20},
            {"LDRI",21},{"STRI",22},{"LDBI",23},{"STBI",24},
            {"TRP",0},
            {"RUN",25},{"END",26},{"BLK",27},{"LCK",28},{"ULK",29},
        };
        static Dictionary<int, string> opcodeMapR;

        static readonly Dictionary<string, int> registerMap = new Dictionary<string, int>()
        {
            {"R0",0}, {"R1",1}, {"R2",2}, {"R3",3}, {"R4",4},{"R5",5},{"R6",6},{"R7",7},
            {"SL",8}, {"SP",9}, {"FP",10}, {"SB",11}, {"PC",12},
        };
        static readonly int instructionSize = 4;
        static readonly int intSize = 4;
        static readonly int byteSize = 1;

        static Dictionary<string, int> FirstPass(List<string> input)
        {
            var labelTable = new Dictionary<string, int>();

            int index = 0;

            var indexTable = new List<string>();

            foreach (var line in input)
            {
                indexTable.Add(line + " - " + index);

                var tokens = line.Split(' ');
                //check if its a valid opcode
                if (opcodeMap.ContainsKey(tokens[0]))
                {
                    index += instructionSize;
                    continue;
                }
                //check if it's a directive
                if (tokens[0].Equals(".INT")) { index += intSize; continue; }
                if (tokens[0].Equals(".BYT")) { index += byteSize; continue; }
                if (tokens[0].Equals("NOOP")) { index += byteSize; continue; }

                //tokens[0] is a label then
                if (labelTable.ContainsKey(tokens[0]))
                {
                    if (DEBUG)
                        Console.WriteLine(string.Format("Label: {0} is defined twice", tokens[0]));
                }
                else
                    labelTable.Add(tokens[0], index);

                //check how far to increase counter now that the label has been dealt with
                if (opcodeMap.ContainsKey(tokens[1]))
                {
                    index += instructionSize;
                    continue;
                }
                if (tokens[1].Equals(".INT"))
                {
                    index += intSize;
                    continue;
                }
                if (tokens[1].Equals(".BYT"))
                {
                    index += byteSize;
                    continue;
                }
            }

            return labelTable;
        }

        private static List<byte> SecondPass(List<string> input, Dictionary<string, int> symbolTable)
        {
            var indexList = new List<string>();
            var mem = new List<byte>();
            foreach (var line in input)
            {
                try
                {
                    var tokens = new List<string>(line.Split(' '));
                    if (symbolTable.ContainsKey(tokens[0]))
                    {
                        tokens.RemoveAt(0);
                    }

                    indexList.Add(tokens[0] + " - " + mem.Count());

                    switch (tokens[0])
                    {
                        case "END":
                        case "BLK":
                            try
                            {
                                mem.Add((byte)opcodeMap[tokens[0]]);
                                mem.Add(0);
                                mem.Add(0);
                                mem.Add(0);
                            }
                            catch (Exception)
                            {
                                throw;
                            }
                            break;
                        case "JMP":
                        case "LCK":
                        case "ULK":
                            try
                            {
                                AddLabelInstruction(mem, symbolTable, tokens);
                            }
                            catch (Exception)
                            {
                                throw;
                            }
                            break;
                        case "JMR":
                            try { 
                            mem.Add((byte) opcodeMap["JMR"]);
                            mem.Add((byte) registerMap[tokens[1]]);
                            mem.Add(0);
                            mem.Add(0);
                            }
                            catch (Exception)
                            {
                                throw;
                            }
                            break;
                        case "LDR":
                            try { 
                            if (registerMap.ContainsKey(tokens[2]))
                            {
                                mem.Add((byte)opcodeMap["LDRI"]);
                                mem.Add((byte)registerMap[tokens[1]]);
                                mem.Add((byte)registerMap[tokens[2]]);
                                mem.Add(0);
                            }
                            else if (symbolTable.ContainsKey(tokens[2]))
                            {
                                AddRegisterLabelInstruction(mem, symbolTable, tokens);
                            }
                            else throw new Exception("VM: Second Pass: Symbol table does not contain label: " + tokens[2]);
                            }
                            catch (Exception)
                            {
                                throw;
                            }
                            break;
                        case ".BYT":
                            try
                            {
                                var str = tokens[1];
                                var value = (int) tokens[1][1];
                                str = str.Trim('\'');
                                if (str.Equals("\\s"))
                                    value = 32;
                                if (str.Equals("\\n"))
                                    value = 10;
                                if (str.Equals("\\r"))
                                    value = 13;

                                mem.Add((byte) value);
                            }
                            catch (Exception)
                            {
                                throw;
                            }
                            break;
                        case ".INT":
                            try
                            {
                                var value = Convert.ToInt16(tokens[1]);
                                mem.Add((byte) ((value >> 24) & 0xFF));
                                mem.Add((byte) ((value >> 16) & 0xFF));
                                mem.Add((byte) ((value >> 8) & 0xFF));
                                mem.Add((byte) (value & 0xFF));
                            }
                            catch (Exception)
                            {
                                throw;
                            }
                            break;
                        case "BNZ":
                        case "BGT":
                        case "BLT":
                        case "BRZ":
                        case "LDA":
                        case "RUN":
                            try { 
                            AddRegisterLabelInstruction(mem, symbolTable, tokens);
                            }
                            catch (Exception)
                            {
                                throw;
                            }
                            break;
                        case "LDB":
                            try { 
                            if (registerMap.ContainsKey(tokens[2]))
                            {
                                mem.Add((byte) opcodeMap["LDBI"]);
                                mem.Add((byte) registerMap[tokens[1]]);
                                mem.Add((byte) registerMap[tokens[2]]);
                                mem.Add(0);
                            }
                            else if (symbolTable.ContainsKey(tokens[2]))
                            {
                                AddRegisterLabelInstruction(mem, symbolTable, tokens);
                            }
                            }
                            catch (Exception)
                            {
                                throw;
                            }
                            break;
                        case "STB":
                            try { 
                            if (registerMap.ContainsKey(tokens[2]))
                            {
                                mem.Add((byte) opcodeMap["STBI"]);
                                mem.Add((byte) registerMap[tokens[1]]);
                                mem.Add((byte) registerMap[tokens[2]]);
                                mem.Add(0);
                            }
                            else if (symbolTable.ContainsKey(tokens[2]))
                            {
                                AddRegisterLabelInstruction(mem, symbolTable, tokens);
                            }
                            }
                            catch (Exception)
                            {
                                throw;
                            }
                            break;
                        case "STR":
                            try { 
                            if (registerMap.ContainsKey(tokens[2]))
                            {
                                mem.Add((byte) opcodeMap["STRI"]);
                                mem.Add((byte) registerMap[tokens[1]]);
                                mem.Add((byte) registerMap[tokens[2]]);
                                mem.Add(0);
                            }
                            else if (symbolTable.ContainsKey(tokens[2]))
                            {
                                AddRegisterLabelInstruction(mem, symbolTable, tokens);
                            }
                            }
                            catch (Exception)
                            {
                                throw;
                            }
                            break;
                        case "MOV":
                        case "ADD":
                        case "SUB":
                        case "MUL":
                        case "DIV":
                        case "OR":
                        case "AND":
                        case "CMP":
                            try { 
                            AddRegisterRegisterInstruction(mem, symbolTable, tokens);
                            }
                            catch (Exception)
                            {
                                throw;
                            }
                            break;
                        case "ADI":
                            try
                            {
                                mem.Add((byte)opcodeMap[tokens[0]]);
                                mem.Add((byte)registerMap[tokens[1]]);
                                int i = Convert.ToInt16(tokens[2]);
                                if (Math.Abs(i) > 127)
                                    throw new Exception("VM: Too much data to store in ADI command");
                                if (i >= 0)
                                {
                                    mem.Add((byte)i);
                                    mem.Add(0);
                                }
                                else
                                {
                                    i = Math.Abs(i);
                                    mem.Add((byte)i);
                                    mem.Add(1);
                                }
                            }
                            catch (Exception)
                            {
                                throw;
                            }
                            break;
                        case "TRP":
                            try
                            {
                                mem.Add((byte)opcodeMap[tokens[0]]);
                                int i = Convert.ToInt16(tokens[1]);
                                mem.Add((byte)i);
                                mem.Add(0);
                                mem.Add(0);
                            }
                            catch (Exception)
                            {
                                throw;
                            }
                            break;
                        case "NOOP":
                            try
                            {
                                mem.Add(0);
                            }
                            catch (Exception)
                            {
                                throw;
                            }
                            break;
                        default:
                            if (DEBUG)
                                Console.WriteLine(string.Format("Error: {0} opcode is not recognized", tokens[0]));
                            break;
                    }
                }
                catch (Exception)
                {
                    throw;
                }
            }
            return mem;
        }

        static void AddLabelInstruction(List<byte> mem, Dictionary<string, int> symbolTable, List<string> tokens)
        {
            mem.Add((byte)opcodeMap[tokens[0]]);
            mem.Add(0);
            AddLabel(mem, symbolTable[tokens[1]]);
        }
        static void AddRegisterLabelInstruction(List<byte> mem, Dictionary<string, int> symbolTable, List<string> tokens)
        {
            mem.Add((byte)opcodeMap[tokens[0]]);
            mem.Add((byte)registerMap[tokens[1]]);
            AddLabel(mem, symbolTable[tokens[2]]);
        }
        static void AddRegisterRegisterInstruction(List<byte> mem, Dictionary<string, int> symbolTable, List<string> tokens)
        {
            mem.Add((byte)opcodeMap[tokens[0]]);
            mem.Add((byte)registerMap[tokens[1]]);
            mem.Add((byte)registerMap[tokens[2]]);
            mem.Add(0);
        }
        static void AddLabel(List<byte> mem, int address)
        {
            mem.Add((byte)((address >> 8) & 0xFF));
            mem.Add((byte)(address & 0xFF));
        }
        #endregion

        public class VM
        {
            private int PC;
            private int[] register;
            private int threadIndex = 0;
            private int threadId = 0;
            private List<byte> mem
            {
                get
                {
                    return byteCode;
                }
            }
            public bool IsMain;
            public bool EndThread = false;

            public VM(bool isMain)
            {
                IsMain = isMain;
                register = new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                PC = 0;
            }

            public VM(bool isMain, int pc, int[] reg, int tId)
            {
                IsMain = isMain;
                PC = pc;
                register = new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                reg.CopyTo(register, 0);
                threadId = tId;
            }

            public void ExecuteProgram()
            {
                if (!IsMain) return;
                while (!ENDPROGRAM)
                {
                    threads[threadIndex].ExecuteInstruction();
                    if (threads[threadIndex].EndThread)//remove thread if has signalled end
                        threads[threadIndex] = null;

                    //go to next thread
                    threadIndex++;
                    if (threadIndex >= threads.Length)
                        threadIndex = 0;
                    while (threads[threadIndex] == null)
                    {
                        threadIndex++;
                        if (threadIndex >= threads.Length)
                            threadIndex = 0;
                    }
                }
            }
            public void ExecuteInstruction()
            {
                var command = opcodeMapR[(int)mem[PC]];
                var op1 = mem[PC + 1];
                var op2 = mem[PC + 2];
                var label = GetLabel();
                var newPC = PC + instructionSize;

                switch (command)
                {
                    case "RUN":
                        CreateNewThread(op1, label);
                        break;
                    case "END":
                        if (!IsMain)
                            EndThread = true;
                        break;
                    case "BLK":
                        if (IsMain)
                        {
                            if (GetThreadCount() > 0)
                                newPC = PC;
                            else
                                newPC = newPC;
                        }
                        break;
                    case "LCK":
                        {
                            var tId = GetData(label);
                            if (tId == -1)
                            {
                                var value = threadId;
                                mem[label] = (byte)((value >> 24) & 0xFF);
                                mem[label + 1] = (byte)((value >> 16) & 0xFF);
                                mem[label + 2] = (byte)((value >> 8) & 0xFF);
                                mem[label + 3] = (byte)(value & 0xFF);
                                tId = value;
                            }
                            if (tId != threadId)
                                newPC = PC;
                        }
                        break;
                    case "ULK":
                        {
                            var tId = GetData(label);
                            if (tId == threadId)
                            {
                                var value = -1;
                                mem[label] = (byte)((value >> 24) & 0xFF);
                                mem[label + 1] = (byte)((value >> 16) & 0xFF);
                                mem[label + 2] = (byte)((value >> 8) & 0xFF);
                                mem[label + 3] = (byte)(value & 0xFF);
                            }
                        }
                        break;
                    case "TRP":
                        switch (op1)
                        {
                            case 0:
                                ENDPROGRAM = true;
                                break;
                            case 1:
                                Console.Write(GetReg(0));
                                break;
                            case 2:
                                short result;
                                if (!Int16.TryParse(Console.ReadLine(), out result))
                                {
                                    result = 0;
                                    if (DEBUG)
                                        Console.WriteLine("Invalid input (can't convert to Int)");
                                }
                                SetReg(0, (int)result);
                                break;
                            case 3:
                                Console.Write((char)GetReg(0));
                                break;
                            case 4:
                                SetReg(0, (int)(Console.ReadKey().KeyChar));
                                break;
                            case 10:
                                SetReg(0, GetReg(0) - 48);
                                break;
                            case 11:
                                SetReg(0, GetReg(0) + 48);
                                break;
                        }
                        break;
                    case "JMP":
                        newPC = label;
                        break;
                    case "JMR":
                        newPC = GetReg(op1);
                        break;
                    case "BNZ":
                        if (GetReg(op1) != 0)
                            newPC = label;
                        break;
                    case "BGT":
                        if (GetReg(op1) > 0)
                            newPC = label;
                        break;
                    case "BLT":
                        if (GetReg(op1) < 0)
                            newPC = label;
                        break;
                    case "BRZ":
                        if (GetReg(op1) == 0)
                            newPC = label;
                        break;
                    case "MOV":
                        SetReg(op1, GetReg(op2));
                        break;
                    case "ADI":
                        {
                            var op3 = mem[PC + 3];
                            var value = (int)op2;
                            if (op3 != 0)
                                value *= -1;
                            SetReg(op1, GetReg(op1) + value);
                        }
                        break;
                    case "ADD":
                        SetReg(op1, GetReg(op1) + GetReg(op2));
                        break;
                    case "SUB":
                    case "CMP":
                        SetReg(op1, GetReg(op1) - GetReg(op2));
                        break;
                    case "MUL":
                        SetReg(op1, GetReg(op1) * GetReg(op2));
                        break;
                    case "DIV":
                        SetReg(op1, GetReg(op1) / GetReg(op2));
                        break;
                    case "AND":
                        SetReg(op1, GetReg(op1) & GetReg(op2));
                        break;
                    case "OR":
                        SetReg(op1, GetReg(op1) | GetReg(op2));
                        break;
                    case "LDA":
                        SetReg(op1, label);
                        break;
                    case "STR":
                        {
                            var value = GetReg(op1);
                            while(mem.Count()<=label+3)
                                mem.Add(0);
                            mem[label] = (byte)((value >> 24) & 0xFF);
                            mem[label + 1] = (byte)((value >> 16) & 0xFF);
                            mem[label + 2] = (byte)((value >> 8) & 0xFF);
                            mem[label + 3] = (byte)(value & 0xFF);
                        }
                        break;
                    case "STRI":
                        {
                            var value = GetReg(op1);
                            var address = GetReg(op2);
                            while (mem.Count() <= address + 3)
                                mem.Add(0);
                            mem[address] = (byte)((value >> 24) & 0xFF);
                            mem[address + 1] = (byte)((value >> 16) & 0xFF);
                            mem[address + 2] = (byte)((value >> 8) & 0xFF);
                            mem[address + 3] = (byte)(value & 0xFF);
                        }
                        break;
                    case "LDR":
                        SetReg(op1, GetData(label));
                        break;
                    case "LDRI":
                        {
                            var address = GetReg(op2);
                            var value = 0;
                            if (address + 3 >= mem.Count())
                            {
                                if (DEBUG)
                                    Console.WriteLine("Attempting to LDR outside of memory...");
                                break;
                            }
                            value = (mem[address] << 24) +
                                (mem[address + 1] << 16) +
                                (mem[address + 2] << 8) +
                                (mem[address + 3]);
                            SetReg(op1, value);
                        }
                        break;
                    case "STB":
                        {
                            var value = GetReg(op1);
                            while (mem.Count() <= label)
                                mem.Add(0);
                            mem[label] = (byte)(value & 0xFF);
                        }
                        break;
                    case "STBI":
                        {
                            var value = GetReg(op1);
                            var address = GetReg(op2);
                            while (mem.Count() <= address)
                                mem.Add(0);
                            mem[address] = (byte)(value & 0xFF);
                        }
                        break;
                    case "LDB":
                        if (label >= mem.Count())
                        {
                            if (DEBUG)
                                Console.WriteLine("Attempting to LDB outside of memory...");
                            break;
                        }
                        SetReg(op1, mem[label]);
                        break;
                    case "LDBI":
                        {
                            var address = GetReg(op2);
                            if (address >= mem.Count())
                            {
                                if (DEBUG)
                                    Console.WriteLine("Attempting to LDBI outside of memory...");
                                break;
                            }
                            SetReg(op1, mem[address]);
                        }
                        break;
                }
                PC = newPC;
            }
            private int GetLabel()
            {
                return (mem[PC + 2] << 8) + mem[PC + 3];
            }
            private int GetData(int address)
            {
                if (address + 3 >= mem.Count()) return 0;
                return (mem[address] << 24) +
                    (mem[address + 1] << 16) +
                    (mem[address + 2] << 8) +
                    (mem[address + 3]);
            }
            private int GetReg(int index)
            {
                if (index <= 11) return register[index];
                return PC;
            }
            private void SetReg(int index, int value)
            {
                if (index <= 11) register[index] = value;
            }
            private int CreateNewThread(int regIndex, int address)
            {
                for (int i = 1; i < threads.Length; i++)
                {
                    if (threads[i] == null)
                    {
                        if (regIndex <= 11) register[regIndex] = i;
                        var vm = new VM(false, address, register, i);
                        threads[i] = vm;
                        return i;
                    }
                }
                if (DEBUG)
                {
                    Console.WriteLine("COULD NOT CREATE NEW THREAD");
                }
                return 0;
            }
            private int GetThreadCount()
            {
                var result = 0;
                for (int i = 1; i < threads.Length; i++)
                {
                    if (threads[i] != null)
                        result++;
                }
                return result;
            }
        }

    }
}
