namespace MasterServerToolkit.MasterServer
{
    public class HtmlResult : StringResult
    {
        public HtmlResult(string value) : base(value)
        {
            ContentType = "text/html";
        }
    }
}
