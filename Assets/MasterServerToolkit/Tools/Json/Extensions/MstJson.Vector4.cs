using UnityEngine;

namespace MasterServerToolkit.Json
{
    public static partial class MstJsonTemplates
    {
        public static MstJson FromVector4(this Vector4 vector)
        {
            var jsonObject = MstJson.EmptyObject;
            if (vector.x != 0) jsonObject.AddField("x", vector.x);
            if (vector.y != 0) jsonObject.AddField("y", vector.y);
            if (vector.z != 0) jsonObject.AddField("z", vector.z);
            if (vector.w != 0) jsonObject.AddField("w", vector.w);
            return jsonObject;
        }

        public static Vector4 ToVector4(this MstJson jsonObject)
        {
            var x = jsonObject["x"] ? jsonObject["x"].FloatValue : 0;
            var y = jsonObject["y"] ? jsonObject["y"].FloatValue : 0;
            var z = jsonObject["z"] ? jsonObject["z"].FloatValue : 0;
            var w = jsonObject["w"] ? jsonObject["w"].FloatValue : 0;
            return new Vector4(x, y, z, w);
        }

        public static MstJson ToJson(this Vector4 vector)
        {
            return vector.FromVector4();
        }
    }
}