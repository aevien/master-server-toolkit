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
        private TMP_InputField usernameInputField;

        protected override void Awake()
        {
            base.Awake();
            Mst.Events.AddListener(MstEventKeys.showPickUsernameView, (message) =>
            {
                usernameInputField.text = SimpleNameGenerator.GenerateFirstName(Gender.Male);

                Show();
            });
        }

        protected override void Start()
        {
            base.Start();
            usernameInputField.text = SimpleNameGenerator.GenerateFirstName(Gender.Male);
        }

        public void Submit()
        {
            Mst.Client.Chat.PickUsername(usernameInputField.text, (isSuccess, error) =>
            {
                if (!isSuccess)
                {
                    Logs.Error(error);
                    Mst.Events.Invoke(MstEventKeys.showOkDialogBox, new OkDialogBoxEventMessage(error));
                    return;
                }

                // Save username in global params
                Mst.Options.Set(MstDictKeys.USER_NAME, usernameInputField.text);

                Hide();
                ViewsManager.Show("ChatsView");
            });
        }

        public void Quit()
        {
            Mst.Runtime.Quit();
        }
    }
}