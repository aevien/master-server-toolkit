using MasterServerToolkit.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class RoomTerminator : MonoBehaviour
    {
        #region INSPECTOR

        /// <summary>
        /// Room will be closed when last player left it
        /// </summary>
        [Header("Terminator Settings"), SerializeField, Tooltip("Room will be closed when last player left it")]
        protected bool terminateRoomWhenLastPlayerQuits = false;

        /// <summary>
        /// Waits until room is empty and terminates it in the given number of seconds
        /// </summary>
        [SerializeField, Tooltip("Waits until room is empty and terminates it in the given number of seconds"), Range(0, 300)]
        protected int terminateEmptyRoomInSeconds = 60;

        protected ITerminatableRoom terminatableRoom;

        /// <summary>
        /// Log levelof this module
        /// </summary>
        [SerializeField]
        protected LogLevel logLevel = LogLevel.Info;

        #endregion

        /// <summary>
        /// Logger assigned to this module
        /// </summary>
        protected Logging.Logger logger;

        protected virtual void Awake()
        {
            terminatableRoom = GetComponentInChildren<ITerminatableRoom>();

            logger = Mst.Create.Logger(GetType().Name);
            logger.LogLevel = logLevel;
        }

        protected virtual void Start()
        {
            if(!Mst.Client.Rooms.ForceClientMode && !Mst.Runtime.IsEditor)
            {
                if (terminatableRoom == null)
                {
                    logger.Error("No ITerminatableRoom component found!");
                    return;
                }

                terminatableRoom.OnCheckTerminationConditionEvent += CheckTerminationConditions;

                // Start waiting empty room termination
                if (!terminateRoomWhenLastPlayerQuits && terminateEmptyRoomInSeconds > 0)
                    StartCoroutine(StartEmptyIntervalsCheck(terminateEmptyRoomInSeconds));
            }
        }

        protected virtual void OnDestroy()
        {
            if (terminatableRoom != null)
                terminatableRoom.OnCheckTerminationConditionEvent -= CheckTerminationConditions;
        }

        /// <summary>
        /// Each time, after the amount of seconds provided passes, checks
        /// if the server is empty, and if it is - terminates application
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns></returns>
        private IEnumerator StartEmptyIntervalsCheck(float timeout)
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(timeout);
                CheckTerminationConditions();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void CheckTerminationConditions()
        {
            if ((terminateRoomWhenLastPlayerQuits && terminatableRoom.IsAllowedToBeTerminated()) || terminatableRoom.IsAllowedToBeTerminated())
            {
                logger.Debug("Terminating game server according to conditions");
                Mst.Runtime.Quit();
            }
        }
    }
}
