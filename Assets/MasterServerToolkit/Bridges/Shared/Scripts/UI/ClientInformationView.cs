using MasterServerToolkit.MasterServer;
using MasterServerToolkit.UI;
using UnityEngine;

namespace MasterServerToolkit.Bridges
{
    public class ClientInformationView : UIView
    {
        [Header("Components"), SerializeField]
        [Tooltip("Required account information panel shown by ShowClientAuthStatusPanel when the client is signed in.")]
        private ClientAuthStatusPanel clientAuthStatusPanel;

        public void ShowClientAuthStatusPanel()
        {
            if (Mst.Client.Auth.IsSignedIn)
            {
                clientAuthStatusPanel.SetVisible(true);
            }
            else
            {
                string outputMessage = "Please make sure that you are logged in as a user";
                Logger.Error(outputMessage);

                ViewsManager.Show<OkDialogBoxView>( new OkDialogBoxEventMessage(outputMessage));
            }
        }
    }
}
