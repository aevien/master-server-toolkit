using MasterServerToolkit.UI;

namespace MasterServerToolkit.Bridges
{
    public class LoadingInfoView : PopupView
    {
        protected override void OnStartShow()
        {
            base.OnStartShow();
            SetLabels(Payload.AsString());
        }
    }
}