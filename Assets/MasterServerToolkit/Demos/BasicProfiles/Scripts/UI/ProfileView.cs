using MasterServerToolkit.Games;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.UI;

namespace MasterServerToolkit.Examples.BasicProfile
{
    public class ProfileView : UIView
    {
        private AvatarComponent avatar;
        private UIProperty displayNameUIProperty;
        private UIProperty bronzeUIProperty;
        private UIProperty silverUIProperty;
        private UIProperty goldUIProperty;

        private ProfileLoaderBehaviour profileLoader;

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

            profileLoader = FindObjectOfType<ProfileLoaderBehaviour>();
            profileLoader.OnProfileLoadedEvent.AddListener(OnProfileLoadedEventHandler);

            avatar = ChildComponent<AvatarComponent>("avatar");
            displayNameUIProperty = ChildComponent<UIProperty>("displayNameUIProperty");

            bronzeUIProperty = ChildComponent<UIProperty>("bronzeUIProperty");
            silverUIProperty = ChildComponent<UIProperty>("silverUIProperty");
            goldUIProperty = ChildComponent<UIProperty>("goldUIProperty");
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (profileLoader && profileLoader.Profile != null)
                profileLoader.Profile.OnPropertyUpdatedEvent -= Profile_OnPropertyUpdatedEvent;
        }

        private void OnProfileLoadedEventHandler()
        {
            profileLoader.Profile.OnPropertyUpdatedEvent += Profile_OnPropertyUpdatedEvent;

            foreach (var property in profileLoader.Profile.Properties)
                Profile_OnPropertyUpdatedEvent(property.Key, property.Value);
        }

        private void Profile_OnPropertyUpdatedEvent(ushort key, IObservableProperty property)
        {
            if (key == (short)ObservablePropertyCodes.DisplayName)
            {
                DisplayName = property.As<ObservableString>().Value;
            }
            else if (key == (short)ObservablePropertyCodes.Avatar)
            {
                avatar.SetAvatarUrl(property.Serialize());
            }
            else if (key == (short)ObservablePropertyCodes.Bronze)
            {
                Bronze = property.As<ObservableFloat>().Value.ToString("F2");
            }
            else if (key == (short)ObservablePropertyCodes.Silver)
            {
                Silver = property.As<ObservableFloat>().Value.ToString("F2");
            }
            else if (key == (short)ObservablePropertyCodes.Gold)
            {
                Gold = property.As<ObservableFloat>().Value.ToString("F2");
            }
        }
    }
}