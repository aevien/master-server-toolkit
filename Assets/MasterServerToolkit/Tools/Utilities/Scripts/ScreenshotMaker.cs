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
        [Tooltip("Integer scale applied to Width and Height when Apply Resolution Multiplier is enabled. 1 keeps the configured resolution; larger values increase image size and capture cost.")]
        public int resolutionMultiplier = 2;

        /// <summary>
        /// Whether to apply the resolution multiplier to the width and height.
        /// If false, the exact width and height specified will be used.
        /// </summary>
        [Tooltip("Applies Resolution Multiplier to the configured Width and Height. Disable to capture at the exact configured or screen resolution.")]
        public bool applyResolutionMultiplier = true;

        /// <summary>
        /// Keyboard key that triggers screenshot capture when pressed.
        /// </summary>
        [Tooltip("Keyboard key that starts a screenshot while the component is active and no capture is already running.")]
        public KeyCode screenshotKey = KeyCode.F12;

        /// <summary>
        /// Width of the screenshot in pixels. If set to 0, the current screen width will be used.
        /// </summary>
        [Tooltip("Base screenshot width in pixels. 0 uses the current screen width. Resolution Multiplier may scale this value.")]
        public int width = 1920;

        /// <summary>
        /// Height of the screenshot in pixels. If set to 0, the current screen height will be used.
        /// </summary>
        [Tooltip("Base screenshot height in pixels. 0 uses the current screen height. Resolution Multiplier may scale this value.")]
        public int height = 1080;

        [Header("Capture Settings")]
        /// <summary>
        /// Specifies what to capture in the screenshot
        /// </summary>
        [Tooltip("Selects whether the capture contains the game view and UI, only the game view, or only UI elements.")]
        public CaptureMode captureMode = CaptureMode.Everything;

        /// <summary>
        /// Main camera used for game view capture
        /// </summary>
        [Tooltip("Camera used for game-view capture. When empty, the component tries Camera.main and then an available scene camera.")]
        public Camera mainCamera;

        /// <summary>
        /// Whether to capture UI with transparent background
        /// </summary>
        [Tooltip("Uses a transparent background for UI-only screenshots when the selected canvas and output format support alpha.")]
        public bool transparentBackground = true;

        [Header("Debug")]
        /// <summary>
        /// Enable to show debug messages
        /// </summary>
        [Tooltip("Writes detailed capture diagnostics to the Unity Console. Keep disabled during normal gameplay and production builds.")]
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

        protected override void Awake()
        {
#if UNITY_SERVER
            Destroy(gameObject);
#else
            base.Awake();
#endif
        }

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
            RenderTexture rt = null;
            Texture2D screenTexture = null;
            RenderTexture originalRT = null;
            RenderTexture originalActiveRT = RenderTexture.active;
            int originalCullingMask = 0;
            bool hasCameraState = false;

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
                rt = new RenderTexture(resWidth, resHeight, 24);
                if (transparentBackground && captureMode == CaptureMode.UIOnly)
                {
                    rt.format = RenderTextureFormat.ARGB32;
                }

                // Remember the camera's original target texture
                originalRT = currentCamera.targetTexture;

                // Set the camera to render to our texture
                currentCamera.targetTexture = rt;

                // Remember the original culling mask
                originalCullingMask = currentCamera.cullingMask;
                hasCameraState = true;

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

                screenTexture = new Texture2D(resWidth, resHeight, format, false);

                // Read the render texture
                RenderTexture.active = rt;
                screenTexture.ReadPixels(new Rect(0, 0, resWidth, resHeight), 0, 0);
                screenTexture.Apply();

                // Save the screenshot
                byte[] bytes = screenTexture.EncodeToPNG();
                string filename = ScreenShotName(resWidth, resHeight, captureMode);
                File.WriteAllBytes(filename, bytes);

                Debug.Log($"Screenshot saved to: {filename}");

                // Open the screenshot
                EditorUtility.RevealInFinder(filename);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error taking editor screenshot: {e.Message}\n{e.StackTrace}");
            }
            finally
            {
                if (hasCameraState && currentCamera != null)
                {
                    currentCamera.cullingMask = originalCullingMask;
                    currentCamera.targetTexture = originalRT;
                }

                RenderTexture.active = originalActiveRT;

                if (screenTexture != null)
                    UnityEngine.Object.DestroyImmediate(screenTexture);

                if (rt != null)
                    UnityEngine.Object.DestroyImmediate(rt);

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
            Texture2D screenshot = null;

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
                    Debug.Log($"Taking screenshot with mode: {captureMode}, resolution: {resWidth}x{resHeight}, camera: {(currentCamera != null ? currentCamera.name : "none")}");

                // Wait for the end of the frame to ensure all rendering is complete
                yield return new WaitForEndOfFrame();

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
                }
                else
                {
                    Debug.LogError("Failed to capture screenshot!");
                }
            }
            finally
            {
                if (screenshot != null)
                    Destroy(screenshot);

                // Reset current camera to ensure we don't keep a reference
                currentCamera = null;
                isTakingScreenshot = false;
            }
        }

        /// <summary>
        /// Captures the entire screen (both game view and UI)
        /// </summary>
        private Texture2D CaptureScreen(int width, int height)
        {
            Texture2D screenTexture = null;
            RenderTexture rt = null;
            RenderTexture currentRT = RenderTexture.active;

            try
            {
                // Create a new texture
                screenTexture = new Texture2D(width, height, TextureFormat.RGB24, false);

                // Resize the screen capture texture if needed
                if (width != Screen.width || height != Screen.height)
                {
                    // Create a temporary render texture at the desired resolution
                    rt = new RenderTexture(width, height, 24);

                    // Capture the screen to the render texture
                    Graphics.Blit(null, rt);
                    RenderTexture.active = rt;

                    // Read the screen pixels
                    screenTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    screenTexture.Apply();
                }
                else
                {
                    // Direct screen capture at same resolution
                    screenTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    screenTexture.Apply();
                }

                Texture2D result = screenTexture;
                screenTexture = null;
                return result;
            }
            finally
            {
                RenderTexture.active = currentRT;

                if (rt != null)
                    Destroy(rt);

                if (screenTexture != null)
                    Destroy(screenTexture);
            }
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
            RenderTexture originalActiveRT = RenderTexture.active;

            // Create a render texture for the camera
            RenderTexture rt = null;
            Texture2D screenTexture = null;

            // Remember the original culling mask
            int originalCullingMask = cameraToUse.cullingMask;

            try
            {
                rt = new RenderTexture(width, height, 24);
                cameraToUse.targetTexture = rt;

                // Exclude UI layer if it exists
                int uiLayerIndex = LayerMask.NameToLayer("UI");
                if (uiLayerIndex != -1)
                {
                    cameraToUse.cullingMask &= ~(1 << uiLayerIndex);
                }

                // Render the camera
                cameraToUse.Render();

                // Create a texture to store the result
                screenTexture = new Texture2D(width, height, TextureFormat.RGB24, false);

                // Read the render texture
                RenderTexture.active = rt;
                screenTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                screenTexture.Apply();

                Texture2D result = screenTexture;
                screenTexture = null;
                return result;
            }
            finally
            {
                // Restore camera settings
                cameraToUse.cullingMask = originalCullingMask;
                cameraToUse.targetTexture = originalRT;
                RenderTexture.active = originalActiveRT;

                if (rt != null)
                    Destroy(rt);

                if (screenTexture != null)
                    Destroy(screenTexture);
            }
        }

        /// <summary>
        /// Captures only UI elements
        /// This method uses a separate camera to render UI layers only
        /// </summary>
        private Texture2D CaptureUI(int width, int height)
        {
            // Create a special camera for UI capture
            GameObject tempCameraObject = null;
            RenderTexture rt = null;
            Texture2D screenTexture = null;
            RenderTexture originalActiveRT = RenderTexture.active;

            try
            {
                tempCameraObject = new GameObject("UI_Screenshot_Camera");
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
                rt = new RenderTexture(width, height, 24);
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
                screenTexture = new Texture2D(width, height, format, false);

                // Read render texture
                RenderTexture.active = rt;
                screenTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                screenTexture.Apply();

                Texture2D result = screenTexture;
                screenTexture = null;
                return result;
            }
            finally
            {
                RenderTexture.active = originalActiveRT;

                if (rt != null)
                    Destroy(rt);

                if (tempCameraObject != null)
                    Destroy(tempCameraObject);

                if (screenTexture != null)
                    Destroy(screenTexture);

                // Restore any canvas settings we changed
                RestoreCanvasSettings();
            }
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
