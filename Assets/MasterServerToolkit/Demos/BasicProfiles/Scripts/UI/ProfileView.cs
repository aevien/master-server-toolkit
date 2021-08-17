using Aevien.UI;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.UI;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace MasterServerToolkit.Examples.BasicProfile
{
    public class ProfileView : UIView
    {
        private Image avatarImage;
        private DemoProfilesBehaviour profilesManager;
        private UIProperty displayNameUIProperty;
        private UIProperty bronzeUIProperty;
        private UIProperty silverUIProperty;
        private UIProperty goldUIProperty;

        public string DisplayName
        {
            get
            {
                return displayNameUIProperty ? displayNameUIProperty.Lable : string.Empty;
            }

            set
            {
                if (displayNameUIProperty)
                    displayNameUIProperty.Lable = value;
            }
        }

        public string Bronze
        {
            get
            {
                return bronzeUIProperty ? bronzeUIProperty.Lable : string.Empty;
            }

            set
            {
                if (bronzeUIProperty)
                    bronzeUIProperty.Lable = value;
            }
        }

        public string Silver
        {
            get
            {
                return silverUIProperty ? silverUIProperty.Lable : string.Empty;
            }

            set
            {
                if (silverUIProperty)
                    silverUIProperty.Lable = value;
            }
        }

        public string Gold
        {
            get
            {
                return goldUIProperty ? goldUIProperty.Lable : string.Empty;
            }

            set
            {
                if (goldUIProperty)
                    goldUIProperty.Lable = value;
            }
        }

        protected override void Start()
        {
            base.Start();

            if (!profilesManager)
            {
                profilesManager = FindObjectOfType<DemoProfilesBehaviour>();
            }

            profilesManager.OnPropertyUpdatedEvent += ProfilesManager_OnPropertyUpdatedEvent;

            avatarImage = ChildComponent<Image>("avatarImage");
            displayNameUIProperty = ChildComponent<UIProperty>("displayNameUIProperty");

            bronzeUIProperty = ChildComponent<UIProperty>("bronzeUIProperty");
            silverUIProperty = ChildComponent<UIProperty>("silverUIProperty");
            goldUIProperty = ChildComponent<UIProperty>("goldUIProperty");
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (profilesManager)
            {
                profilesManager.OnPropertyUpdatedEvent -= ProfilesManager_OnPropertyUpdatedEvent;
            }
        }

        private void ProfilesManager_OnPropertyUpdatedEvent(short key, IObservableProperty property)
        {
            if (key == (short)ObservablePropertiyCodes.DisplayName)
            {
                DisplayName = property.CastTo<ObservableString>().GetValue();
            }
            else if (key == (short)ObservablePropertiyCodes.Avatar)
            {
                LoadAvatarImage(property.Serialize());
            }
            else if (key == (short)ObservablePropertiyCodes.Bronze)
            {
                Bronze = property.CastTo<ObservableFloat>().GetValue().ToString("F2");
            }
            else if (key == (short)ObservablePropertiyCodes.Silver)
            {
                Silver = property.CastTo<ObservableFloat>().GetValue().ToString("F2");
            }
            else if (key == (short)ObservablePropertiyCodes.Gold)
            {
                Gold = property.CastTo<ObservableFloat>().GetValue().ToString("F2");
            }
        }

        private void LoadAvatarImage(string url)
        {
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
                    Debug.Log(www.error);
                }
                else if (www.result == UnityWebRequest.Result.Success)
                {
                    var myTexture = ((DownloadHandlerTexture)www.downloadHandler).texture;
                    avatarImage.sprite = null;
                    avatarImage.sprite = Sprite.Create(myTexture, new Rect(0f, 0f, myTexture.width, myTexture.height), new Vector2(0.5f, 0.5f), 100f);
                }
#endif
            }
        }
    }
}