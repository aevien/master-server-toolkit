using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.UI;
using MasterServerToolkit.Utils;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace MasterServerToolkit.Games
{
    public class UsernamePickView : UIView
    {
        [Header("Components"), SerializeField]
        private TMP_InputField usernameInputField;
        [SerializeField]
        private TMP_Text messageText;

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