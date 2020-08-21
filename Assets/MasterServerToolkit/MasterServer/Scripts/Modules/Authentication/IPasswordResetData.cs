namespace MasterServerToolkit.MasterServer
{
    public interface IPasswordResetData
    {
        string Email { get; set; }
        string Code { get; set; }
    }
}