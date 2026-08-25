using MasterServerToolkit.Json;
using System.Runtime.InteropServices;

namespace MasterServerToolkit.GameService
{
    public class VkGamesShareModule : BaseServiceModule, IShareModule
    {
        [DllImport("__Internal")] private static extern void Gb_Vk_CanAddToFavorites();
        [DllImport("__Internal")] private static extern void Gb_Vk_AddToFavorites();
        private ShortcutAvailabilityHandler shortcutAvailabilityCallback;
        private ShortcutPromptHandler shortcutCallback;
        private bool isShortcutAvailabilityPending;
        private bool isShortcutPromptPending;

        public override void OnBeforeInit(IService service)
        {
            IsSupported = true;
            base.OnBeforeInit(service);
        }

        public override void OnInit(IService service)
        {
            base.OnInit(service);
            IsReady = true;
        }

        public void Review() => Review(null);
        public void Review(ReviewHandler callback) => callback?.Invoke(new ReviewResultInfo { FeedbackSent = false, Error = "unsupported" });
        public void CanReview(ReviewAvailabilityHandler callback) => callback?.Invoke(new ReviewAvailabilityInfo { CanReview = false, Reason = "unsupported" });

        public void CanShowShortcutPrompt(ShortcutAvailabilityHandler callback)
        {
            if (isShortcutAvailabilityPending)
            {
                callback?.Invoke(new ShortcutAvailabilityInfo
                {
                    CanShow = false,
                    Reason = "request_in_progress"
                });
                return;
            }

            isShortcutAvailabilityPending = true;
            shortcutAvailabilityCallback = callback;
            Gb_Vk_CanAddToFavorites();
        }

        public void ShowShortcutPrompt(ShortcutPromptHandler callback)
        {
            if (isShortcutPromptPending)
            {
                callback?.Invoke(new ShortcutPromptInfo
                {
                    Outcome = "error",
                    Error = "request_in_progress"
                });
                return;
            }

            isShortcutPromptPending = true;
            shortcutCallback = callback;
            Gb_Vk_AddToFavorites();
        }

        protected void Vk_OnCanAddToFavorites(string json)
        {
            var response = MstJson.IsJson(json) ? new MstJson(json) : MstJson.CreateObject();
            ShortcutAvailabilityHandler callback = shortcutAvailabilityCallback;
            shortcutAvailabilityCallback = null;
            isShortcutAvailabilityPending = false;
            callback?.Invoke(new ShortcutAvailabilityInfo
            {
                CanShow = response.HasField("canShow") && response["canShow"].BoolValue,
                Reason = response.HasField("reason") ? response["reason"].StringValue : "invalid_response"
            });
        }

        protected void Vk_OnAddToFavorites(string json)
        {
            var response = MstJson.IsJson(json) ? new MstJson(json) : MstJson.CreateObject();
            ShortcutPromptHandler callback = shortcutCallback;
            shortcutCallback = null;
            isShortcutPromptPending = false;
            callback?.Invoke(new ShortcutPromptInfo
            {
                Outcome = response.HasField("success") && response["success"].BoolValue ? "accepted" : "error",
                Error = response.HasField("error") ? response["error"].StringValue : string.Empty
            });
        }
    }
}
