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
        public KeyCode screenshoKey = KeyCode.F12;
        public Camera screenCamera;
        public int width = 1920;
        public int height = 1080;

        #endregion

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

        [ContextMenu("Take screenshot")]
        private void TakeScreenshot()
        {
#if !UNITY_WEBGL || UNITY_EDITOR

            if (screenCamera == null)
            {
                screenCamera = Camera.main;
            }

            var resWidth = (width > 0 ? width : Screen.width) * resolutionMultiplier;
            var resHeight = (height > 0 ? height : Screen.height) * resolutionMultiplier;

            RenderTexture rt = new RenderTexture(resWidth, resHeight, 24);
            Texture2D screenShot = new Texture2D(resWidth, resHeight, TextureFormat.RGB24, false);
            screenCamera.targetTexture = rt;
            screenCamera.Render();
            RenderTexture.active = rt;
            screenShot.ReadPixels(new Rect(0, 0, resWidth, resHeight), 0, 0);
            screenCamera.targetTexture = null;
            RenderTexture.active = null;

            if (Application.isEditor)
            {
                DestroyImmediate(rt);
            }
            else
            {
                Destroy(rt);
            }

            byte[] bytes = screenShot.EncodeToPNG();
            string filename = ScreenShotName(resWidth, resHeight);

            File.WriteAllBytes(filename, bytes);
            Debug.Log(string.Format("Took screenshot to: {0}", filename));
            Application.OpenURL(filename);
#endif
        }
    }
}