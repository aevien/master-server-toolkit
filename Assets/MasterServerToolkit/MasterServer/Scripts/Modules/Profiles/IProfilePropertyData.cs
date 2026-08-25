using MasterServerToolkit.Json;

namespace MasterServerToolkit.MasterServer
{
    public interface IProfilePropertyData
    {
        string AccountId { get; set; }
        string PropertyKey { get; set; }
        string PropertyValue { get; set; }

        MstJson ToJson();
    }
}
