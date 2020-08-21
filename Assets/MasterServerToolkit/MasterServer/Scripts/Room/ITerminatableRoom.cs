using System;

namespace MasterServerToolkit.MasterServer
{
    public interface ITerminatableRoom
    {
        event Action OnCheckTerminationConditionEvent;
        bool IsAllowedToBeTerminated();
    }
}