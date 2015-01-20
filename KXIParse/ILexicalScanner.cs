namespace KXIParse
{
    interface ILexicalScanner
    {
        Token GetToken();
        void NextToken();
    }
}
