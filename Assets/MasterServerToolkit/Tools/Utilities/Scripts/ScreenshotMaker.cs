using System;
using System.IO;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MasterServerToolkit.Utils
{
    /// <summary>
    /// Enhanced screenshot maker with specific support for UI capture.
    /// This version uses specialized methods for capturing UI elements that work with
    /// any Canvas render mode (including ScreenSpace-Overlay).
    /// Also works directly in the Editor without entering Play mode.
    /// </summary>
    public class ScreenshotMaker : SingletonBehaviour<ScreenshotMaker>
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        #region INSPECTOR

        [Header("General Settings")]
        /// <summary>
        /// Multiplier for screenshot resolution. Higher values produce larger, more detailed images.
        /// </summary>
        public int resolutionMultiplier = 2;

        /// <summary>
        /// Whether to apply the resolution multiplier to the width and height.
        /// If false, the exact width and height specified will be used.
        /// </summary>
        public bool applyResolutionMultiplier = true;

        /// <summary>
        /// Keyboard key that triggers screenshot capture when pressed.
        /// </summary>
        public KeyCode screenshotKey = KeyCode.F12;

        /// <summary>
        /// Width of the screenshot in pixels. If set to 0, the current screen width will be used.
        /// </summary>
        public int width = 1920;

        /// <summary>
        /// Height of the screenshot in pixels. If set to 0, the current screen height will be used.
        /// </summary>
        public int height = 1080;

        [Header("Capture Settings")]
        /// <summary>
        /// Specifies what to capture in the screenshot
        /// </summary>
        public CaptureMode captureMode = CaptureMode.Everything;

        /// <summary>
        /// Main camera used for game view capture
        /// </summary>
        public Camera mainCamera;

        /// <summary>
        /// Whether to capture UI with transparent background
        /// </summary>
        [Tooltip("Enable for transparent background in UI screenshots")]
        public bool transparentBackground = true;

        [Header("Debug")]
        /// <summary>
        /// Enable to show debug messages
        /// </summary>
        public bool debugMode = false;

        /// <summary>
        /// Enum defining what content to capture in the screenshot
        /// </summary>
        public enum CaptureMode
        {
            /// <summary>Captures everything including UI and game view</summary>
            Everything,
            /// <summary>Captures only game view without UI</summary>
            GameViewOnly,
            /// <summary>Captures only UI elements</summary>
            UIOnly
        }

        // Private member variables
        private bool isTakingScreenshot = false;
        private Camera currentCamera = null;

        #endregion

        /// <summary>
        /// Called when the component is initialized
        /// </summary>
        private void Start()
        {
            FindMainCamera();
        }

        /// <summary>
        /// Handles input for screenshot capture
        /// </summary>
        void Update()
        {
            if (Input.GetKeyDown(screenshotKey) && !isTakingScreenshot)
            {
                TakeScreenshot();
            }
        }

        /// <summary>
        /// Finds and sets the main camera if it's not already assigned
        /// </summary>
        private void FindMainCamera()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;

#if UNITY_EDITOR
                // If still no camera found, try to find any camera in the scene
                if (mainCamera == null)
                {
                    Camera[] cameras = FindObjectsOfType<Camera>();
                    if (cameras.Length > 0)
                    {
                        // Find the first enabled camera
                        foreach (Camera cam in cameras)
                        {
                            if (cam.enabled)
                            {
                                mainCamera = cam;
                                break;
                            }
                        }

                        // If no enabled camera, take the first one
                        if (mainCamera == null && cameras.Length > 0)
                        {
                            mainCamera = cameras[0];
                        }
                    }
                }
#endif

                if (mainCamera == null && debugMode)
                {
                    Debug.LogWarning("No main camera found!");
                }
            }
        }

        /// <summary>
        /// Generates a filename for the screenshot based on resolution and current timestamp.
        /// </summary>
        /// <param name="width">Width of the screenshot in pixels</param>
        /// <param name="height">Height of the screenshot in pixels</param>
        /// <param name="mode">The capture mode used</param>
        /// <returns>Full path to the screenshot file</returns>
        public static string ScreenShotName(int width, int height, CaptureMode mode)
        {
            // Define the directory path for saving screenshots
            string dir;

#if UNITY_EDITOR
            // In editor, use the project's Assets folder as a base
            dir = Path.Combine(Application.dataPath, "../Screenshots");
#else
            // In build, use the application's directory
            dir = Path.Combine(Directory.GetCurrentDirectory(), "Screenshots");
#endif

            // Create the directory if it doesn't exist
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Return a filename that includes resolution, mode and timestamp
            return Path.Combine(dir, $"screen_{mode}_{width}x{height}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png");
        }

        /// <summary>
        /// Takes a screenshot using the current settings and main camera
        /// </summary>
        [ContextMenu("Take Screenshot")]
        public void TakeScreenshot()
        {
            // Use the main camera by default
            TakeScreenshot(mainCamera);
        }

        /// <summary>
        /// Takes a screenshot using the specified camera
        /// </summary>
        /// <param name="camera">The camera to use for the screenshot. If null, the main camera will be used.</param>
        public void TakeScreenshot(Camera camera)
        {
            if (isTakingScreenshot)
                return;

            // Store the camera to use
            currentCamera = camera ?? mainCamera;

            // If still null, try to find main camera
            if (currentCamera == null)
            {
                FindMainCamera();
                currentCamera = mainCamera;

                // If we still don't have a camera, log an error and return
                if (currentCamera == null)
                {
                    Debug.LogError("No camera available for taking screenshot!");
                    return;
                }
            }

#if UNITY_EDITOR
            // In Edit mode, use direct method instead of coroutine
            if (!Application.isPlaying)
            {
                TakeEditorScreenshot();
                return;
            }
#endif

            // In Play mode, use coroutine
            StartCoroutine(CaptureScreenshot());
        }

        /// <summary>
        /// Takes a screenshot of UI elements only
        /// </summary>
        [ContextMenu("Take UI Screenshot")]
        public void TakeUIScreenshot()
        {
            captureMode = CaptureMode.UIOnly;
            TakeScreenshot();
        }

        /// <summary>
        /// Takes a screenshot of the game view without UI
        /// </summary>
        [ContextMenu("Take Game View Screenshot")]
        public void TakeGameViewScreenshot()
        {
            captureMode = CaptureMode.GameViewOnly;
            TakeScreenshot();
        }

        /// <summary>
        /// Takes a screenshot of the game view using the specified camera
        /// </summary>
        /// <param name="camera">The camera to use for the screenshot</param>
        public void TakeGameViewScreenshot(Camera camera)
        {
            captureMode = CaptureMode.GameViewOnly;
            TakeScreenshot(camera);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Method for taking screenshots in Editor mode
        /// </summary>
        private void TakeEditorScreenshot()
        {
            isTakingScreenshot = true;

            try
            {
                // Calculate resolution based on settings
                int resWidth, resHeight;

                if (applyResolutionMultiplier)
                {
                    resWidth = (width > 0 ? width : Screen.width) * resolutionMultiplier;
                    resHeight = (height > 0 ? height : Screen.height) * resolutionMultiplier;
                }
                else
                {
                    // Use exact dimensions specified in the inspector
                    resWidth = width > 0 ? width : Screen.width;
                    resHeight = height > 0 ? height : Screen.height;
                }

                if (debugMode)
                    Debug.Log($"Taking screenshot in Editor with mode: {captureMode}, resolution: {resWidth}x{resHeight}, camera: {(currentCamera != null ? currentCamera.name : "none")}");

                // Force repaint of scene view to ensure up-to-date rendering
                SceneView.RepaintAll();

                // Wait for repaint to complete
                EditorApplication.QueuePlayerLoopUpdate();

                // Create a render texture for the camera
                RenderTexture rt = new RenderTexture(resWidth, resHeight, 24);
                if (transparentBackground && captureMode == CaptureMode.UIOnly)
                {
                    rt.format = RenderTextureFormat.ARGB32;
                }

                // Remember the camera's original target texture
                RenderTexture originalRT = currentCamera.targetTexture;

                // Set the camera to render to our texture
                currentCamera.targetTexture = rt;

                // Remember the original culling mask
                int originalCullingMask = currentCamera.cullingMask;

                // Exclude UI layer if it exists for GameViewOnly mode
                if (captureMode == CaptureMode.GameViewOnly)
                {
                    int uiLayerIndex = LayerMask.NameToLayer("UI");
                    if (uiLayerIndex != -1)
                    {
                        currentCamera.cullingMask &= ~(1 << uiLayerIndex);
                    }
                }
                else if (captureMode == CaptureMode.UIOnly)
                {
                    // For UI only, just show UI layer
                    int uiLayerIndex = LayerMask.NameToLayer("UI");
                    if (uiLayerIndex != -1)
                    {
                        currentCamera.cullingMask = (1 << uiLayerIndex);
                    }
                    else
                    {
                        // If no UI layer, try to find canvas layers
                        Canvas[] canvases = FindObjectsOfType<Canvas>();
                        int newCullingMask = 0;
                        foreach (Canvas canvas in canvases)
                        {
                            newCullingMask |= (1 << canvas.gameObject.layer);
                        }

                        if (newCullingMask > 0)
                        {
                            currentCamera.cullingMask = newCullingMask;
                        }
                    }
                }

                // Render with the camera
                currentCamera.Render();

                // Create a texture to hold the screenshot
                TextureFormat format = (transparentBackground && captureMode == CaptureMode.UIOnly) ?
                    TextureFormat.RGBA32 : TextureFormat.RGB24;

                Texture2D screenTexture = new Texture2D(resWidth, resHeight, format, false);

                // Read the render texture
                RenderTexture.active = rt;
                screenTexture.ReadPixels(new Rect(0, 0, resWidth, resHeight), 0, 0);
                screenTexture.Apply();

                // Restore camera settings
                currentCamera.cullingMask = originalCullingMask;
                currentCamera.targetTexture = originalRT;
                RenderTexture.active = null;

                // Save the screenshot
                byte[] bytes = screenTexture.EncodeToPNG();
                string filename = ScreenShotName(resWidth, resHeight, captureMode);
                File.WriteAllBytes(filename, bytes);

                Debug.Log($"Screenshot saved to: {filename}");

                // Open the screenshot
                EditorUtility.RevealInFinder(filename);

                // Clean up
                UnityEngine.Object.DestroyImmediate(screenTexture);
                UnityEngine.Object.DestroyImmediate(rt);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error taking editor screenshot: {e.Message}\n{e.StackTrace}");
            }
            finally
            {
                // Always reset state
                currentCamera = null;
                isTakingScreenshot = false;
            }
        }
#endif

        /// <summary>
        /// Coroutine that handles the screenshot capture process in Play mode
        /// </summary>
        private IEnumerator CaptureScreenshot()
        {
            isTakingScreenshot = true;

            // Calculate resolution based on settings
            int resWidth, resHeight;

            if (applyResolutionMultiplier)
            {
                resWidth = (width > 0 ? width : Screen.width) * resolutionMultiplier;
                resHeight = (height > 0 ? height : Screen.height) * resolutionMultiplier;
            }
            else
            {
                // Use exact dimensions specified in the inspector
                resWidth = width > 0 ? width : Screen.width;
                resHeight = height > 0 ? height : Screen.height;
            }

            if (debugMode)
                Debug.Log($"Taking screenshot with mode: {captureMode}, resolution: {resWidth}x{resHeight}, camera: {(currentCamera != null ? currentCamera.name : "none")}");

            // Wait for the end of the frame to ensure all rendering is complete
            yield return new WaitForEndOfFrame();

            // Create textures for capturing
            Texture2D screenshot = null;

            switch (captureMode)
            {
                case CaptureMode.Everything:
                    // Capture the entire screen
                    screenshot = CaptureScreen(resWidth, resHeight);
                    break;

                case CaptureMode.GameViewOnly:
                    // Capture only the game view (without UI)
                    screenshot = CaptureGameView(resWidth, resHeight);
                    break;

                case CaptureMode.UIOnly:
                    // Capture only UI elements
                    screenshot = CaptureUI(resWidth, resHeight);
                    break;
            }

            if (screenshot != null)
            {
                // Save the screenshot
                byte[] bytes = screenshot.EncodeToPNG();
                string filename = ScreenShotName(resWidth, resHeight, captureMode);
                File.WriteAllBytes(filename, bytes);

                Debug.Log($"Screenshot saved to: {filename}");

                // Open the screenshot
                Application.OpenURL(filename);

                // Clean up
                Destroy(screenshot);
            }
            else
            {
                Debug.LogError("Failed to capture screenshot!");
            }

            // Reset current camera to ensure we don't keep a reference
            currentCamera = null;
            isTakingScreenshot = false;
        }

        /// <summary>
        /// Captures the entire screen (both game view and UI)
        /// </summary>
        private Texture2D CaptureScreen(int width, int height)
        {
            // Create a new texture
            Texture2D screenTexture = new Texture2D(width, height, TextureFormat.RGB24, false);

            // Resize the screen capture texture if needed
            if (width != Screen.width || height != Screen.height)
            {
                // Create a temporary render texture at the desired resolution
                RenderTexture rt = new RenderTexture(width, height, 24);

                // Remember the current active render texture
                RenderTexture currentRT = RenderTexture.active;

                // Capture the screen to the render texture
                Graphics.Blit(null, rt);
                RenderTexture.active = rt;

                // Read the screen pixels
                screenTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                screenTexture.Apply();

                // Restore the active render texture
                RenderTexture.active = currentRT;

                // Clean up
                Destroy(rt);
            }
            else
            {
                // Direct screen capture at same resolution
                screenTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                screenTexture.Apply();
            }

            return screenTexture;
        }

        /// <summary>
        /// Captures only the game view (without UI)
        /// </summary>
        private Texture2D CaptureGameView(int width, int height)
        {
            // Use the current camera for the screenshot
            Camera cameraToUse = currentCamera;

            if (cameraToUse == null)
            {
                cameraToUse = mainCamera;
                if (cameraToUse == null)
                {
                    Debug.LogError("No camera available for game view screenshot!");
                    return null;
                }
            }

            // Remember the camera's original target texture
            RenderTexture originalRT = cameraToUse.targetTexture;

            // Create a render texture for the camera
            RenderTexture rt = new RenderTexture(width, height, 24);
            cameraToUse.targetTexture = rt;

            // Remember the original culling mask
            int originalCullingMask = cameraToUse.cullingMask;

            // Exclude UI layer if it exists
            int uiLayerIndex = LayerMask.NameToLayer("UI");
            if (uiLayerIndex != -1)
            {
                cameraToUse.cullingMask &= ~(1 << uiLayerIndex);
            }

            // Render the camera
            cameraToUse.Render();

            // Create a texture to store the result
            Texture2D screenTexture = new Texture2D(width, height, TextureFormat.RGB24, false);

            // Read the render texture
            RenderTexture.active = rt;
            screenTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            screenTexture.Apply();

            // Restore camera settings
            cameraToUse.cullingMask = originalCullingMask;
            cameraToUse.targetTexture = originalRT;
            RenderTexture.active = null;

            // Clean up
            Destroy(rt);

            return screenTexture;
        }

        /// <summary>
        /// Captures only UI elements
        /// This method uses a separate camera to render UI layers only
        /// </summary>
        private Texture2D CaptureUI(int width, int height)
        {
            // Create a special camera for UI capture
            GameObject tempCameraObject = new GameObject("UI_Screenshot_Camera");
            Camera uiCamera = tempCameraObject.AddComponent<Camera>();

            // Configure camera for UI capture
            uiCamera.clearFlags = transparentBackground ? CameraClearFlags.Depth : CameraClearFlags.SolidColor;
            uiCamera.backgroundColor = transparentBackground ? Color.clear : Color.black;
            uiCamera.cullingMask = 0; // Start with nothing

            // Add UI layer to culling mask
            int uiLayerIndex = LayerMask.NameToLayer("UI");
            if (uiLayerIndex != -1)
            {
                uiCamera.cullingMask |= 1 << uiLayerIndex;
            }
            else
            {
                // If there's no "UI" layer, try to find all Canvas objects and add their layers
                Canvas[] canvases = FindObjectsOfType<Canvas>();
                foreach (Canvas canvas in canvases)
                {
                    uiCamera.cullingMask |= 1 << canvas.gameObject.layer;

                    if (debugMode)
                        Debug.Log($"Adding canvas layer to UI camera: {canvas.gameObject.name} on layer {canvas.gameObject.layer}");

                    // Also add all children of the canvas to the culling mask
                    foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true))
                    {
                        uiCamera.cullingMask |= 1 << child.gameObject.layer;
                    }
                }
            }

            // Set orthographic size and position to match screen
            uiCamera.orthographic = true;
            uiCamera.orthographicSize = Screen.height / 2.0f;
            uiCamera.transform.position = new Vector3(Screen.width / 2.0f, Screen.height / 2.0f, -1000);

            // Configure canvases for screenshot
            ConfigureCanvasesForScreenshot(uiCamera);

            // Create render texture
            RenderTexture rt = new RenderTexture(width, height, 24);
            rt.antiAliasing = 4;

            if (transparentBackground)
            {
                rt.format = RenderTextureFormat.ARGB32;
            }

            // Render to texture
            uiCamera.targetTexture = rt;
            uiCamera.Render();

            // Create texture for result
            TextureFormat format = transparentBackground ? TextureFormat.RGBA32 : TextureFormat.RGB24;
            Texture2D screenTexture = new Texture2D(width, height, format, false);

            // Read render texture
            RenderTexture.active = rt;
            screenTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            screenTexture.Apply();

            // Clean up
            RenderTexture.active = null;
            Destroy(rt);
            Destroy(tempCameraObject);

            // Restore any canvas settings we changed
            RestoreCanvasSettings();

            return screenTexture;
        }

        // Store original canvas settings
        private Canvas[] modifiedCanvases;
        private RenderMode[] originalRenderModes;
        private Camera[] originalWorldCameras;

        /// <summary>
        /// Configures all canvases in the scene for screenshot capture
        /// </summary>
        private void ConfigureCanvasesForScreenshot(Camera uiCamera)
        {
            // Find all canvases
            Canvas[] canvases = FindObjectsOfType<Canvas>();
            modifiedCanvases = canvases;
            originalRenderModes = new RenderMode[canvases.Length];
            originalWorldCameras = new Camera[canvases.Length];

            // Store and modify canvas settings
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];

                // Store original settings
                originalRenderModes[i] = canvas.renderMode;
                originalWorldCameras[i] = canvas.worldCamera;

                if (debugMode)
                    Debug.Log($"Canvas {canvas.name} original mode: {canvas.renderMode}");

                // For overlay canvases, change to camera mode temporarily
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = uiCamera;

                    // Force refresh of Canvas scaling
                    CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                    if (scaler != null)
                    {
                        float originalScale = scaler.scaleFactor;
                        scaler.scaleFactor = originalScale * 0.99f;
                        scaler.scaleFactor = originalScale;
                    }
                }
                // For camera canvases, point to our UI camera
                else if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
                {
                    canvas.worldCamera = uiCamera;
                }
                // Handle world space canvases
                else if (canvas.renderMode == RenderMode.WorldSpace)
                {
                    // Just ensure it's in the camera's view
                    uiCamera.transform.position = canvas.transform.position - canvas.transform.forward * 10;
                    uiCamera.transform.LookAt(canvas.transform);
                }
            }
        }

        /// <summary>
        /// Restores original canvas settings after screenshot
        /// </summary>
        private void RestoreCanvasSettings()
        {
            if (modifiedCanvases == null)
                return;

            for (int i = 0; i < modifiedCanvases.Length; i++)
            {
                Canvas canvas = modifiedCanvases[i];
                if (canvas != null)
                {
                    canvas.renderMode = originalRenderModes[i];
                    canvas.worldCamera = originalWorldCameras[i];
                }
            }

            modifiedCanvases = null;
            originalRenderModes = null;
            originalWorldCameras = null;
        }

        /// <summary>
        /// Log information about all canvases in the scene
        /// </summary>
        [ContextMenu("Debug Canvas Info")]
        public void DebugCanvasInfo()
        {
            Canvas[] canvases = FindObjectsOfType<Canvas>(true);
            string info = $"Found {canvases.Length} canvases:\n";

            foreach (Canvas canvas in canvases)
            {
                info += $"- {canvas.name}:\n";
                info += $"  * RenderMode: {canvas.renderMode}\n";
                info += $"  * Layer: {LayerMask.LayerToName(canvas.gameObject.layer)} (index: {canvas.gameObject.layer})\n";
                info += $"  * WorldCamera: {(canvas.worldCamera ? canvas.worldCamera.name : "none")}\n";
                info += $"  * OverrideSorting: {canvas.overrideSorting}\n";
                info += $"  * SortOrder: {canvas.sortingOrder}\n";

                // Get all child UI elements
                Graphic[] graphics = canvas.GetComponentsInChildren<Graphic>(true);
                info += $"  * Has {graphics.Length} UI elements\n";
            }

            Debug.Log(info);
        }
#endif
    }
}