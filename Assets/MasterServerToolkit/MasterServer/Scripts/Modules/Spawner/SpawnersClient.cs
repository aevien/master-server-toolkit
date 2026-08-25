using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public class SpawnersClient : MstBaseClient
    {
        public delegate void SpawnRequestResultHandler(SpawnRequestController controller, string error);
        public delegate void FinalizationDataResultHandler(Dictionary<string, string> data, string error);

        /// <summary>
        /// List of spawn request controllers
        /// </summary>
        private Dictionary<int, SpawnRequestController> _localSpawnRequests;

        /// <summary>
        /// Create instance of <see cref="SpawnersClient"/> with connection
        /// </summary>
        /// <param name="connection"></param>
        public SpawnersClient(IClientSocket connection) : base(connection)
        {
            RegisterErrorParsers();
            _localSpawnRequests = new Dictionary<int, SpawnRequestController>();
        }

        /// <summary>
        /// Sends a request to master server, to spawn a process with given options
        /// </summary>
        /// <param name="options"></param>
        public void RequestSpawn(MstProperties options)
        {
            RequestSpawn(options, new MstProperties(), string.Empty, null, Connection);
        }

        /// <summary>
        /// Sends a request to master server, to spawn a process in a given region, and with given options
        /// </summary>
        /// <param name="options"></param>
        public void RequestSpawn(MstProperties options, string region)
        {
            RequestSpawn(options, new MstProperties(), region, null, Connection);
        }

        /// <summary>
        /// Sends a request to master server, to spawn a process in a given region, and with given options
        /// </summary>
        /// <param name="options"></param>
        /// <param name="region"></param>
        /// <param name="callback"></param>
        public void RequestSpawn(MstProperties options, string region, SpawnRequestResultHandler callback)
        {
            RequestSpawn(options, new MstProperties(), region, callback, Connection);
        }

        /// <summary>
        /// Sends a request to master server, to spawn a process in a given region, and with given options
        /// </summary>
        /// <param name="options"></param>
        /// <param name="region"></param>
        /// <param name="callback"></param>
        public void RequestSpawn(MstProperties options, MstProperties customOptions, string region, SpawnRequestResultHandler callback)
        {
            RequestSpawn(options, customOptions, region, callback, Connection);
        }

        /// <summary>
        /// Sends a request to master server, to spawn a process in a given region, and with given options
        /// </summary>
        /// <param name="options"></param>
        /// <param name="customOptions"></param>
        /// <param name="region"></param>
        /// <param name="callback"></param>
        public void RequestSpawn(MstProperties options, MstProperties customOptions, string region, SpawnRequestResultHandler callback, IClientSocket connection)
        {
            // If we are not connected
            if (!connection.IsConnected)
            {
                callback?.Invoke(null, Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            // Set region to room by filter. If region is empty the room will be international
            options.Set(Mst.Args.Names.RoomRegion, string.IsNullOrEmpty(region) ? string.Empty : region);
            options.Append(customOptions);

            // Send request to Master Server SpawnerModule
            connection.SendMessage(MstOpCodes.ClientsSpawnRequest, options.ToBytes(), (status, response) =>
            {
                // If spawn request failed
                if (status != ResponseStatus.Success)
                {
                    Logs.Error($"Spawn request failed. Status={status}");
                    callback?.Invoke(null, Mst.Errors.Parse(status, response));
                    return;
                }

                // Create new spawn request controller
                var controller = new SpawnRequestController(response.AsInt(), connection, options);

                // List controler by spawn task id
                _localSpawnRequests[controller.SpawnTaskId] = controller;

                Logs.Debug($"Room was successfuly started with client options: {options}");

                callback?.Invoke(controller, null);
            });
        }

        /// <summary>
        /// Sends a request to abort spawn request, which was not yet finalized
        /// </summary>
        /// <param name="spawnTaskId"></param>
        public void AbortSpawn(int spawnTaskId)
        {
            AbortSpawn(spawnTaskId, null, Connection);
        }

        /// <summary>
        /// Sends a request to abort spawn request, which was not yet finalized
        /// </summary>
        public void AbortSpawn(int spawnTaskId, SuccessCallback callback)
        {
            AbortSpawn(spawnTaskId, callback, Connection);
        }

        /// <summary>
        /// Sends a request to abort spawn request, which was not yet finalized
        /// </summary>
        /// <param name="spawnTaskId"></param>
        /// <param name="callback"></param>
        /// <param name="connection"></param>
        public void AbortSpawn(int spawnTaskId, SuccessCallback callback, IClientSocket connection)
        {
            if (!connection.IsConnected)
            {
                callback?.Invoke(false, Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            Logs.Debug($"Aborting process [{spawnTaskId}]");

            connection.SendMessage(MstOpCodes.AbortSpawnRequest, spawnTaskId, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    Logs.Error($"Spawn abort request failed. Status={status}");
                    callback?.Invoke(false, Mst.Errors.Parse(status, response));
                    return;
                }

                Logs.Debug($"Room process [{spawnTaskId}] was successfuly aborted");

                callback?.Invoke(true, null);
            });
        }

        /// <summary>
        /// Retrieves data, which was given to master server by a spawned process,
        /// which was finalized
        /// </summary>
        /// <param name="spawnId"></param>
        /// <param name="callback"></param>
        public void GetFinalizationData(int spawnId, FinalizationDataResultHandler callback)
        {
            GetFinalizationData(spawnId, callback, Connection);
        }

        /// <summary>
        /// Retrieves data, which was given to master server by a spawned process,
        /// which was finalized
        /// </summary>
        public void GetFinalizationData(int spawnId, FinalizationDataResultHandler callback, IClientSocket connection)
        {
            if (!connection.IsConnected)
            {
                callback.Invoke(null, Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            connection.SendMessage(MstOpCodes.GetSpawnFinalizationData, spawnId, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback.Invoke(null, Mst.Errors.Parse(status, response));
                    return;
                }

                callback.Invoke(new Dictionary<string, string>().FromBytes(response.AsBytes()), null);
            });
        }

        /// <summary>
        /// Retrieves a specific spawn request controller
        /// </summary>
        /// <param name="spawnId"></param>
        /// <returns></returns>
        public SpawnRequestController GetSpawnRequestController(int spawnId)
        {
            _localSpawnRequests.TryGetValue(spawnId, out SpawnRequestController controller);
            return controller;
        }

        /// <summary>
        /// Retrieves a specific spawn request controller
        /// </summary>
        /// <param name="spawnId"></param>
        /// <returns></returns>
        public bool TryGetRequestController(int spawnId, out SpawnRequestController controller)
        {
            controller = GetSpawnRequestController(spawnId);
            return controller != null;
        }

        internal static void RegisterErrorParsers()
        {
            Mst.Errors.TryRegister(MstErrorCodes.SPAWNER_MODULE_STOPPING);
            Mst.Errors.TryRegister(MstErrorCodes.SPAWNER_NOT_REGISTERED);
            Mst.Errors.TryRegister(MstErrorCodes.SPAWNER_CAPACITY_UNAVAILABLE);
            Mst.Errors.TryRegister(MstErrorCodes.SPAWN_PERMISSION_DENIED);
            Mst.Errors.TryRegister(MstErrorCodes.SPAWN_REQUESTER_DISCONNECTED);
            Mst.Errors.TryRegister(MstErrorCodes.SPAWN_REQUEST_ALREADY_ACTIVE);
            Mst.Errors.TryRegister(MstErrorCodes.SPAWN_REQUEST_NOT_FOUND);
            Mst.Errors.TryRegister(MstErrorCodes.SPAWN_REQUEST_ALREADY_COMPLETED);
            Mst.Errors.TryRegister(MstErrorCodes.SPAWN_REQUEST_OWNER_REQUIRED);
            Mst.Errors.TryRegister(MstErrorCodes.SPAWN_FINALIZATION_UNAVAILABLE);
            Mst.Errors.TryRegister(MstErrorCodes.SPAWNER_REGISTRATION_PERMISSION_DENIED);
            Mst.Errors.TryRegister(MstErrorCodes.SPAWNER_REGISTRATION_DISCONNECTED);
            Mst.Errors.TryRegister(MstErrorCodes.SPAWNER_REGISTRATION_REJECTED);
            Mst.Errors.TryRegister(MstErrorCodes.SPAWNER_REGISTRATION_NOT_FOUND);
            Mst.Errors.TryRegister(MstErrorCodes.SPAWNER_REGISTRATION_OWNER_REQUIRED);
            Mst.Errors.TryRegister(MstErrorCodes.SPAWNED_PROCESS_PERMISSION_DENIED);
            Mst.Errors.TryRegister(MstErrorCodes.SPAWN_TASK_NOT_FOUND);
            Mst.Errors.TryRegister(MstErrorCodes.SPAWN_CODE_INVALID);
            Mst.Errors.TryRegister(MstErrorCodes.SPAWN_TASK_ALREADY_REGISTERED);
            Mst.Errors.TryRegister(MstErrorCodes.SPAWN_TASK_OWNER_REQUIRED);
            Mst.Errors.TryRegister(MstErrorCodes.SPAWN_TASK_ALREADY_COMPLETED);
            Mst.Errors.TryRegister(MstErrorCodes.SPAWNER_CONTROLLER_NOT_FOUND);
            Mst.Errors.TryRegister(MstErrorCodes.SPAWN_PROCESS_START_FAILED);
            Mst.Errors.TryRegister(MstErrorCodes.SPAWN_PROCESS_NOT_FOUND);
            Mst.Errors.TryRegister(MstErrorCodes.SPAWN_PROCESS_KILL_FAILED);
        }
    }
}
