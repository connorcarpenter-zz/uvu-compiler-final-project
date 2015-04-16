using System.Collections.Generic;

namespace KXIParse
{
    class Symbol
    {
        public string Scope { get; set; }
        public string SymId { get; set; }
        public string Value { get; set; }
        public string Kind { get; set; }
        public Data Data { get; set; }
        public int Vars { get; set; }
        public int Offset { get; set; }
    }

    internal class Data
    {
        public string Type { get; set; }
        public string AccessMod { get; set; }
        public bool IsArray { get; set; }
        public List<string> Params { get; set; }
        public int Size = 4;
    }
}
