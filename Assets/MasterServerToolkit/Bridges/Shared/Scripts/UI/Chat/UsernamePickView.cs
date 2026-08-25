using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.UI;
using MasterServerToolkit.Utils;
using TMPro;
using UnityEngine;

namespace MasterServerToolkit.Bridges
{
    public class UsernamePickView : UIView
    {
        [Header("Components"), SerializeField]
        [Tooltip("Required input field populated with a generated name when the view opens and submitted as the client's chat username.")]
        private TMP_InputField usernameInputField;

        protected override void OnStartShow()
        {
            base.OnStartShow();
            usernameInputField.text = SimpleNameGenerator.GenerateFirstName(Gender.Male);
        }

        public void Submit()
        {
            Mst.Client.Chat.PickUsername(usernameInputField.text, (isSuccess, error) =>
            {
                if (!isSuccess)
                {
                    Logs.Error(error);
                    ViewsManager.Show<OkDialogBoxView>(new OkDialogBoxEventMessage(error)
                    {
                        MessageType = DialogBoxMessageType.Error
                    });
                    return;
                }

                // Save username in global params
                Mst.Options.Set(MstParamKeys.USER_NAME, usernameInputField.text);

                Hide();
                ViewsManager.Show<ChatsView>();
            });
        }

        public void Quit()
        {
            Mst.Runtime.Quit();
        }
    }
}
