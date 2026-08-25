namespace MasterServerToolkit.Bridges
{
    public interface IHealable
    {
        float Health { get; }
        bool Heal(float value);
    }
}
