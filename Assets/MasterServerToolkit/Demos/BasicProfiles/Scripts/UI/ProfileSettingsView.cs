using MasterServerToolkit.MasterServer;
using MasterServerToolkit.UI;
using TMPro;
using UnityEngine;

namespace MasterServerToolkit.Examples.BasicProfile
{
    public class ProfileSettingsView : UIView
    {
        #region INSPECTOR

        [Header("Components"), SerializeField]
        private TMP_InputField displayNameInputField;
        [SerializeField]
        private TMP_InputField avatarUrlInputField;

        #endregion

        private DemoProfilesBehaviour profileBehaviour;

        protected override void Start()
        {
            base.Start();

            profileBehaviour = FindObjectOfType<DemoProfilesBehaviour>();
            profileBehaviour.OnProfileLoadedEvent.AddListener(OnProfileLoadedEventHandler);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (profileBehaviour && profileBehaviour.HasProfile)
                profileBehaviour.Profile.OnPropertyUpdatedEvent -= ProfilesManager_OnPropertyUpdatedEvent;
        }

        public void Submit()
        {
            var data = new MstProperties();
            data.Set("displayName", displayNameInputField.text);
            data.Set("avatarUrl", avatarUrlInputField.text);

            profileBehaviour.UpdateProfile(data);
        }

        private void OnProfileLoadedEventHandler()
        {
            profileBehaviour.Profile.OnPropertyUpdatedEvent += ProfilesManager_OnPropertyUpdatedEvent;

            foreach (var property in profileBehaviour.Profile.Properties)
                ProfilesManager_OnPropertyUpdatedEvent(property.Key, property.Value);
        }

        private void ProfilesManager_OnPropertyUpdatedEvent(ushort key, IObservableProperty property)
        {
            if (key == ProfilePropertyOpCodes.displayName)
                displayNameInputField.text = property.Serialize();
            else if (key == ProfilePropertyOpCodes.avatarUrl)
                avatarUrlInputField.text = property.Serialize();
        }
    }
}
