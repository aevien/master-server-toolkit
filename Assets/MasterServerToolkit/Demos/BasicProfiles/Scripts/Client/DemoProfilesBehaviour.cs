﻿using MasterServerToolkit.Bridges;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using UnityEngine.Events;

namespace MasterServerToolkit.Demos.BasicProfile
{
    public class DemoProfilesBehaviour : ProfileLoaderBehaviour
    {
        public UnityEvent OnProfileSavedEvent;

        public void UpdateProfile(MstProperties data)
        {
            Connection.SendMessage(MstOpCodes.UpdateDisplayNameRequest, data.ToBytes(), (status, response) =>
            {
                Mst.Events.Invoke(MstEventKeys.hideLoadingInfo);

                if (status == ResponseStatus.Success)
                {
                    OnProfileSavedEvent?.Invoke();
                    Logger.Debug("Your profile is successfuly updated and saved");
                }
                else
                {
                    Mst.Events.Invoke(MstEventKeys.showOkDialogBox, new OkDialogBoxEventMessage(response.AsString()));
                    Logger.Error(response.AsString());
                }
            });
        }
    }
}
