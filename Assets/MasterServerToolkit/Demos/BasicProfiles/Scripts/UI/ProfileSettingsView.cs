using MasterServerToolkit.MasterServer;
using MasterServerToolkit.UI;
using TMPro;

namespace MasterServerToolkit.Examples.BasicProfile
{
    public class ProfileSettingsView : UIView
    {
        private DemoProfilesBehaviour profilesManager;
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

        protected override void Start()
        {
            base.Start();

            if (!profilesManager)
            {
                profilesManager = FindObjectOfType<DemoProfilesBehaviour>();
            }

            profilesManager.OnPropertyUpdatedEvent += ProfilesManager_OnPropertyUpdatedEvent;
            profilesManager.OnProfileLoadedEvent.AddListener(ProfilesManager_OnProfileLoadedEvent);

            displayNameInputField = ChildComponent<TMP_InputField>("displayNameInputField");
            avatarUrlInputField = ChildComponent<TMP_InputField>("avatarUrlInputField");
        }

        private void ProfilesManager_OnProfileLoadedEvent()
        {
            DisplayName = profilesManager.Profile.GetProperty<ObservableString>((short)ObservablePropertiyCodes.DisplayName).Serialize();
            AvatarUrl = profilesManager.Profile.GetProperty<ObservableString>((short)ObservablePropertiyCodes.Avatar).Serialize();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (profilesManager)
            {
                profilesManager.OnPropertyUpdatedEvent += ProfilesManager_OnPropertyUpdatedEvent;
                profilesManager.OnProfileLoadedEvent.RemoveListener(ProfilesManager_OnProfileLoadedEvent);
            }
        }

        private void ProfilesManager_OnPropertyUpdatedEvent(short key, IObservableProperty property)
        {
            if (key == (short)ObservablePropertiyCodes.DisplayName)
            {
                DisplayName = property.Serialize();
            }
            else if (key == (short)ObservablePropertiyCodes.Avatar)
            {
                AvatarUrl = property.Serialize();
            }
        }
    }
}
