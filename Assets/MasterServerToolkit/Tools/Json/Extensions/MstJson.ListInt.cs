using MasterServerToolkit.Json;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public static partial class MstJsonTemplates
    {
        public static MstJson FromList(this List<int> list)
        {
            var json = MstJson.EmptyArray;

            foreach (var item in list)
            {
                json.Add(item);
            }

            return json;
        }

        public static List<int> ToList(this MstJson json)
        {
            var list = new List<int>();

            if (json.IsArray)
            {
                foreach (var item in json)
                {
                    list.Add(item.IntValue);
                }
            }

            return list;
        }
    }
}