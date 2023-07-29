using UnityEngine;

namespace MasterServerToolkit.Json
{
    public static partial class MstJsonTemplates
    {
        public static MstJson FromColor(this Color color)
        {
            var jsonObject = MstJson.EmptyObject;
            if (color.r != 0) jsonObject.AddField("r", color.r);
            if (color.g != 0) jsonObject.AddField("g", color.g);
            if (color.b != 0) jsonObject.AddField("b", color.b);
            if (color.a != 0) jsonObject.AddField("a", color.a);
            return jsonObject;
        }

        public static Color ToColor(this MstJson jsonObject)
        {
            var color = new Color();
            for (var i = 0; i < jsonObject.Count; i++)
            {
                switch (jsonObject.Keys[i])
                {
                    case "r":
                        color.r = jsonObject[i].FloatValue;
                        break;
                    case "g":
                        color.g = jsonObject[i].FloatValue;
                        break;
                    case "b":
                        color.b = jsonObject[i].FloatValue;
                        break;
                    case "a":
                        color.a = jsonObject[i].FloatValue;
                        break;
                }
            }

            return color;
        }

        public static MstJson ToJson(this Color color)
        {
            return color.FromColor();
        }
    }
}
