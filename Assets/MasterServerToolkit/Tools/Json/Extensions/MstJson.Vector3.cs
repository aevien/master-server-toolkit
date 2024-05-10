using UnityEngine;

namespace MasterServerToolkit.Json
{
    public static partial class MstJsonTemplates
    {
        public static MstJson FromVector3(this Vector3 vector)
        {
            var jsonObject = MstJson.EmptyObject;
            if (vector.x != 0) jsonObject.AddField("x", vector.x);
            if (vector.y != 0) jsonObject.AddField("y", vector.y);
            if (vector.z != 0) jsonObject.AddField("z", vector.z);
            return jsonObject;
        }

        public static Vector3 ToVector3(this MstJson jsonObject)
        {
            var x = jsonObject["x"] ? jsonObject["x"].FloatValue : 0;
            var y = jsonObject["y"] ? jsonObject["y"].FloatValue : 0;
            var z = jsonObject["z"] ? jsonObject["z"].FloatValue : 0;
            return new Vector3(x, y, z);
        }

        public static MstJson ToJson(this Vector3 vector)
        {
            return vector.FromVector3();
        }
    }
}