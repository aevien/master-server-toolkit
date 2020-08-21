using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Aevien.UI
{
    public interface IValidatableComponent
    {
        bool IsValid();
    }
}