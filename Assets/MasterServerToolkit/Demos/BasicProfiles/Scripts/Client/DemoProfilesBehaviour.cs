using MasterServerToolkit.Bridges;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using MasterServerToolkit.UI;
using UnityEngine;
using UnityEngine.Events;

namespace MasterServerToolkit.Demos.BasicProfile
{
    public class DemoProfilesBehaviour : ProfileLoaderBehaviour
    {
        [Tooltip("Invoked after the master server confirms that the submitted display name and avatar URL were accepted. The event is not invoked when the request fails.")]
        public UnityEvent OnProfileSavedEvent;

        public void UpdateProfile(MstProperties data)
        {
            Connection.SendMessage(MstOpCodes.UpdateDisplayNameRequest, data.ToBytes(), (status, response) =>
            {
                ViewsManager.Hide<LoadingInfoView>();

                if (status == ResponseStatus.Success)
                {
                    OnProfileSavedEvent?.Invoke();
                    Logger.Debug("Your profile is successfuly updated and saved");
                }
                else
                {
                    string error = Mst.Errors.Parse(status, response);
                    ViewsManager.Show<OkDialogBoxView>(new OkDialogBoxEventMessage(error));
                    Logger.Error(error);
                }
            });
        }
    }
}
