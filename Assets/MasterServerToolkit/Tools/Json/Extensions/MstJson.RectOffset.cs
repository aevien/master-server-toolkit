using UnityEngine;

namespace MasterServerToolkit.Json
{
    public static partial class MstJsonTemplates
    {
        public static MstJson FromRectOffset(this RectOffset rectOffset)
        {
            var jsonObject = MstJson.EmptyObject;
            if (rectOffset.bottom != 0) jsonObject.AddField("bottom", rectOffset.bottom);
            if (rectOffset.left != 0) jsonObject.AddField("left", rectOffset.left);
            if (rectOffset.right != 0) jsonObject.AddField("right", rectOffset.right);
            if (rectOffset.top != 0) jsonObject.AddField("top", rectOffset.top);
            return jsonObject;
        }

        public static RectOffset ToRectOffset(this MstJson jsonObject)
        {
            var rectOffset = new RectOffset();
            for (var i = 0; i < jsonObject.Count; i++)
            {
                switch (jsonObject.Keys[i])
                {
                    case "bottom":
                        rectOffset.bottom = jsonObject[i].IntValue;
                        break;
                    case "left":
                        rectOffset.left = jsonObject[i].IntValue;
                        break;
                    case "right":
                        rectOffset.right = jsonObject[i].IntValue;
                        break;
                    case "top":
                        rectOffset.top = jsonObject[i].IntValue;
                        break;
                }
            }

            return rectOffset;
        }

        public static MstJson ToJson(this RectOffset rectOffset)
        {
            return rectOffset.FromRectOffset();
        }
    }
}