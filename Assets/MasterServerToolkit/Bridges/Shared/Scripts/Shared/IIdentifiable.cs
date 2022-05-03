using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.Games
{
    public interface IIdentifiable
    {
        string Id { get; }
        string Title { get; }
    }
}