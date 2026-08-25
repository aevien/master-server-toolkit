using MasterServerToolkit.Json;
using System.Runtime.InteropServices;

namespace MasterServerToolkit.GameService
{
    public class YandexGamesShareModule : BaseServiceModule, IShareModule
    {
        public override bool IsSupported { get; protected set; }

        [DllImport("__Internal")]
        private static extern void Gb_Yg_CanReview();
        [DllImport("__Internal")]
        private static extern void Gb_Yg_ReviewGame();
        [DllImport("__Internal")]
        private static extern void Gb_Yg_CanShowShortcutPrompt();
        [DllImport("__Internal")]
        private static extern void Gb_Yg_ShowShortcutPrompt();

        private ReviewAvailabilityHandler reviewAvailabilityCallback;
        private ReviewHandler reviewCallback;
        private ShortcutAvailabilityHandler shortcutAvailabilityCallback;
        private ShortcutPromptHandler shortcutPromptCallback;

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

        public void Review()
        {
            Review(null);
        }

        public void Review(ReviewHandler callback)
        {
            reviewCallback = callback;
            Gb_Yg_ReviewGame();
        }

        public void CanReview(ReviewAvailabilityHandler callback)
        {
            reviewAvailabilityCallback = callback;
            Gb_Yg_CanReview();
        }

        public void CanShowShortcutPrompt(ShortcutAvailabilityHandler callback)
        {
            shortcutAvailabilityCallback = callback;
            Gb_Yg_CanShowShortcutPrompt();
        }

        public void ShowShortcutPrompt(ShortcutPromptHandler callback)
        {
            shortcutPromptCallback = callback;
            Gb_Yg_ShowShortcutPrompt();
        }

        #region WEB_CALLBACKS

        protected void Yg_OnCanReview(string json)
        {
            var data = new MstJson(json);
            reviewAvailabilityCallback?.Invoke(new ReviewAvailabilityInfo
            {
                CanReview = data.HasField(YandexGamesKeys.Value) && data[YandexGamesKeys.Value].BoolValue,
                Reason = data.HasField(YandexGamesKeys.Reason) ? data[YandexGamesKeys.Reason].StringValue : string.Empty
            });
            reviewAvailabilityCallback = null;
        }

        protected void Yg_OnReviewGame(string json)
        {
            var data = new MstJson(json);
            reviewCallback?.Invoke(new ReviewResultInfo
            {
                FeedbackSent = data.HasField(YandexGamesKeys.FeedbackSent) && data[YandexGamesKeys.FeedbackSent].BoolValue,
                Error = data.HasField(YandexGamesKeys.Error) ? data[YandexGamesKeys.Error].StringValue : string.Empty
            });
            reviewCallback = null;
        }

        protected void Yg_OnCanShowShortcutPrompt(string json)
        {
            var data = new MstJson(json);
            shortcutAvailabilityCallback?.Invoke(new ShortcutAvailabilityInfo
            {
                CanShow = data.HasField(YandexGamesKeys.CanShow) && data[YandexGamesKeys.CanShow].BoolValue,
                Reason = data.HasField(YandexGamesKeys.Reason) ? data[YandexGamesKeys.Reason].StringValue : string.Empty
            });
            shortcutAvailabilityCallback = null;
        }

        protected void Yg_OnShowShortcutPrompt(string json)
        {
            var data = new MstJson(json);
            shortcutPromptCallback?.Invoke(new ShortcutPromptInfo
            {
                Outcome = data.HasField(YandexGamesKeys.Outcome) ? data[YandexGamesKeys.Outcome].StringValue : string.Empty,
                Error = data.HasField(YandexGamesKeys.Error) ? data[YandexGamesKeys.Error].StringValue : string.Empty
            });
            shortcutPromptCallback = null;
        }

        #endregion
    }
}
