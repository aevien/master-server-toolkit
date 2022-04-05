namespace MasterServerToolkit.MasterServer
{
    public interface IBaseClientModule
    {
        BaseClientBehaviour ParentBehaviour { get; set; }
        void OnInitialize(BaseClientBehaviour parentBehaviour);
    }
}