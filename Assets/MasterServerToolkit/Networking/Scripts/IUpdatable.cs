namespace MasterServerToolkit.Networking
{
    public interface IUpdatable
    {
        /// <summary>
        /// This method will be updated by <see cref="MstUpdateRunner"/>. It is created for running in main Unity thread
        /// </summary>
        void DoUpdate();
    }
}