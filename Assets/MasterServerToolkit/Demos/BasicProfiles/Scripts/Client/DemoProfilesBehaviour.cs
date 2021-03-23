using MasterServerToolkit.Games;
using MasterServerToolkit.Networking;
using MasterServerToolkit.UI;
using System;
using System.Collections.Generic;
using UnityEngine.Events;

namespace MasterServerToolkit.MasterServer.Examples.BasicProfile
{
    public class DemoProfilesBehaviour : ProfilesBehaviour
    {
        private ProfileView profileView;
        private ProfileSettingsView profileSettingsView;

        public event Action<short, IObservableProperty> OnPropertyUpdatedEvent;
        public UnityEvent OnProfileSavedEvent;

        protected override void OnInitialize()
        {
            profileView = ViewsManager.GetView<ProfileView>("ProfileView");
            profileSettingsView = ViewsManager.GetView<ProfileSettingsView>("ProfileSettingsView");

            Profile = new ObservableProfile
            {
                new ObservableString((short)ObservablePropertiyCodes.DisplayName),
                new ObservableString((short)ObservablePropertiyCodes.Avatar),
                new ObservableFloat((short)ObservablePropertiyCodes.Bronze),
                new ObservableFloat((short)ObservablePropertiyCodes.Silver),
                new ObservableFloat((short)ObservablePropertiyCodes.Gold)
            };

            Profile.OnPropertyUpdatedEvent += OnPropertyUpdatedEventHandler;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            Profile.OnPropertyUpdatedEvent -= OnPropertyUpdatedEventHandler;
        }

        private void OnPropertyUpdatedEventHandler(short key, IObservableProperty property)
        {
            OnPropertyUpdatedEvent?.Invoke(key, property);
        }

        public void UpdateProfile()
        {
            Mst.Events.Invoke(MstEventKeys.showLoadingInfo, "Saving profile data... Please wait!");

            MstTimer.WaitForSeconds(1f, () =>
            {
                var data = new Dictionary<string, string>
                {
                    { "displayName", profileSettingsView.DisplayName },
                    { "avatarUrl", profileSettingsView.AvatarUrl }
                };

                Connection.SendMessage((short)MstMessageCodes.UpdateDisplayNameRequest, data.ToBytes(), OnSaveProfileResponseCallback);
            });
        }

        private void OnSaveProfileResponseCallback(ResponseStatus status, IIncomingMessage response)
        {
            Mst.Events.Invoke(MstEventKeys.hideLoadingInfo);

            if(status == ResponseStatus.Success)
            {
                OnProfileSavedEvent?.Invoke();

                logger.Debug("Your profile is successfuly updated and saved");
            }
            else
            {
                Mst.Events.Invoke(MstEventKeys.showOkDialogBox, new OkDialogBoxEventMessage(response.AsString()));
                logger.Error(response.AsString());
            }
        }
    }
}
