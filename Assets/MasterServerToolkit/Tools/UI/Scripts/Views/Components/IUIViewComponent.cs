namespace MasterServerToolkit.UI
{
    public interface IUIViewComponent
    {
        bool IsVisible { get; }
        IUIView Owner { get; set; }
        void OnOwnerAwake();
        void OnOwnerStart();
        void OnOwnerUpdate();
        void OnOwnerShow(IUIView owner);
        void OnOwnerHide(IUIView owner);
    }
}