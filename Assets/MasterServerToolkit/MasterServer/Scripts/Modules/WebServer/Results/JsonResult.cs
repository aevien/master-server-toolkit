using MasterServerToolkit.Json;

namespace MasterServerToolkit.MasterServer
{
    public class JsonResult : HttpResult
    {
        public JsonResult(MstJson json)
        {
            Value = json.ToString();
            ContentType = "application/json";
        }
    } 
}
