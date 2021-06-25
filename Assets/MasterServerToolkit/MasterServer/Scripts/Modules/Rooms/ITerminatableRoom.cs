using System;

namespace MasterServerToolkit.MasterServer
{
    public interface ITerminatableRoom
    {
        event Action OnCheckTerminationConditionEvent;
        /// <summary>
        /// Check if this room server is allowed to be terminated
        /// </summary>
        /// <returns></returns>
        bool IsAllowedToBeTerminated();
    }
}