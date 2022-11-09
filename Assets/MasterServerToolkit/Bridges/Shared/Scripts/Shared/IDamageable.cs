namespace MasterServerToolkit.Bridges
{
    public interface IDamageable
    {
        float Health { get; }
        void Damage(float value);
        void Damage(float value, IIdentifiable damageGiver);
    }
}