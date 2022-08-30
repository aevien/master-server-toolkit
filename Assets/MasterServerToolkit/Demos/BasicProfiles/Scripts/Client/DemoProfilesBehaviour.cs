using MasterServerToolkit.Games;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using MasterServerToolkit.UI;
using System.Collections.Generic;
using UnityEngine.Events;

namespace MasterServerToolkit.Examples.BasicProfile
{
    public class DemoProfilesBehaviour : ProfileLoaderBehaviour
    {
        private ProfileSettingsView profileSettingsView;

        public UnityEvent OnProfileSavedEvent;

        protected override void OnInitialize()
        {
            profileSettingsView = ViewsManager.GetView<ProfileSettingsView>("ProfileSettingsView");

            Profile = new ObservableProfile();
            ProfileProperties.Fill(Profile);
        }

        public void UpdateProfile(MstProperties data)
        {
            Mst.Events.Invoke(MstEventKeys.showLoadingInfo, "Saving profile data... Please wait!");

            MstTimer.Instance.WaitForSeconds(1f, () =>
            {
                Connection.SendMessage(MstOpCodes.UpdateDisplayNameRequest, data.ToBytes(), (status, response) =>
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
                });
            });
        }
    }
}
