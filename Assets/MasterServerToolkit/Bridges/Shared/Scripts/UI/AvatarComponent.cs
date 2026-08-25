using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace MasterServerToolkit.Bridges
{
    public class AvatarComponent : MonoBehaviour
    {
        #region INSPECTOR

        [Header("Components"), SerializeField]
        [Tooltip("Required Image that displays the assigned or downloaded avatar. The GameObject is hidden when no sprite is available.")]
        private Image icon;
        [SerializeField]
        [Tooltip("Optional Image used as a rotating loading indicator while an avatar download is in progress.")]
        private Image progressImage;
        [SerializeField]
        [Tooltip("Fallback sprite shown when the avatar URL is empty, invalid, or cannot be downloaded. Leave empty to hide the avatar Image on failure.")]
        private Sprite defaultSprite;

        #endregion

        private Sprite downloadedAvatarSprite;
        private Texture2D downloadedAvatarTexture;
        private Coroutine activeLoadCoroutine;

        private void Awake()
        {
            SetProgressActive(false);
            SetAvatarSprite(null);
        }

        private void OnDisable()
        {
            if (activeLoadCoroutine != null)
            {
                StopCoroutine(activeLoadCoroutine);
                activeLoadCoroutine = null;
                SetProgressActive(false);
            }
        }

        private void OnDestroy()
        {
            ReleaseDownloadedAvatar();
        }

        private void Update()
        {
            if (progressImage != null && progressImage.isActiveAndEnabled)
                progressImage.transform.Rotate(0, 0, -200f * Time.deltaTime);
        }

        private void SetProgressActive(bool value)
        {
            if (progressImage != null)
                progressImage.gameObject.SetActive(value);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="avatar"></param>
        public void SetAvatarSprite(Sprite avatar)
        {
            if (!ReferenceEquals(avatar, downloadedAvatarSprite))
                ReleaseDownloadedAvatar();

            icon.sprite = avatar;
            icon.gameObject.SetActive(icon.sprite != null);
        }

        private void SetDownloadedAvatarSprite(Sprite avatar, Texture2D texture)
        {
            ReleaseDownloadedAvatar();

            downloadedAvatarSprite = avatar;
            downloadedAvatarTexture = texture;

            icon.sprite = avatar;
            icon.gameObject.SetActive(icon.sprite != null);
        }

        private void ReleaseDownloadedAvatar()
        {
            if (downloadedAvatarSprite != null)
            {
                Destroy(downloadedAvatarSprite);
                downloadedAvatarSprite = null;
            }

            if (downloadedAvatarTexture != null)
            {
                Destroy(downloadedAvatarTexture);
                downloadedAvatarTexture = null;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="url"></param>
        public void SetAvatarUrl(string url)
        {
            if (isActiveAndEnabled)
            {
                if (string.IsNullOrEmpty(url))
                {
                    SetAvatarSprite(defaultSprite);
                }
                else
                {
                    if (activeLoadCoroutine != null)
                    {
                        StopCoroutine(activeLoadCoroutine);
                        activeLoadCoroutine = null;
                    }

                    activeLoadCoroutine = StartCoroutine(StartLoadAvatarImage(url));
                }
            }
        }

        private IEnumerator StartLoadAvatarImage(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out Uri balidUri))
            {
                SetProgressActive(true);

                using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(url))
                {
                    yield return www.SendWebRequest();
                    SetProgressActive(false);

#if UNITY_2019_1_OR_NEWER && !UNITY_2020_3_OR_NEWER
                if (www.isHttpError || www.isNetworkError)
                {
                    SetAvatarSprite(defaultSprite);
                    Debug.Log(www.error);
                }
                else
                {
                    var myTexture = ((DownloadHandlerTexture)www.downloadHandler).texture;
                    var sprite = Sprite.Create(myTexture, new Rect(0f, 0f, myTexture.width, myTexture.height), new Vector2(0.5f, 0.5f), 100f);
                    SetDownloadedAvatarSprite(sprite, myTexture);
                }
#elif UNITY_2020_3_OR_NEWER
                    if (www.result == UnityWebRequest.Result.ProtocolError
                        || www.result == UnityWebRequest.Result.ProtocolError
                         || www.result == UnityWebRequest.Result.DataProcessingError)
                    {
                        SetAvatarSprite(defaultSprite);
                        Debug.Log(www.error);
                    }
                    else if (www.result == UnityWebRequest.Result.Success)
                    {
                        var myTexture = ((DownloadHandlerTexture)www.downloadHandler).texture;
                        var sprite = Sprite.Create(myTexture, new Rect(0f, 0f, myTexture.width, myTexture.height), new Vector2(0.5f, 0.5f), 100f);
                        SetDownloadedAvatarSprite(sprite, myTexture);
                    }
#endif
                }
            }
            else
            {
                SetAvatarSprite(defaultSprite);
                Debug.Log($"Url {url} is not valid");
            }

            activeLoadCoroutine = null;
        }
    }
}
