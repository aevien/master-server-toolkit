using UnityEngine;

namespace MasterServerToolkit.Json
{
    public static partial class MstJsonTemplates
    {
        public static MstJson FromLayerMask(this LayerMask layerMask)
        {
            var jsonObject = MstJson.EmptyObject;
            jsonObject.AddField("value", layerMask.value);
            return jsonObject;
        }

        public static LayerMask ToLayerMask(this MstJson jsonObject)
        {
            var layerMask = new LayerMask { value = jsonObject["value"].IntValue };
            return layerMask;
        }

        public static MstJson ToJson(this LayerMask layerMask)
        {
            return layerMask.FromLayerMask();
        }
    }
}