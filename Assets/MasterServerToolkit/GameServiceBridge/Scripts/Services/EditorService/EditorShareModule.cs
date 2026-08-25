namespace MasterServerToolkit.GameService
{
	public class EditorShareModule : BaseServiceModule, IShareModule
    {
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
            callback?.Invoke(new ReviewResultInfo
            {
                FeedbackSent = false,
                Error = "unsupported"
            });
        }

        public void CanReview(ReviewAvailabilityHandler callback)
        {
            callback?.Invoke(new ReviewAvailabilityInfo
            {
                CanReview = false,
                Reason = "unsupported"
            });
        }

        public void CanShowShortcutPrompt(ShortcutAvailabilityHandler callback)
        {
            callback?.Invoke(new ShortcutAvailabilityInfo
            {
                CanShow = false,
                Reason = "unsupported"
            });
        }

        public void ShowShortcutPrompt(ShortcutPromptHandler callback)
        {
            callback?.Invoke(new ShortcutPromptInfo
            {
                Outcome = string.Empty,
                Error = "unsupported"
            });
        }
    }
}
