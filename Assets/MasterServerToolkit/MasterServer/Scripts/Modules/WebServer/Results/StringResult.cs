namespace MasterServerToolkit.MasterServer
{
    public class StringResult : HttpResult
    {
        public StringResult(string value)
        {
            Value = value;
        }
    }
}