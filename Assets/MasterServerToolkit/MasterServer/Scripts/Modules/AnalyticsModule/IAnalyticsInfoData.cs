using MasterServerToolkit.Json;
using System;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public interface IAnalyticsInfoData
	{
         string Id { get; set; }
         string Key { get; set; }
         string  Category { get; set; }
         string UserId { get; set; }
         DateTime Timestamp { get; set; }
         Dictionary<string, string> Data { get; set; }
         MstJson ToJson();
    }
}