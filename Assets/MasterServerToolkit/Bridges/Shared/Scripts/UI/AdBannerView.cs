using MasterServerToolkit.MasterServer;
using MasterServerToolkit.UI;
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MasterServerToolkit.Bridges
{
    public class AdBannerView : PopupView
    {
        [SerializeField]
        [Tooltip("Seconds the banner must remain visible before its action is accepted. Use a value greater than 0 so the progress ratio has a meaningful duration.")]
        private float showTime = 5f;
        [SerializeField]
        [Tooltip("Required Slider that displays the remaining wait as a normalized value from 1 to 0 while the banner is visible.")]
        private Slider progress;

        private bool showProgress = false;
        private float currentShowTime = 0;

        private void Update()
        {
            if (showProgress)
            {
                currentShowTime -= Time.deltaTime;

                if (currentShowTime <= 0)
                {
                    showProgress = false;
                    currentShowTime = 0;
                }
            }

            if (IsVisible)
                progress.value = currentShowTime / showTime;
        }

        protected override void OnStartShow()
        {
            base.OnStartShow();

            try
            {
                var messageData = Payload.As<AdBannerEventMessage>();
                SetLabels(messageData.Message);

                SetButtonsClick(() =>
                {
                    if (showProgress)
                    {
                        messageData.FailedCallback?.Invoke();
                    }
                    else
                    {
                        messageData.SuccessCallback?.Invoke();
                    }

                    Hide();
                });

                Show();

                currentShowTime = showTime;
                showProgress = true;
            }
            catch (Exception e)
            {
                logger.Error(e);
            }
        }
    }

    public class AdBannerEventMessage : DialogBoxEventMessage
    {
        public AdBannerEventMessage() : base() { }

        public AdBannerEventMessage(string message) : base(message)
        {
            FailedCallback = null;
        }

        public AdBannerEventMessage(string message, UnityAction successCallback, UnityAction failedCallback) : base(message)
        {
            SuccessCallback = successCallback;
            FailedCallback = failedCallback;
        }

        public UnityAction SuccessCallback { get; set; }
        public UnityAction FailedCallback { get; set; }
    }
}
