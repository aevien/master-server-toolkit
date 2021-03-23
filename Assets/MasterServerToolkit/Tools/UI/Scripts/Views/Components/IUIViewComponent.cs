namespace MasterServerToolkit.UI
{
    public interface IUIViewComponent
    {
        IUIView Owner { get; set; }

        void OnOwnerAwake();
        void OnOwnerStart();
        void OnOwnerShow(IUIView owner);
        void OnOwnerHide(IUIView owner);
    }
}