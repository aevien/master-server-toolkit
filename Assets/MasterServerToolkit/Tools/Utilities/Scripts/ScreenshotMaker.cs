using System;
using System.IO;
using UnityEngine;

namespace MasterServerToolkit.Utils
{
    public class ScreenshotMaker : SingletonBehaviour<ScreenshotMaker>
    {
        #region INSPECTOR
        [Header("Settings")]
        public int resolutionMultiplier = 2;
        public KeyCode screenshoKey = KeyCode.K;
        public TextureFormat format = TextureFormat.RGB24;
        #endregion

        private int resWidth;
        private int resHeight;

        private void Start()
        {
            resWidth = Screen.width * resolutionMultiplier;
            resHeight = Screen.height * resolutionMultiplier;
        }

        public static string ScreenShotName(int width, int height)
        {
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "Screenshots");

            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            return Path.Combine(dir, $"screen_{width}x{height}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png");
        }

        void LateUpdate()
        {
            if (Input.GetKeyDown(screenshoKey))
            {
                TakeScreenshot();
            }
        }

        private void TakeScreenshot()
        {
            RenderTexture rt = new RenderTexture(resWidth, resHeight, 24);
            Texture2D screenShot = new Texture2D(resWidth, resHeight, format, false);
            Camera.main.targetTexture = rt;
            Camera.main.Render();
            RenderTexture.active = rt;
            screenShot.ReadPixels(new Rect(0, 0, resWidth, resHeight), 0, 0);
            Camera.main.targetTexture = null;
            RenderTexture.active = null;
            Destroy(rt);
            byte[] bytes = screenShot.EncodeToPNG();
            string filename = ScreenShotName(resWidth, resHeight);

            File.WriteAllBytes(filename, bytes);
            Debug.Log(string.Format("Took screenshot to: {0}", filename));
            Application.OpenURL(filename);
        }
    }
}