using MasterServerToolkit.Games;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using MasterServerToolkit.UI;
using System;
using System.Collections.Generic;
using UnityEngine.Events;

namespace MasterServerToolkit.Examples.BasicProfile
{
    public class DemoProfilesBehaviour : ProfilesBehaviour
    {
        private ProfileView profileView;
        private ProfileSettingsView profileSettingsView;

        public event Action<ushort, IObservableProperty> OnPropertyUpdatedEvent;
        public UnityEvent OnProfileSavedEvent;

        protected override void OnInitialize()
        {
            profileView = ViewsManager.GetView<ProfileView>("ProfileView");
            profileSettingsView = ViewsManager.GetView<ProfileSettingsView>("ProfileSettingsView");

            Profile = new ObservableProfile
            {
                new ObservableString((ushort)ObservablePropertyCodes.DisplayName),
                new ObservableString((ushort)ObservablePropertyCodes.Avatar),
                new ObservableFloat((ushort)ObservablePropertyCodes.Bronze),
                new ObservableFloat((ushort)ObservablePropertyCodes.Silver),
                new ObservableFloat((ushort)ObservablePropertyCodes.Gold)
            };

            Profile.OnPropertyUpdatedEvent += OnPropertyUpdatedEventHandler;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            Profile.OnPropertyUpdatedEvent -= OnPropertyUpdatedEventHandler;
        }

        private void OnPropertyUpdatedEventHandler(ushort key, IObservableProperty property)
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

                Connection.SendMessage((ushort)MstOpCodes.UpdateDisplayNameRequest, data.ToBytes(), OnSaveProfileResponseCallback);
            });
        }

        private void OnSaveProfileResponseCallback(ResponseStatus status, IIncomingMessage response)
        {
            Mst.Events.Invoke(MstEventKeys.hideLoadingInfo);

            if (status == ResponseStatus.Success)
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
