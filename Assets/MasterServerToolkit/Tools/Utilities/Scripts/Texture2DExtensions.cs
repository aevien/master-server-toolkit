using System;
using UnityEngine;

namespace MasterServerToolkit.Extensions
{
    public static class Texture2DExtensions
    {
        public static string ToBase64Url(this Texture2D texture, bool asPng = true)
        {
            if (texture == null)
                throw new ArgumentNullException(nameof(texture), "Texture is null");

            if (texture.width <= 0 || texture.height <= 0)
                throw new ArgumentException("Texture has invalid size", nameof(texture));

            Texture2D src = texture;

            if (!IsReadable(src))
            {
                src = MakeReadableCopy(texture);

                if (src == null)
                    throw new InvalidOperationException("Failed to make a readable copy of the texture.");
            }

            byte[] data = asPng ? src.EncodeToPNG() : src.EncodeToJPG();

            if (ReferenceEquals(src, texture) == false)
            {
                UnityEngine.Object.Destroy(src);
            }

            if (data == null || data.Length == 0)
                throw new InvalidOperationException("Image encoding returned null/empty byte array.");

            string base64 = Convert.ToBase64String(data);
            string mime = asPng ? "image/png" : "image/jpeg";
            return $"data:{mime};base64,{base64}";
        }

        private static bool IsReadable(Texture2D tex)
        {
            try
            {
                _ = tex.GetPixel(0, 0);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static Texture2D MakeReadableCopy(Texture source)
        {
            int w = source.width;
            int h = source.height;

            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prevActive = RenderTexture.active;

            try
            {
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;

                var readable = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
                readable.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                readable.Apply(false, false);
                return readable;
            }
            finally
            {
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);
            }
        }
    }
}
