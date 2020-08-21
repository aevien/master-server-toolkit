using System;
using System.IO;
using UnityEngine;

namespace Aevien.Utilities
{
    public class ScreenshotMaker : Singleton<ScreenshotMaker>
    {
        #region INSPECTOR
        [Header("Settings")]
        public int resolutionMultiplier = 2;
        public KeyCode screenshoKey = KeyCode.K;
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
            Camera.main.GetComponent<Camera>().targetTexture = rt;
            Texture2D screenShot = new Texture2D(resWidth, resHeight, TextureFormat.ARGB32, false);
            Camera.main.GetComponent<Camera>().Render();
            RenderTexture.active = rt;
            screenShot.ReadPixels(new Rect(0, 0, resWidth, resHeight), 0, 0);
            Camera.main.GetComponent<Camera>().targetTexture = null;
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