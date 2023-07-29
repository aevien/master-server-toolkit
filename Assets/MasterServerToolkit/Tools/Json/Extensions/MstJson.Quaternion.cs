using UnityEngine;

namespace MasterServerToolkit.Json
{
    public static partial class MstJsonTemplates
    {
        public static MstJson FromQuaternion(this Quaternion quaternion)
        {
            var jsonObject = MstJson.EmptyObject;
            if (quaternion.w != 0) jsonObject.AddField("w", quaternion.w);
            if (quaternion.x != 0) jsonObject.AddField("x", quaternion.x);
            if (quaternion.y != 0) jsonObject.AddField("y", quaternion.y);
            if (quaternion.z != 0) jsonObject.AddField("z", quaternion.z);
            return jsonObject;
        }

        public static Quaternion ToQuaternion(this MstJson jsonObject)
        {
            var x = jsonObject["x"] ? jsonObject["x"].FloatValue : 0;
            var y = jsonObject["y"] ? jsonObject["y"].FloatValue : 0;
            var z = jsonObject["z"] ? jsonObject["z"].FloatValue : 0;
            var w = jsonObject["w"] ? jsonObject["w"].FloatValue : 0;
            return new Quaternion(x, y, z, w);
        }

        public static MstJson ToJson(this Quaternion quaternion)
        {
            return quaternion.FromQuaternion();
        }
    }
}