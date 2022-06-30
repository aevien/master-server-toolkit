using MasterServerToolkit.Games;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.UI;
using TMPro;

namespace MasterServerToolkit.Examples.BasicProfile
{
    public class ProfileSettingsView : UIView
    {
        private TMP_InputField displayNameInputField;
        private TMP_InputField avatarUrlInputField;

        private ProfileLoaderBehaviour profileLoader;

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

            profileLoader = FindObjectOfType<ProfileLoaderBehaviour>();
            profileLoader.OnProfileLoadedEvent.AddListener(OnProfileLoadedEventHandler);

            displayNameInputField = ChildComponent<TMP_InputField>("displayNameInputField");
            avatarUrlInputField = ChildComponent<TMP_InputField>("avatarUrlInputField");
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (profileLoader && profileLoader.Profile != null)
                profileLoader.Profile.OnPropertyUpdatedEvent -= ProfilesManager_OnPropertyUpdatedEvent;
        }

        private void OnProfileLoadedEventHandler()
        {
            profileLoader.Profile.OnPropertyUpdatedEvent += ProfilesManager_OnPropertyUpdatedEvent;

            foreach (var property in profileLoader.Profile.Properties)
                ProfilesManager_OnPropertyUpdatedEvent(property.Key, property.Value);
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
