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
    class Tarcoder
    {
        private Dictionary<string, Symbol> symbolTable;
        private List<Quad> icodeList;
        private List<Triad> tcodeList; 
        private const bool DEBUG =false;
        
        public Tarcoder(Dictionary<string,Symbol> _symbolTable, List<Quad> _icodeList)
        {
            symbolTable = _symbolTable;
            icodeList = _icodeList;
            tcodeList = new List<Triad>();
        }

        public List<Triad> Generate()
        {
            Start();
            return tcodeList;
        }

        private void Start()
        {
            AddTriad("","","","","","Start");
            AddTriad("", "CMP", "R0", "R0", "", "");
            AddTriad("", "ADI", "R0", "1", "", "");
            AddTriad("", "TRP", "1", "", "", "");
            AddTriad("", "TRP", "0", "", "", "");
            AddTriad("", "", "", "", "", "End");
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

