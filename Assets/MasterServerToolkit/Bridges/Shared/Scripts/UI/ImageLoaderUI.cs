using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace MasterServerToolkit.Bridges
{
    public class ImageLoaderUI : MonoBehaviour
    {
        #region INSPECTOR

        [Header("Components"), SerializeField]
        private Image icon;
        [SerializeField]
        private Image progressImage;
        [SerializeField]
        private Sprite defaultSprite;

        #endregion

        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        private void Awake()
        {
            SetProgressActive(false);
            SetSprite(null);
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
        /// <param name="sprite"></param>
        public void SetSprite(Sprite sprite)
        {
            icon.sprite = sprite;
            icon.gameObject.SetActive(icon.sprite != null);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="url"></param>
        public void Load(string url)
        {
            if (isActiveAndEnabled)
            {
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
                    StopAllCoroutines();
                    StartCoroutine(StartLoadAvatarImage(url));
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

                    SetProgressActive(www.result == UnityWebRequest.Result.InProgress);

#if UNITY_2019_1_OR_NEWER && !UNITY_2020_3_OR_NEWER
                    if (www.isHttpError || www.isNetworkError)
                    {
                        SetSprite(defaultSprite);
                        Debug.Log(www.error);
                    }
                    else
                    {
                        var myTexture = ((DownloadHandlerTexture)www.downloadHandler).texture;
                        var sprite = Sprite.Create(myTexture, new Rect(0f, 0f, myTexture.width, myTexture.height), new Vector2(0.5f, 0.5f), 100f);
                        SetSprite(sprite);
                        cache.Add(url, sprite);
                    }
#elif UNITY_2020_3_OR_NEWER
                    if (www.result == UnityWebRequest.Result.ProtocolError
                        || www.result == UnityWebRequest.Result.ProtocolError
                         || www.result == UnityWebRequest.Result.DataProcessingError)
                    {
                        SetSprite(defaultSprite);
                        Debug.Log(www.error);
                    }
                    else if (www.result == UnityWebRequest.Result.Success)
                    {
                        var myTexture = ((DownloadHandlerTexture)www.downloadHandler).texture;
                        var sprite = Sprite.Create(myTexture, new Rect(0f, 0f, myTexture.width, myTexture.height), new Vector2(0.5f, 0.5f), 100f);
                        SetSprite(sprite);

                        if (cache.ContainsKey(url) == false)
                            cache.Add(url, sprite);
                    }
#endif
                }
            }
            else
            {
                SetSprite(defaultSprite);
                Debug.LogError($"Url {url} is not valid");
            }
        }
    }
}