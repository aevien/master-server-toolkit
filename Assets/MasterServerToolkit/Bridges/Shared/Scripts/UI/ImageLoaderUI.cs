using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace MasterServerToolkit.Bridges
{
    /// <summary>
    /// Asynchronous network image loader component with caching for Unity UI
    /// </summary>
    /// <remarks>
    /// The ImageLoaderUI component provides functionality to load image sprites from remote URLs
    /// and display them in Unity UI components. Key features include:
    /// 
    /// - Asynchronous loading with visual loading indicator
    /// - Automatic sprite conversion with proper pivot settings
    /// - Size-limited LRU caching system to prevent memory leaks
    /// - Error handling with fallback to default sprite
    /// - Event callbacks for load success and failure
    /// - Compatible with multiple Unity versions
    /// 
    /// This component is designed to be attached to GameObject containing a UI Image component.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Basic usage example:
    /// imageLoader.Load("https://avatar.iran.liara.run/public");
    /// 
    /// // To listen for loading events:
    /// imageLoader.onImageLoaded.AddListener(sprite => Debug.Log("Image loaded successfully"));
    /// imageLoader.onImageLoadError.AddListener(error => Debug.LogWarning($"Failed to load: {error}"));
    /// </code>
    /// </example>
    /// <seealso cref="UnityEngine.UI.Image"/>
    /// <seealso cref="UnityEngine.Networking.UnityWebRequest"/>
    public class ImageLoaderUI : MonoBehaviour
    {
        #region INSPECTOR

        [Header("Components"), SerializeField]
        private Image icon;
        [SerializeField]
        private Image progressImage;
        [SerializeField]
        private Sprite defaultSprite;

        [Header("Settings")]
        [SerializeField, Tooltip("Maximum number of images in the cache")]
        private int maxCacheSize = 50;

        [Header("Events")]
        [SerializeField]
        private UnityEvent<Sprite> onImageLoaded;
        [SerializeField]
        private UnityEvent<string> onImageLoadError;

        #endregion

        // Dictionary for caching loaded images
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        // Queue for tracking the order of loading (for implementing FIFO when reaching the cache limit)
        private static readonly Queue<string> cacheOrder = new Queue<string>();

        // Current active loading process
        private Coroutine activeLoadCoroutine;

        private void Awake()
        {
            SetProgressActive(false);
            SetSprite(defaultSprite);

            // Initialize events if they weren't added in the inspector
            if (onImageLoaded == null)
                onImageLoaded = new UnityEvent<Sprite>();

            if (onImageLoadError == null)
                onImageLoadError = new UnityEvent<string>();
        }

        private void Update()
        {
            if (progressImage != null && progressImage.isActiveAndEnabled)
                progressImage.transform.Rotate(0, 0, -200f * Time.deltaTime);
        }

        private void OnDisable()
        {
            // Stop loading when the component is deactivated
            if (activeLoadCoroutine != null)
            {
                StopCoroutine(activeLoadCoroutine);
                activeLoadCoroutine = null;
                SetProgressActive(false);
            }
        }

        /// <summary>
        /// Sets the activity state of the loading indicator
        /// </summary>
        /// <param name="value">Activity state</param>
        private void SetProgressActive(bool value)
        {
            if (progressImage != null)
                progressImage.gameObject.SetActive(value);
        }

        /// <summary>
        /// Sets the sprite in the image component
        /// </summary>
        /// <param name="sprite">Sprite to display</param>
        public void SetSprite(Sprite sprite)
        {
            icon.sprite = sprite;
            icon.gameObject.SetActive(icon.sprite != null);

            // Trigger the image loaded event
            onImageLoaded?.Invoke(sprite);
        }

        /// <summary>
        /// Loads an image from the specified URL
        /// </summary>
        /// <param name="url">Image URL</param>
        public void Load(string url)
        {
            if (!isActiveAndEnabled)
                return;

            // Cancel previous loading
            if (activeLoadCoroutine != null)
            {
                StopCoroutine(activeLoadCoroutine);
                activeLoadCoroutine = null;
            }

            if (string.IsNullOrEmpty(url))
            {
                SetSprite(defaultSprite);
            }
            else if (cache.ContainsKey(url))
            {
                SetSprite(cache[url]);
            }
            else
            {
                activeLoadCoroutine = StartCoroutine(StartLoadAvatarImage(url));
            }
        }

        /// <summary>
        /// Clears the image cache
        /// </summary>
        public void ClearCache()
        {
            cache.Clear();
            cacheOrder.Clear();
        }

        /// <summary>
        /// Coroutine for loading an image
        /// </summary>
        /// <param name="url">Image URL</param>
        private IEnumerator StartLoadAvatarImage(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out Uri validUri))
            {
                SetProgressActive(true);

                using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(url))
                {
                    yield return www.SendWebRequest();

                    // Turn off progress indicator only after the request completes
                    SetProgressActive(false);

#if UNITY_2019_1_OR_NEWER && !UNITY_2020_3_OR_NEWER
                    if (www.isHttpError || www.isNetworkError)
                    {
                        SetSprite(defaultSprite);
                        Debug.Log($"Error loading image from {url}: {www.error}");
                        onImageLoadError?.Invoke(www.error);
                    }
                    else
                    {
                        var myTexture = ((DownloadHandlerTexture)www.downloadHandler).texture;
                        var sprite = Sprite.Create(myTexture, new Rect(0f, 0f, myTexture.width, myTexture.height), new Vector2(0.5f, 0.5f), 100f);
                        SetSprite(sprite);
                        
                        // Check if this URL is already in the cache before adding
                        if (!cache.ContainsKey(url))
                        {
                            // Check cache size and remove old entries if necessary
                            if (cache.Count >= maxCacheSize && cacheOrder.Count > 0)
                            {
                                string oldestUrl = cacheOrder.Dequeue();
                                cache.Remove(oldestUrl);
                            }
                            
                            cache.Add(url, sprite);
                            cacheOrder.Enqueue(url);
                        }
                    }
#elif UNITY_2020_3_OR_NEWER
                    if (www.result == UnityWebRequest.Result.ConnectionError
                        || www.result == UnityWebRequest.Result.ProtocolError
                        || www.result == UnityWebRequest.Result.DataProcessingError)
                    {
                        SetSprite(defaultSprite);
                        Debug.Log($"Error loading image from {url}: {www.error}");
                        onImageLoadError?.Invoke(www.error);
                    }
                    else if (www.result == UnityWebRequest.Result.Success)
                    {
                        var myTexture = ((DownloadHandlerTexture)www.downloadHandler).texture;
                        var sprite = Sprite.Create(myTexture, new Rect(0f, 0f, myTexture.width, myTexture.height), new Vector2(0.5f, 0.5f), 100f);
                        SetSprite(sprite);

                        // Check cache size and remove old entries if necessary
                        if (!cache.ContainsKey(url))
                        {
                            if (cache.Count >= maxCacheSize && cacheOrder.Count > 0)
                            {
                                string oldestUrl = cacheOrder.Dequeue();
                                cache.Remove(oldestUrl);
                            }

                            cache.Add(url, sprite);
                            cacheOrder.Enqueue(url);
                        }
                    }
#endif
                }
            }
            else
            {
                SetSprite(defaultSprite);
                string errorMessage = $"URL {url} is not valid";
                Debug.LogError(errorMessage);
                onImageLoadError?.Invoke(errorMessage);
            }

            activeLoadCoroutine = null;
        }
    }
}