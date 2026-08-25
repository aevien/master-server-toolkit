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
        [Tooltip("Required container that receives generated channel entries and whose children are cleared when the view opens.")]
        private RectTransform chatChannelsContainer;
        [SerializeField]
        [Tooltip("Required container that receives incoming and outgoing message entries and whose children are cleared when the view opens.")]
        private RectTransform chatMesagesContainer;
        [SerializeField]
        [Tooltip("Required status label shown while channel data is requested and hidden after the request completes.")]
        private TMP_Text statusInfoText;
        [SerializeField]
        [Tooltip("Required title label updated after joining the configured default channel.")]
        private TMP_Text chatTitleText;
        [SerializeField]
        [Tooltip("Required message-row prefab instantiated for messages received from other users and user-leave notices.")]
        private ChatMessageItemUI incomingMessageItemPrefab;
        [SerializeField]
        [Tooltip("Required message-row prefab instantiated for messages sent by the local chat user.")]
        private ChatMessageItemUI outgoingMessageItemPrefab;
        [SerializeField]
        [Tooltip("Required channel-row prefab instantiated for every channel returned by the chat module.")]
        private ChatChannelItemUI chatChannelItemPrefab;
        [SerializeField]
        [Tooltip("Required input field used to compose messages. Submitted messages are truncated to 200 characters plus an ellipsis.")]
        private TMP_InputField messageInputField;

        [Header("Settings"), SerializeField]
        [Tooltip("Channel name the view joins when opened and uses as the receiver for sent channel messages. It must match a channel accepted by the server.")]
        private string defaultChannelName = "MST Chat Demo";

        #endregion

        private List<ChatChannelItemUI> chanelItemsList;

        /// <summary>
        /// Client chat username
        /// </summary>
        private string username;

        protected void Start()
        {
            chanelItemsList = new List<ChatChannelItemUI>();

            Mst.Client.Chat.OnMessageReceivedEvent += Chat_OnMessageReceivedEvent;
            Mst.Client.Chat.OnUserJoinedChannelEvent += Chat_OnUserJoinedChannelEvent;
            Mst.Client.Chat.OnUserLeftChannelEvent += Chat_OnUserLeftChannelEvent;
        }

        protected override void OnDestroy()
        {
            Mst.Client.Chat.OnMessageReceivedEvent -= Chat_OnMessageReceivedEvent;
            Mst.Client.Chat.OnUserJoinedChannelEvent -= Chat_OnUserJoinedChannelEvent;
            Mst.Client.Chat.OnUserLeftChannelEvent -= Chat_OnUserLeftChannelEvent;

            base.OnDestroy();
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
                    ViewsManager.Show<OkDialogBoxView>(new OkDialogBoxEventMessage("Username cannot be empty! Set username in UsernamePickView and try again", () =>
                    {
                        ViewsManager.Show<AdBannerView>();
                    }));

                    return;
                }

                var message = new ChatMessagePacket()
                {
                    Receiver = defaultChannelName,
                    Message = messageInputField.text.Length > 200 ? $"{messageInputField.text.Substring(0, 200)}..." : messageInputField.text,
                    MessageType = ChatMessageType.Channel
                };

                messageInputField.text = string.Empty;

                Mst.Client.Chat.SendMessage(message, (isSuccess, error) =>
                {
                    if (!isSuccess)
                    {
                        ViewsManager.Show<OkDialogBoxView>(new OkDialogBoxEventMessage(error)
                        {
                            MessageType = DialogBoxMessageType.Error
                        });
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

        protected override void OnStartShow()
        {
            base.OnStartShow();
            ClearChannels();
            ClearMessages();
            JoinPredefinedChannels();

            username = Mst.Options.AsString(MstParamKeys.USER_NAME);
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
                    statusInfoText.gameObject.SetActive(false);

                    if (!isSuccess)
                    {
                        ViewsManager.Show<OkDialogBoxView>(new OkDialogBoxEventMessage(joinChannelError, () =>
                        {
                            Hide();
                            ViewsManager.Show<UsernamePickView>();
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
                    ViewsManager.Show<OkDialogBoxView>(new OkDialogBoxEventMessage(getChannelsError, () =>
                    {
                        Hide();
                        ViewsManager.Show<UsernamePickView>();
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
