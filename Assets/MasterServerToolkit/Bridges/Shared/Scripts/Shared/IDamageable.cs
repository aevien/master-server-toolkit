namespace MasterServerToolkit.Games
{
    public interface IDamageable
    {
        void Damage(float value);
        void Damage(float value, IIdentifiable damageGiver);
    }
}