namespace MasterServerToolkit.Games
{
    public interface IHealable
    {
        float Health { get; }
        void Heal(float value);
    }
}
