using System.Collections.Generic;

namespace KXIParse
{
    interface ILexer
    {
        List<Token> GenerateTokenList();
    }
}
