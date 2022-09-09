namespace MasterServerToolkit.Games
{
    public interface IHealable
    {
        float Health { get; }
        bool Heal(float value);
    }
}
