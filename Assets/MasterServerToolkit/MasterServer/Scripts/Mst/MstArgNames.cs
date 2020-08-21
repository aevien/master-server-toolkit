namespace MasterServerToolkit.MasterServer
{
    public class MstArgNames
    {
        /// <summary>
        /// Use this cmd to start master server after unity player is started
        /// </summary>
        public string StartMaster { get { return "-msfStartMaster"; } }

        /// <summary>
        /// Use this cmd to start server room spawner after unity player is started
        /// </summary>
        public string StartSpawner { get { return "-msfStartSpawner"; } }

        /// <summary>
        /// Use this cmd to start client connection to server after unity player is started
        /// </summary>
        public string StartClientConnection { get { return "-msfStartClientConnection"; } }

        /// <summary>
        /// Use this cmd to setup master server connection port
        /// </summary>
        public string MasterPort { get { return "-msfMasterPort"; } }

        /// <summary>
        /// Use this cmd to setup master server connection IP address
        /// </summary>
        public string MasterIp { get { return "-msfMasterIp"; } }

        /// <summary>
        /// Use this cmd to setup spawned process task ID
        /// </summary>
        public string SpawnTaskId { get { return "-msfSpawnTaskId"; } }

        /// <summary>
        /// Use this cmd to check if there's no tampering with spawned processes
        /// </summary>
        public string SpawnTaskUniqueCode { get { return "-msfSpawnTaskUniqueCode"; } }

        /// <summary>
        /// Use this cmd to setup IP address of the spawned room server
        /// </summary>
        public string RoomIp { get { return "-msfRoomIp"; } }

        /// <summary>
        /// Use this cmd to setup port of the spawned room server
        /// </summary>
        public string RoomPort { get { return "-msfRoomPort"; } }

        /// <summary>
        /// Use this cmd if you want a spawner to start creating room ports from your own specific value
        /// </summary>
        public string RoomDefaultPort { get { return "-msfRoomDefaultPort"; } }

        /// <summary>
        /// Use this cmd to setup server room as provate or not
        /// </summary>
        public string RoomIsPrivate { get { return "-msfRoomIsPrivate"; } }

        /// <summary>
        /// Use this cmd to setup server room name
        /// </summary>
        public string RoomName { get { return "-msfRoomName"; } }

        /// <summary>
        /// Use this cmd to setup server room password
        /// </summary>
        public string RoomPassword { get { return "-msfRoomPassword"; } }

        /// <summary>
        /// Use this cmd to setup the max number of connections of the spawned room server
        /// </summary>
        public string RoomMaxConnections { get { return "-msfRoomMaxConnections"; } }

        /// <summary>
        /// Use this cmd to setup the path to room server executable file that you need to spawn
        /// </summary>
        public string RoomExecutablePath { get { return "-msfRoomExe"; } }

        /// <summary>
        /// Use this cmd to setup the region, to which the spawner belongs
        /// </summary>
        public string RoomRegion { get { return "-msfRoomRegion"; } }

        /// <summary>
        /// Use this cmd to setup WebSockets mode on room server if you use WebGL version of client
        /// this feature works only is you server supports web sockets
        /// </summary>
        public string UseWebSockets { get { return "-msfUseWebSockets"; } }

        /// <summary>
        /// Send this cmd to load room gameplay scene or another one when connected to room server
        /// </summary>
        public string LoadScene { get { return "-msfLoadScene"; } }

        /// <summary>
        /// Use this cmd if youwant to connect to you database with some connection string
        /// </summary>
        public string DbConnectionString { get { return "-msfDbConnectionString"; } }

        /// <summary>
        /// Id of the lobby, for which the process was spawned
        /// </summary>
        public string LobbyId { get { return "-msfLobbyId"; } }

        /// <summary>
        /// Use this cmd to setup the max number of processes the spawner can spawn 
        /// </summary>
        public string MaxProcesses { get { return "-msfMaxProcesses"; } }

        /// <summary>
        /// Whether or not to use secure connection
        /// </summary>
        public string UseSsl { get { return "-msfUseSsl"; } }

        /// <summary>
        /// Defines path to certificate
        /// </summary>
        public string CertificatePath { get { return "-msfCertificatePath"; } }

        /// <summary>
        /// Defines path to certificate
        /// </summary>
        public string CertificatePassword { get { return "-msfCertificatePassword"; } }
    }
}