using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace MasterServerToolkit.Games
{
    public class AvatarComponent : MonoBehaviour
    {
        #region INSPECTOR

        [Header("Components"), SerializeField]
        private Image icon;

        #endregion

        private void Awake()
        {
            SetAvatarSprite(null);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="avatar"></param>
        public void SetAvatarSprite(Sprite avatar)
        {
            icon.sprite = avatar;
            icon.gameObject.SetActive(icon.sprite);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="url"></param>
        public void SetAvatarUrl(string url)
        {
            StopAllCoroutines();
            StartCoroutine(StartLoadAvatarImage(url));
        }

        private IEnumerator StartLoadAvatarImage(string url)
        {
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
                if (www.result == UnityWebRequest.Result.ProtocolError
                    || www.result == UnityWebRequest.Result.ProtocolError
                     || www.result == UnityWebRequest.Result.DataProcessingError)
                {
                    SetAvatarSprite(null);
                    Debug.Log(www.error);
                }
                else if (www.result == UnityWebRequest.Result.Success)
                {
                    var myTexture = ((DownloadHandlerTexture)www.downloadHandler).texture;
                    SetAvatarSprite(Sprite.Create(myTexture, new Rect(0f, 0f, myTexture.width, myTexture.height), new Vector2(0.5f, 0.5f), 100f));
                }
#endif
            }
        }
    }
}