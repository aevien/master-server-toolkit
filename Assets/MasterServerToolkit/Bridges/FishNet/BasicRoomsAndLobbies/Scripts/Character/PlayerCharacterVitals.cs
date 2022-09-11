#if FISHNET
using FishNet.Object;
using MasterServerToolkit.Networking;
using System;
using UnityEngine;

namespace MasterServerToolkit.Bridges.FishNetworking.Character
{
    public delegate void VitalChangeFloatDelegate(short key, float value);

    public class PlayerCharacterVitals : PlayerCharacterBehaviour
    {
        #region INSPECTOR

        [Header("Components"), SerializeField]
        private CharacterController characterController;
        [SerializeField]
        private GameObject dieEffectPrefab;

        #endregion

        /// <summary>
        /// Called when player resurrected
        /// </summary>
        public event Action OnAliveEvent;

        /// <summary>
        /// Called when player dies
        /// </summary>
        public event Action OnDieEvent;

        /// <summary>
        /// Called on client when one of the vital value is changed
        /// </summary>
        public event VitalChangeFloatDelegate OnVitalChangedEvent;

        /// <summary>
        /// Check if character is alive
        /// </summary>
        public bool IsAlive { get; protected set; } = true;

        public override bool IsReady => characterController;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void NotifyVitalChanged(short key, float value)
        {
            if (base.IsServer)
            {
                Rpc_NotifyVitalChanged(key, value);
            }
        }

        [ObserversRpc]
        private void Rpc_NotifyVitalChanged(short key, float value)
        {
            if (base.IsOwner)
            {
                OnVitalChangedEvent?.Invoke(key, value);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void NotifyAlive()
        {
            if (base.IsServer)
            {
                IsAlive = true;
                Rpc_NotifyAlive();
            }
        }

        [ObserversRpc]
        private void Rpc_NotifyAlive()
        {
            if (base.IsOwner)
            {
                IsAlive = true;
                OnAliveEvent?.Invoke();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void NotifyDied()
        {
            if (base.IsServer)
            {
                characterController.enabled = false;

                IsAlive = false;
                Rpc_NotifyDied();
            }
        }

        [ObserversRpc]
        private void Rpc_NotifyDied()
        {
            if (base.IsOwner)
            {
                characterController.enabled = false;

                IsAlive = false;
                OnDieEvent?.Invoke();
            }

            if (dieEffectPrefab)
            {
                MstTimer.WaitForSeconds(1f, () =>
                {
                    Instantiate(dieEffectPrefab, transform.position, Quaternion.identity);
                });
            }
        }
    }
}
#endif