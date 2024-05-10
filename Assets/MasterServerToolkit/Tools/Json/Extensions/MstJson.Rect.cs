using UnityEngine;

namespace MasterServerToolkit.Json
{
    public static partial class MstJsonTemplates
    {
        public static MstJson FromRect(this Rect rect)
        {
            var jsonObject = MstJson.EmptyObject;
            if (rect.x != 0) jsonObject.AddField("x", rect.x);
            if (rect.y != 0) jsonObject.AddField("y", rect.y);
            if (rect.height != 0) jsonObject.AddField("height", rect.height);
            if (rect.width != 0) jsonObject.AddField("width", rect.width);
            return jsonObject;
        }

        public static Rect ToRect(this MstJson jsonObject)
        {
            var rect = new Rect();
            for (var i = 0; i < jsonObject.Count; i++)
            {
                switch (jsonObject.Keys[i])
                {
                    case "x":
                        rect.x = jsonObject[i].FloatValue;
                        break;
                    case "y":
                        rect.y = jsonObject[i].FloatValue;
                        break;
                    case "height":
                        rect.height = jsonObject[i].FloatValue;
                        break;
                    case "width":
                        rect.width = jsonObject[i].FloatValue;
                        break;
                }
            }

            return rect;
        }

        public static MstJson ToJson(this Rect rect)
        {
            return rect.FromRect();
        }
    }
}