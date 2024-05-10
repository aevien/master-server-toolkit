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
        private Image icon;
        [SerializeField]
        private Image progressImage;
        [SerializeField]
        private Sprite defaultSprite;

        #endregion

        private void Awake()
        {
            SetProgressActive(false);
            SetAvatarSprite(null);
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
            icon.sprite = avatar;
            icon.gameObject.SetActive(icon.sprite != null);
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

#if UNITY_2019_1_OR_NEWER && !UNITY_2020_3_OR_NEWER
                if (www.isHttpError || www.isNetworkError)
                {
                    Debug.Log(www.error);
                }
                else
                {
                    var myTexture = ((DownloadHandlerTexture)www.downloadHandler).texture;
                    avatarImage.sprite = null;
                    avatarImage.sprite = Sprite.Create(myTexture, new Rect(0f, 0f, myTexture.width, myTexture.height), new Vector2(0.5f, 0.5f), 100f);
                }
#elif UNITY_2020_3_OR_NEWER
                    SetProgressActive(www.result == UnityWebRequest.Result.InProgress);

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
                        SetAvatarSprite(sprite);
                    }
#endif
                }
            }
            else
            {
                Debug.Log($"Url {url} is not valid");
            }
        }
    }
}