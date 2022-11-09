using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using MasterServerToolkit.UI;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace MasterServerToolkit.Bridges
{
    public class ChatsView : UIView
    {
        #region INSPECTOR

        [Header("Components"), SerializeField]
        private RectTransform chatChannelsContainer;
        [SerializeField]
        private RectTransform chatMesagesContainer;
        [SerializeField]
        private TMP_Text statusInfoText;
        [SerializeField]
        private TMP_Text chatTitleText;
        [SerializeField]
        private ChatMessageItemUI incomingMessageItemPrefab;
        [SerializeField]
        private ChatMessageItemUI outgoingMessageItemPrefab;
        [SerializeField]
        private ChatChannelItemUI chatChannelItemPrefab;
        [SerializeField]
        private TMP_InputField messageInputField;

        [Header("Settings"), SerializeField]
        private string defaultChannelName = "MST Chat Demo";

        #endregion

        private List<ChatChannelItemUI> chanelItemsList;

        /// <summary>
        /// Client chat username
        /// </summary>
        private string username;

        protected override void Start()
        {
            base.Start();

            chanelItemsList = new List<ChatChannelItemUI>();

            Mst.Client.Chat.OnMessageReceivedEvent += Chat_OnMessageReceivedEvent;
            Mst.Client.Chat.OnUserJoinedChannelEvent += Chat_OnUserJoinedChannelEvent;
            Mst.Client.Chat.OnUserLeftChannelEvent += Chat_OnUserLeftChannelEvent;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Return))
            {
                TrySendMessage();
            }
        }

        /// <summary>
        /// Sends message to anybody
        /// </summary>
        public void TrySendMessage()
        {
            if (!string.IsNullOrEmpty(messageInputField.text))
            {
                if (string.IsNullOrEmpty(username))
                {
                    Mst.Events.Invoke(MstEventKeys.showOkDialogBox, new OkDialogBoxEventMessage("Username cannot be empty! Set username in UsernamePickView and try again", () =>
                    {
                        Mst.Events.Invoke(MstEventKeys.showPickUsernameView);
                    }));

                    return;
                }

                var message = new ChatMessagePacket()
                {
                    Receiver = defaultChannelName,
                    Message = messageInputField.text.Length > 200 ? $"{messageInputField.text.Substring(0, 200)}..." : messageInputField.text,
                    MessageType = ChatMessageType.ChannelMessage
                };

                messageInputField.text = string.Empty;

                Mst.Client.Chat.SendMessage(message, (isSuccess, error) =>
                {
                    if (!isSuccess)
                    {
                        Mst.Events.Invoke(MstEventKeys.showOkDialogBox, new OkDialogBoxEventMessage(error));
                        return;
                    }
                });

                messageInputField.Select();
            }
        }

        /// <summary>
        /// Invokes when new message has come
        /// </summary>
        /// <param name="message"></param>
        private void Chat_OnMessageReceivedEvent(ChatMessagePacket message)
        {
            if (message.Sender == username)
            {
                var messageItem = Instantiate(outgoingMessageItemPrefab, chatMesagesContainer, false);
                messageItem.Set("Me", message.Message);
            }
            else
            {
                var messageItem = Instantiate(incomingMessageItemPrefab, chatMesagesContainer, false);
                messageItem.Set(message.Sender, message.Message);
            }
        }

        private void Chat_OnUserJoinedChannelEvent(string channel, string user)
        {
            foreach (var channelItemUI in chanelItemsList)
            {
                if (channelItemUI.DsplayName == channel)
                {
                    channelItemUI.DsplayName = channel;
                    channelItemUI.OnlineCount++;
                    channelItemUI.Repaint();
                    break;
                }
            }
        }

        private void Chat_OnUserLeftChannelEvent(string channel, string user)
        {
            foreach (var channelItemUI in chanelItemsList)
            {
                if (channelItemUI.DsplayName == channel)
                {
                    channelItemUI.DsplayName = channel;
                    channelItemUI.OnlineCount--;
                    channelItemUI.Repaint();
                    break;
                }
            }

            var messageItem = Instantiate(incomingMessageItemPrefab, chatMesagesContainer, false);
            messageItem.Set(user, "I'm off. Bye!");
        }

        protected override void OnShow()
        {
            base.OnShow();
            ClearChannels();
            ClearMessages();
            JoinPredefinedChannels();

            username = Mst.Options.AsString(MstDictKeys.USER_NAME);
        }

        /// <summary>
        /// Clears list of messages
        /// </summary>
        public void ClearMessages()
        {
            foreach (Transform t in chatMesagesContainer)
            {
                Destroy(t.gameObject);
            }
        }

        /// <summary>
        /// Clears list of channels
        /// </summary>
        public void ClearChannels()
        {
            foreach (Transform t in chatChannelsContainer)
            {
                Destroy(t.gameObject);
            }
        }

        /// <summary>
        /// Joins channel
        /// </summary>
        private void JoinPredefinedChannels()
        {
            canvasGroup.interactable = false;

            if (statusInfoText)
            {
                statusInfoText.text = "Finding channels... Please wait!";
                statusInfoText.gameObject.SetActive(true);
            }

            MstTimer.WaitForSeconds(0.5f, () =>
            {
                // Join default chat
                Mst.Client.Chat.JoinChannel(defaultChannelName, (isSuccess, joinChannelError) =>
                {
                    canvasGroup.interactable = true;
                    statusInfoText.gameObject.SetActive(false);

                    if (!isSuccess)
                    {
                        Mst.Events.Invoke(MstEventKeys.showOkDialogBox, new OkDialogBoxEventMessage(joinChannelError, () =>
                        {
                            Hide();
                            ViewsManager.Show("UsernamePickView");
                        }));

                        return;
                    }

                    chatTitleText.text = $"You are in \"{defaultChannelName}\" chat channel";
                    messageInputField.Select();

                    UpdateChanelsList();
                });
            });
        }

        private void UpdateChanelsList()
        {
            // Get my channels
            Mst.Client.Chat.GetMyChannels((channels, getChannelsError) =>
            {
                statusInfoText.gameObject.SetActive(false);

                if (!string.IsNullOrEmpty(getChannelsError))
                {
                    Mst.Events.Invoke(MstEventKeys.showOkDialogBox, new OkDialogBoxEventMessage(getChannelsError, () =>
                    {
                        Hide();
                        ViewsManager.Show("UsernamePickView");
                    }));

                    return;
                }

                DrawChannelsList(channels);
            });
        }

        /// <summary>
        /// Draws channels list
        /// </summary>
        /// <param name="channels"></param>
        private void DrawChannelsList(List<ChatChannelInfo> channels)
        {
            chanelItemsList.Clear();
            ClearChannels();

            foreach (var channel in channels)
            {
                ChatChannelItemUI channelItem = Instantiate(chatChannelItemPrefab, chatChannelsContainer, false);
                channelItem.DsplayName = channel.Name;
                channelItem.OnlineCount = channel.OnlineCount;
                channelItem.Repaint();

                chanelItemsList.Add(channelItem);
            }
        }
    }
}
