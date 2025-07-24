using MasterServerToolkit.Extensions;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.UI;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MasterServerToolkit.Bridges
{
    public class NoticeView : UIView
    {
        #region INSPECTOR

        [Header("Settings"), SerializeField]
        private int maxNotices = 5;

        [Header("Components"), SerializeField]
        private RectTransform messagesContainer;
        [SerializeField]
        private NoticeItem noticeItemPrefab;

        [Header("Settings"), SerializeField]
        private float destroyAfter = 5f;

        #endregion

        /// <summary>
        /// 
        /// </summary>
        protected readonly List<NoticeItem> noticeItems = new List<NoticeItem>();

        protected override void Awake()
        {
            base.Awake();
            Mst.Client.Notifications.OnNotificationReceivedEvent += Notifications_OnNotificationReceivedEvent;
            messagesContainer.RemoveChildren();
        }

        protected virtual void Start()
        {
            // Initialize all items for notice items
            InitAllNotices();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Mst.Client.Notifications.OnNotificationReceivedEvent -= Notifications_OnNotificationReceivedEvent;
        }

        /// <summary>
        /// Initializes all items for notice items
        /// </summary>
        private void InitAllNotices()
        {
            for (int i = 0; i < maxNotices; i++)
            {
                var noticeItem = Instantiate(noticeItemPrefab, messagesContainer, false);
                noticeItem.Hide();
                noticeItems.Add(noticeItem);
            }
        }

        protected virtual void Notifications_OnNotificationReceivedEvent(string message)
        {
            Show();

            var noticeItem = noticeItems.FirstOrDefault();

            noticeItem.transform.SetAsLastSibling();
            noticeItem.Show();

            noticeItem.OutputMessage(message);
            noticeItem.WaitAndHide(destroyAfter);

            noticeItems.RemoveAt(0);
            noticeItems.Add(noticeItem);
        }

        public void Message(string message)
        {
            Notifications_OnNotificationReceivedEvent(message);
        }
    }
}