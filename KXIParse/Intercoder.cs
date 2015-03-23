using System;
using System.Collections.Generic;
using Operator = KXIParse.Semanter.Operator;
using Record = KXIParse.Semanter.Record;

namespace KXIParse
{
    class Intercoder
    {
        private const bool DEBUG = true;
        public static List<string> IntercodeList;
        private static Stack<string> _tempVarNames; 

        public Intercoder(List<string> intercodeList)
        {
            IntercodeList = intercodeList;
            _tempVarNames = new Stack<string>();
        }

        public string GetTempVarName()
        {
            var name = "t" + _tempVarNames.Count;
            _tempVarNames.Push(name);
            return name;
        }

        private void ReleaseTempVars()
        {
            _tempVarNames.Clear();
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

        public void WriteOperation(Operator nextOp, Record i1, Record i2, Record result)
        {
            var nextLine = " - ";

            if (nextOp == Operator.Assignment)
                nextLine = string.Format("{0} {1} {2}", OpMap[nextOp], i1.Value, i2.Value);
            else
                nextLine = string.Format("{0} {1} {2} {3}", OpMap[nextOp], i1.Value, i2.Value, result.Value);

            IntercodeList.Add(nextLine);
            if(DEBUG)Console.WriteLine(nextLine);
        }
    }

}

