using MasterServerToolkit.MasterServer;
using MasterServerToolkit.UI;
using TMPro;

namespace MasterServerToolkit.Examples.BasicProfile
{
    public class ProfileSettingsView : UIView
    {
        private TMP_InputField displayNameInputField;
        private TMP_InputField avatarUrlInputField;

        public string DisplayName
        {
            get
            {
                return displayNameInputField != null ? displayNameInputField.text : string.Empty;
            }

            set
            {
                if (displayNameInputField)
                    displayNameInputField.text = value;
            }
        }

        public string AvatarUrl
        {
            get
            {
                return avatarUrlInputField != null ? avatarUrlInputField.text : string.Empty;
            }

            set
            {
                if (avatarUrlInputField)
                    avatarUrlInputField.text = value;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            Mst.Client.Profiles.OnProfileLoadedEvent += Profiles_OnProfileLoadedEvent;
        }

        protected override void Start()
        {
            base.Start();
            displayNameInputField = ChildComponent<TMP_InputField>("displayNameInputField");
            avatarUrlInputField = ChildComponent<TMP_InputField>("avatarUrlInputField");
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Mst.Client.Profiles.OnProfileLoadedEvent -= Profiles_OnProfileLoadedEvent;
        }

        private void Profiles_OnProfileLoadedEvent(ObservableProfile profile)
        {
            profile.OnPropertyUpdatedEvent -= ProfilesManager_OnPropertyUpdatedEvent;
            profile.OnPropertyUpdatedEvent += ProfilesManager_OnPropertyUpdatedEvent;

            DisplayName = profile.Get<ObservableString>((ushort)ObservablePropertyCodes.DisplayName).Serialize();
            AvatarUrl = profile.Get<ObservableString>((ushort)ObservablePropertyCodes.Avatar).Serialize();
        }

        private void ProfilesManager_OnPropertyUpdatedEvent(ushort key, IObservableProperty property)
        {
            if (key == (short)ObservablePropertyCodes.DisplayName)
            {
                DisplayName = property.Serialize();
            }
            else if (key == (short)ObservablePropertyCodes.Avatar)
            {
                AvatarUrl = property.Serialize();
            }
        }
    }
}
