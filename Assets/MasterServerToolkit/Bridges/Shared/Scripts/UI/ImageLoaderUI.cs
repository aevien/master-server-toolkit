using System;
using System.Collections;
using System.Collections.Generic;
using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;
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
    /// - Size-limited shared caching system to prevent memory leaks
    /// - Deduplication of simultaneous requests for the same cache key and URL
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
    /// // Replace an account avatar without adding every changed URL to the cache:
    /// imageLoader.Load(avatarUrl, $"player-avatar:{accountId}", null);
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
        [Tooltip("Required Image that displays the default, cached, downloaded, or manually assigned sprite. Its GameObject is hidden when the sprite is null.")]
        private Image icon;
        [SerializeField]
        [Tooltip("Optional GameObject shown while a network image request is running and hidden when the request finishes or is cancelled.")]
        private GameObject progressSpinner;
        [SerializeField]
        [Tooltip("Fallback sprite used for an empty URL and after download or URL validation errors. Leave empty to hide the target Image on fallback.")]
        private Sprite defaultSprite;

        [Header("Settings")]
        [SerializeField, Tooltip("Maximum number of remote images retained in the shared in-memory cache. Values of 0 or less disable cache retention while keeping the currently displayed image alive.")]
        private int maxCacheSize = 50;

        [Header("Events")]
        [SerializeField]
        [Tooltip("Invoked whenever the displayed sprite is assigned, including default sprites, cached sprites, downloaded sprites, and null values.")]
        private UnityEvent<Sprite> onImageLoaded;
        [SerializeField]
        [Tooltip("Invoked with an error message when the URL is invalid or the network image request fails. The fallback sprite is assigned before this event.")]
        private UnityEvent<string> onImageLoadError;

        #endregion

        private sealed class CachedImage
        {
            public Sprite Sprite { get; }
            public Texture2D Texture { get; }
            public string SourceUrl { get; }
            public int ReferenceCount { get; set; }
            public bool PendingDestroy { get; set; }
            public bool IsDestroyed { get; set; }

            public CachedImage(string sourceUrl, Sprite sprite, Texture2D texture)
            {
                SourceUrl = sourceUrl;
                Sprite = sprite;
                Texture = texture;
            }
        }

        private struct RequestIdentity : IEquatable<RequestIdentity>
        {
            public string CacheKey { get; }
            public string Url { get; }

            public RequestIdentity(string cacheKey, string url)
            {
                CacheKey = cacheKey;
                Url = url;
            }

            public bool Equals(RequestIdentity other)
            {
                return string.Equals(CacheKey, other.CacheKey, StringComparison.Ordinal)
                    && string.Equals(Url, other.Url, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is RequestIdentity other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((CacheKey != null ? CacheKey.GetHashCode() : 0) * 397)
                        ^ (Url != null ? Url.GetHashCode() : 0);
                }
            }
        }

        private sealed class CacheOrderEntry
        {
            public string CacheKey { get; }
            public CachedImage Image { get; }

            public CacheOrderEntry(string cacheKey, CachedImage image)
            {
                CacheKey = cacheKey;
                Image = image;
            }
        }

        private sealed class LoadSubscription
        {
            public ImageLoaderUI Owner { get; }
            public PendingLoad Request { get; }
            public Action<string> ErrorCallback { get; }

            public LoadSubscription(ImageLoaderUI owner, PendingLoad request,
                Action<string> errorCallback)
            {
                Owner = owner;
                Request = request;
                ErrorCallback = errorCallback;
            }
        }

        private sealed class PendingLoad
        {
            public RequestIdentity Identity { get; }
            public List<LoadSubscription> Subscribers { get; } =
                new List<LoadSubscription>();
            public int MaxCacheSize { get; set; }
            public bool IsCancelled { get; set; }
            public UnityWebRequest WebRequest { get; set; }

            public PendingLoad(RequestIdentity identity, int maxCacheSize)
            {
                Identity = identity;
                MaxCacheSize = maxCacheSize;
            }
        }

        private static readonly Dictionary<string, CachedImage> cache =
            new Dictionary<string, CachedImage>();
        private static readonly Queue<CacheOrderEntry> cacheOrder =
            new Queue<CacheOrderEntry>();
        private static readonly Dictionary<string, string> latestUrls =
            new Dictionary<string, string>();
        private static readonly Dictionary<RequestIdentity, PendingLoad> pendingLoads =
            new Dictionary<RequestIdentity, PendingLoad>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            foreach (PendingLoad pendingLoad in pendingLoads.Values)
            {
                pendingLoad.IsCancelled = true;
                pendingLoad.WebRequest?.Abort();

                foreach (LoadSubscription subscription in pendingLoad.Subscribers)
                {
                    ImageLoaderUI owner = subscription.Owner;

                    if (owner == null
                        || !ReferenceEquals(owner.activeLoadSubscription, subscription))
                    {
                        continue;
                    }

                    owner.activeLoadSubscription = null;
                    owner.SetProgressActive(false);
                }

                pendingLoad.Subscribers.Clear();
            }

            pendingLoads.Clear();
            latestUrls.Clear();

            var retainedEntries = new List<KeyValuePair<string, CachedImage>>();

            foreach (KeyValuePair<string, CachedImage> cacheEntry in cache)
            {
                CachedImage image = cacheEntry.Value;

                if (image.ReferenceCount > 0 && image.Sprite != null && image.Texture != null)
                {
                    retainedEntries.Add(cacheEntry);
                }
                else
                {
                    MarkCacheEntryForDestroy(image);
                }
            }

            cache.Clear();
            cacheOrder.Clear();

            foreach (KeyValuePair<string, CachedImage> retainedEntry in retainedEntries)
            {
                cache.Add(retainedEntry.Key, retainedEntry.Value);
                cacheOrder.Enqueue(new CacheOrderEntry(retainedEntry.Key, retainedEntry.Value));
                latestUrls[retainedEntry.Key] = retainedEntry.Value.SourceUrl;
            }
        }

        private LoadSubscription activeLoadSubscription;
        private CachedImage currentCacheEntry;

        private void Awake()
        {
            SetProgressActive(false);
            SetSpriteInternal(defaultSprite);

            // Initialize events if they weren't added in the inspector
            if (onImageLoaded == null)
                onImageLoaded = new UnityEvent<Sprite>();

            if (onImageLoadError == null)
                onImageLoadError = new UnityEvent<string>();
        }

        private void OnDisable()
        {
            CancelActiveLoad();
        }

        private void OnDestroy()
        {
            CancelActiveLoad();
            ReleaseCurrentCacheEntry();
        }

        /// <summary>
        /// Sets the activity state of the loading indicator
        /// </summary>
        /// <param name="value">Activity state</param>
        private void SetProgressActive(bool value)
        {
            if (progressSpinner != null)
                progressSpinner.SetActive(value);
        }

        /// <summary>
        /// Sets the sprite in the image component
        /// </summary>
        /// <param name="sprite">Sprite to display</param>
        public void SetSprite(Sprite sprite)
        {
            ReleaseCurrentCacheEntry();
            SetSpriteInternal(sprite);
        }

        private void SetSpriteInternal(Sprite sprite)
        {
            icon.sprite = sprite;
            icon.gameObject.SetActive(icon.sprite != null);

            // Trigger the image loaded event
            onImageLoaded?.Invoke(sprite);
        }

        private void SetCachedSprite(CachedImage cacheEntry)
        {
            if (ReferenceEquals(currentCacheEntry, cacheEntry))
            {
                SetSpriteInternal(cacheEntry.Sprite);
                return;
            }

            ReleaseCurrentCacheEntry();

            currentCacheEntry = cacheEntry;
            currentCacheEntry.ReferenceCount++;

            SetSpriteInternal(cacheEntry.Sprite);
        }

        /// <summary>
        /// Loads an image from the specified URL
        /// </summary>
        /// <param name="url">Image URL</param>
        public void Load(string url)
        {
            LoadInternal(url, url, null);
        }

        /// <summary>
        /// Loads an image and reports a failure to the caller that owns the
        /// current presentation decision.
        /// </summary>
        /// <param name="url">Image URL.</param>
        /// <param name="errorCallback">Optional callback invoked after the configured fallback sprite is assigned.</param>
        public void Load(string url, Action<string> errorCallback)
        {
            LoadInternal(url, url, errorCallback);
        }

        /// <summary>
        /// Loads an image using a stable cache identity. A changed URL replaces
        /// the previous image stored under the same key instead of adding a new
        /// cache entry.
        /// </summary>
        /// <param name="url">Image URL.</param>
        /// <param name="stableCacheKey">Stable logical image identity, for example <c>player-avatar:{accountId}</c>. An empty value falls back to the URL.</param>
        /// <param name="errorCallback">Optional callback invoked after the configured fallback sprite is assigned.</param>
        public void Load(string url, string stableCacheKey,
            Action<string> errorCallback)
        {
            LoadInternal(url, string.IsNullOrEmpty(stableCacheKey) ? url : stableCacheKey,
                errorCallback);
        }

        private void LoadInternal(string url, string cacheKey,
            Action<string> errorCallback)
        {
            if (!isActiveAndEnabled)
                return;

            CancelActiveLoad();

            if (string.IsNullOrEmpty(url))
            {
                InvalidateCacheKey(cacheKey);
                SetSprite(defaultSprite);
                return;
            }

            latestUrls[cacheKey] = url;

            if (cache.TryGetValue(cacheKey, out CachedImage cacheEntry))
            {
                if (string.Equals(cacheEntry.SourceUrl, url, StringComparison.Ordinal))
                {
                    SetCachedSprite(cacheEntry);
                    return;
                }

                RemoveCacheEntry(cacheKey, cacheEntry);
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                RemoveLatestUrlIfUnused(cacheKey, url);
                ReportLoadError($"URL {url} is not valid", errorCallback);
                return;
            }

            var identity = new RequestIdentity(cacheKey, url);

            if (!pendingLoads.TryGetValue(identity, out PendingLoad pendingLoad)
                || pendingLoad.IsCancelled)
            {
                pendingLoad = new PendingLoad(identity, maxCacheSize);
                pendingLoads[identity] = pendingLoad;

                MstTimer timer = MstTimer.Instance;

                if (timer == null)
                {
                    pendingLoads.Remove(identity);
                    RemoveLatestUrlIfUnused(cacheKey, url);
                    ReportLoadError($"Cannot load image from {url}: the runtime timer is unavailable.",
                        errorCallback);
                    return;
                }

                timer.StartCoroutine(LoadImage(pendingLoad));
            }
            else
            {
                pendingLoad.MaxCacheSize = Mathf.Max(pendingLoad.MaxCacheSize, maxCacheSize);
            }

            activeLoadSubscription = new LoadSubscription(this, pendingLoad, errorCallback);
            pendingLoad.Subscribers.Add(activeLoadSubscription);
            SetProgressActive(true);
        }

        /// <summary>
        /// Clears the image cache
        /// </summary>
        public void ClearCache()
        {
            foreach (CachedImage cacheEntry in cache.Values)
            {
                MarkCacheEntryForDestroy(cacheEntry);
            }

            cache.Clear();
            cacheOrder.Clear();
            latestUrls.Clear();
        }

        private void CancelActiveLoad()
        {
            if (activeLoadSubscription == null)
                return;

            PendingLoad pendingLoad = activeLoadSubscription.Request;
            pendingLoad.Subscribers.Remove(activeLoadSubscription);
            activeLoadSubscription = null;
            SetProgressActive(false);

            if (pendingLoad.Subscribers.Count == 0)
            {
                pendingLoad.IsCancelled = true;
                pendingLoad.WebRequest?.Abort();
            }
        }

        private void ReleaseCurrentCacheEntry()
        {
            if (currentCacheEntry == null)
                return;

            currentCacheEntry.ReferenceCount = Mathf.Max(0, currentCacheEntry.ReferenceCount - 1);

            if (currentCacheEntry.ReferenceCount == 0 && currentCacheEntry.PendingDestroy)
                DestroyCacheEntry(currentCacheEntry);

            currentCacheEntry = null;
        }

        private static void MarkCacheEntryForDestroy(CachedImage cacheEntry)
        {
            if (cacheEntry == null)
                return;

            cacheEntry.PendingDestroy = true;

            if (cacheEntry.ReferenceCount == 0)
                DestroyCacheEntry(cacheEntry);
        }

        private static void DestroyCacheEntry(CachedImage cacheEntry)
        {
            if (cacheEntry.IsDestroyed)
                return;

            cacheEntry.IsDestroyed = true;

            if (cacheEntry.Sprite != null)
                UnityEngine.Object.Destroy(cacheEntry.Sprite);

            if (cacheEntry.Texture != null)
                UnityEngine.Object.Destroy(cacheEntry.Texture);
        }

        private static void DestroySpriteAndTexture(Sprite sprite, Texture2D texture)
        {
            if (sprite != null)
                UnityEngine.Object.Destroy(sprite);

            if (texture != null)
                UnityEngine.Object.Destroy(texture);
        }

        private static CachedImage StoreLoadedImage(PendingLoad pendingLoad,
            Sprite sprite, Texture2D texture)
        {
            string cacheKey = pendingLoad.Identity.CacheKey;
            string url = pendingLoad.Identity.Url;

            if (pendingLoad.MaxCacheSize <= 0
                || !latestUrls.TryGetValue(cacheKey, out string latestUrl)
                || !string.Equals(latestUrl, url, StringComparison.Ordinal))
            {
                return new CachedImage(url, sprite, texture)
                {
                    PendingDestroy = true
                };
            }

            if (cache.TryGetValue(cacheKey, out CachedImage cacheEntry))
            {
                if (string.Equals(cacheEntry.SourceUrl, url, StringComparison.Ordinal))
                {
                    DestroySpriteAndTexture(sprite, texture);
                    return cacheEntry;
                }

                RemoveCacheEntry(cacheKey, cacheEntry);
            }

            while (cache.Count >= pendingLoad.MaxCacheSize && cacheOrder.Count > 0)
            {
                CacheOrderEntry oldest = cacheOrder.Dequeue();

                if (!cache.TryGetValue(oldest.CacheKey, out CachedImage oldestImage)
                    || !ReferenceEquals(oldest.Image, oldestImage))
                {
                    continue;
                }

                cache.Remove(oldest.CacheKey);
                MarkCacheEntryForDestroy(oldestImage);
                RemoveLatestUrlIfUnused(oldest.CacheKey, oldestImage.SourceUrl);
            }

            cacheEntry = new CachedImage(url, sprite, texture);
            cache.Add(cacheKey, cacheEntry);
            cacheOrder.Enqueue(new CacheOrderEntry(cacheKey, cacheEntry));

            return cacheEntry;
        }

        private static void InvalidateCacheKey(string cacheKey)
        {
            if (string.IsNullOrEmpty(cacheKey))
                return;

            latestUrls.Remove(cacheKey);

            if (cache.TryGetValue(cacheKey, out CachedImage cacheEntry))
                RemoveCacheEntry(cacheKey, cacheEntry);
        }

        private static void RemoveCacheEntry(string cacheKey, CachedImage expectedEntry)
        {
            if (!cache.TryGetValue(cacheKey, out CachedImage currentEntry)
                || !ReferenceEquals(currentEntry, expectedEntry))
            {
                return;
            }

            cache.Remove(cacheKey);
            RemoveFromCacheOrder(cacheKey, currentEntry);
            MarkCacheEntryForDestroy(currentEntry);
            RemoveLatestUrlIfUnused(cacheKey, currentEntry.SourceUrl);
        }

        private static void RemoveFromCacheOrder(string cacheKey,
            CachedImage expectedEntry)
        {
            int entriesCount = cacheOrder.Count;

            for (int i = 0; i < entriesCount; i++)
            {
                CacheOrderEntry orderEntry = cacheOrder.Dequeue();

                if (string.Equals(orderEntry.CacheKey, cacheKey, StringComparison.Ordinal)
                    && ReferenceEquals(orderEntry.Image, expectedEntry))
                {
                    continue;
                }

                cacheOrder.Enqueue(orderEntry);
            }
        }

        private static void RemoveLatestUrlIfUnused(string cacheKey, string url)
        {
            if (cache.ContainsKey(cacheKey)
                || !latestUrls.TryGetValue(cacheKey, out string latestUrl)
                || !string.Equals(latestUrl, url, StringComparison.Ordinal))
            {
                return;
            }

            foreach (RequestIdentity pendingIdentity in pendingLoads.Keys)
            {
                if (string.Equals(pendingIdentity.CacheKey, cacheKey,
                    StringComparison.Ordinal))
                {
                    return;
                }
            }

            latestUrls.Remove(cacheKey);
        }

        private static IEnumerator LoadImage(PendingLoad pendingLoad)
        {
            string loadError = null;
            Texture2D texture = null;
            Sprite sprite = null;

            using (UnityWebRequest webRequest =
                UnityWebRequestTexture.GetTexture(pendingLoad.Identity.Url))
            {
                pendingLoad.WebRequest = webRequest;
                yield return webRequest.SendWebRequest();
                pendingLoad.WebRequest = null;

                if (!pendingLoad.IsCancelled)
                {
#if UNITY_2019_1_OR_NEWER && !UNITY_2020_3_OR_NEWER
                    if (webRequest.isHttpError || webRequest.isNetworkError)
                    {
                        loadError = webRequest.error;
                    }
                    else
                    {
                        texture = ((DownloadHandlerTexture)webRequest.downloadHandler).texture;
                    }
#elif UNITY_2020_3_OR_NEWER
                    if (webRequest.result == UnityWebRequest.Result.Success)
                    {
                        texture = ((DownloadHandlerTexture)webRequest.downloadHandler).texture;
                    }
                    else
                    {
                        loadError = webRequest.error;
                    }
#endif
                }
            }

            RemovePendingLoad(pendingLoad);

            if (pendingLoad.IsCancelled)
            {
                RemoveLatestUrlIfUnused(pendingLoad.Identity.CacheKey,
                    pendingLoad.Identity.Url);
                yield break;
            }

            if (texture == null)
            {
                RemoveLatestUrlIfUnused(pendingLoad.Identity.CacheKey,
                    pendingLoad.Identity.Url);
                CompleteLoadWithError(pendingLoad,
                    string.IsNullOrEmpty(loadError) ? "The downloaded image is empty." : loadError);
                yield break;
            }

            sprite = Sprite.Create(texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f);

            CachedImage cacheEntry = StoreLoadedImage(pendingLoad, sprite, texture);
            CompleteLoadSuccessfully(pendingLoad, cacheEntry);
            RemoveLatestUrlIfUnused(pendingLoad.Identity.CacheKey,
                pendingLoad.Identity.Url);
        }

        private static void RemovePendingLoad(PendingLoad pendingLoad)
        {
            if (pendingLoads.TryGetValue(pendingLoad.Identity, out PendingLoad currentLoad)
                && ReferenceEquals(currentLoad, pendingLoad))
            {
                pendingLoads.Remove(pendingLoad.Identity);
            }
        }

        private static void CompleteLoadSuccessfully(PendingLoad pendingLoad,
            CachedImage cacheEntry)
        {
            LoadSubscription[] subscribers = pendingLoad.Subscribers.ToArray();
            pendingLoad.Subscribers.Clear();

            // Keep an uncached or concurrently invalidated image alive while subscriber
            // callbacks are allowed to clear the cache or destroy their UI objects.
            cacheEntry.ReferenceCount++;

            foreach (LoadSubscription subscription in subscribers)
            {
                ImageLoaderUI owner = subscription.Owner;

                if (owner == null
                    || !owner.isActiveAndEnabled
                    || !ReferenceEquals(owner.activeLoadSubscription, subscription))
                {
                    continue;
                }

                owner.activeLoadSubscription = null;
                owner.SetProgressActive(false);
                owner.SetCachedSprite(cacheEntry);
            }

            cacheEntry.ReferenceCount = Mathf.Max(0, cacheEntry.ReferenceCount - 1);

            if (cacheEntry.ReferenceCount == 0 && cacheEntry.PendingDestroy)
                DestroyCacheEntry(cacheEntry);
        }

        private static void CompleteLoadWithError(PendingLoad pendingLoad,
            string errorMessage)
        {
            LoadSubscription[] subscribers = pendingLoad.Subscribers.ToArray();
            pendingLoad.Subscribers.Clear();
            Logs.Error($"Failed to load image from '{pendingLoad.Identity.Url}': {errorMessage}");

            foreach (LoadSubscription subscription in subscribers)
            {
                ImageLoaderUI owner = subscription.Owner;

                if (owner == null
                    || !owner.isActiveAndEnabled
                    || !ReferenceEquals(owner.activeLoadSubscription, subscription))
                {
                    continue;
                }

                owner.activeLoadSubscription = null;
                owner.SetProgressActive(false);
                owner.SetSprite(owner.defaultSprite);
                owner.onImageLoadError?.Invoke(errorMessage);
                subscription.ErrorCallback?.Invoke(errorMessage);
            }
        }

        private void ReportLoadError(string errorMessage,
            Action<string> errorCallback)
        {
            SetProgressActive(false);
            SetSprite(defaultSprite);
            Logs.Error(errorMessage);
            onImageLoadError?.Invoke(errorMessage);
            errorCallback?.Invoke(errorMessage);
        }
    }
}
