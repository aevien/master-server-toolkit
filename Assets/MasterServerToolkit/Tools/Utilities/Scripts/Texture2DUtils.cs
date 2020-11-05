using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Aevien.Utilities
{
    public static class Texture2DUtils
    {
        public static Texture2D CropImage(this Texture2D tex, int width, int height)
        {
            int tWidth = tex.width < width ? tex.width : width;
            int tHeight = tex.height < height ? tex.height : height;
            int tX = (tex.width / 2) - (tWidth / 2);
            int tY = (tex.height / 2) - (tHeight / 2);

            var pixels = tex.GetPixels(tX, tY, tWidth, tHeight);

            var newTex = new Texture2D(width, height, tex.format, false);
            newTex.SetPixels(pixels);
            newTex.Apply();

            return newTex;
        }
    }
}
