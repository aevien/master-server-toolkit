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
        [Tooltip("Number of reusable notice items created at startup. Use at least 1; a value of 0 leaves no item available for incoming notifications.")]
        private int maxNotices = 5;

        [Header("Components"), SerializeField]
        [Tooltip("Required container that receives the notice-item pool. Existing children are removed during initialization.")]
        private RectTransform messagesContainer;
        [SerializeField]
        [Tooltip("Required NoticeItem prefab instantiated Max Notices times to form the reusable notification pool.")]
        private NoticeItem noticeItemPrefab;

        [Header("Settings"), SerializeField]
        [Tooltip("Seconds each notice remains visible, measured with scaled game time. A value of 0 hides it on the next coroutine update.")]
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
            for (int i = 0; i < maxNotices; i++)
            {
                var noticeItem = Instantiate(noticeItemPrefab, messagesContainer, false);
                noticeItem.Hide();
                noticeItems.Add(noticeItem);
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Mst.Client.Notifications.OnNotificationReceivedEvent -= Notifications_OnNotificationReceivedEvent;
        }

        protected virtual void Notifications_OnNotificationReceivedEvent(string message)
        {
            Show();

            var noticeItem = noticeItems.FirstOrDefault();
            noticeItem.transform.SetAsLastSibling();
            noticeItem.Show(message, destroyAfter);

            noticeItems.RemoveAt(0);
            noticeItems.Add(noticeItem);
        }
    }
}
