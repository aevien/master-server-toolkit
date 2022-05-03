using MasterServerToolkit.Games;
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
        private AvatarComponent avatar;
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

        protected override void Awake()
        {
            base.Awake();
            Mst.Client.Profiles.OnProfileLoadedEvent += Profiles_OnProfileLoadedEvent;
        }

        private void Profiles_OnProfileLoadedEvent(ObservableProfile profile)
        {
            profile.OnPropertyUpdatedEvent -= Profile_OnPropertyUpdatedEvent;
            profile.OnPropertyUpdatedEvent += Profile_OnPropertyUpdatedEvent;

            foreach (var prop in profile.Properties)
                Profile_OnPropertyUpdatedEvent(prop.Key, prop.Value);
        }

        protected override void Start()
        {
            base.Start();

            avatar = ChildComponent<AvatarComponent>("avatar");
            displayNameUIProperty = ChildComponent<UIProperty>("displayNameUIProperty");

            bronzeUIProperty = ChildComponent<UIProperty>("bronzeUIProperty");
            silverUIProperty = ChildComponent<UIProperty>("silverUIProperty");
            goldUIProperty = ChildComponent<UIProperty>("goldUIProperty");
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Mst.Client.Profiles.OnProfileLoadedEvent -= Profiles_OnProfileLoadedEvent;
        }

        private void Profile_OnPropertyUpdatedEvent(ushort key, IObservableProperty property)
        {
            if (key == (short)ObservablePropertyCodes.DisplayName)
            {
                DisplayName = property.As<ObservableString>().Value();
            }
            else if (key == (short)ObservablePropertyCodes.Avatar)
            {
                avatar.SetAvatarUrl(property.Serialize());
            }
            else if (key == (short)ObservablePropertyCodes.Bronze)
            {
                Bronze = property.As<ObservableFloat>().Value().ToString("F2");
            }
            else if (key == (short)ObservablePropertyCodes.Silver)
            {
                Silver = property.As<ObservableFloat>().Value().ToString("F2");
            }
            else if (key == (short)ObservablePropertyCodes.Gold)
            {
                Gold = property.As<ObservableFloat>().Value().ToString("F2");
            }
        }
    }
}