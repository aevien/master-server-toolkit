using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.Games
{
    public interface IDamageable
    {
        void TakeDamage(float value);
        void TakeDamage(float value, IIdentifiable damageGiver);
    }
}