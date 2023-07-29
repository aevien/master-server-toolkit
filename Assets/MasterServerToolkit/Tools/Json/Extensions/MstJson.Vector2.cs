using UnityEngine;

namespace MasterServerToolkit.Json
{
    public static partial class MstJsonTemplates
    {
        public static Vector2 ToVector2(this MstJson jsonObject)
        {
            var x = jsonObject["x"] ? jsonObject["x"].FloatValue : 0;
            var y = jsonObject["y"] ? jsonObject["y"].FloatValue : 0;
            return new Vector2(x, y);
        }

        public static MstJson FromVector2(this Vector2 vector)
        {
            var jsonObject = MstJson.EmptyObject;
            if (vector.x != 0) jsonObject.AddField("x", vector.x);
            if (vector.y != 0) jsonObject.AddField("y", vector.y);
            return jsonObject;
        }

        public static MstJson ToJson(this Vector2 vector)
        {
            return vector.FromVector2();
        }
    }
}
