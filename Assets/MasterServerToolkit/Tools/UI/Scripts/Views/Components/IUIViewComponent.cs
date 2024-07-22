namespace MasterServerToolkit.UI
{
    public interface IUIViewComponent
    {
        void OnOwnerShow(IUIView owner);
        void OnOwnerHide(IUIView owner);
    }
}