namespace MasterServerToolkit.MasterServer
{
    public class MstArgNames
    {
        /// <summary>
        /// Use this cmd to start master server after unity player is started
        /// </summary>
        public string StartMaster { get { return "-mstStartMaster"; } }

        /// <summary>
        /// Use this cmd to start server room spawner after unity player is started
        /// </summary>
        public string StartSpawner { get { return "-mstStartSpawner"; } }

        /// <summary>
        /// Use this cmd to start client connection to server after unity player is started
        /// </summary>
        public string StartClientConnection { get { return "-mstStartClientConnection"; } }

        /// <summary>
        /// Use this cmd to setup master server connection port
        /// </summary>
        public string MasterPort { get { return "-mstMasterPort"; } }

        /// <summary>
        /// Use this cmd to setup master server connection IP address
        /// </summary>
        public string MasterIp { get { return "-mstMasterIp"; } }

        /// <summary>
        /// Use this cmd to setup spawned process task ID
        /// </summary>
        public string SpawnTaskId { get { return "-mstSpawnTaskId"; } }

        /// <summary>
        /// Use this cmd to check if there's no tampering with spawned processes
        /// </summary>
        public string SpawnTaskUniqueCode { get { return "-mstSpawnTaskUniqueCode"; } }

        /// <summary>
        /// Use this cmd to setup IP address of the spawned room server
        /// </summary>
        public string RoomIp { get { return "-mstRoomIp"; } }

        /// <summary>
        /// Use this cmd to setup port of the spawned room server
        /// </summary>
        public string RoomPort { get { return "-mstRoomPort"; } }

        /// <summary>
        /// Use this cmd if you want a spawner to start creating room ports from your own specific value
        /// </summary>
        public string RoomDefaultPort { get { return "-mstRoomDefaultPort"; } }

        /// <summary>
        /// Use this cmd to setup server room as provate or not
        /// </summary>
        public string RoomIsPrivate { get { return "-mstRoomIsPrivate"; } }

        /// <summary>
        /// Use this cmd to setup server room name
        /// </summary>
        public string RoomName { get { return "-mstRoomName"; } }

        /// <summary>
        /// Use this cmd to setup server room password
        /// </summary>
        public string RoomPassword { get { return "-mstRoomPassword"; } }

        /// <summary>
        /// Use this cmd to setup the max number of connections of the spawned room server
        /// </summary>
        public string RoomMaxConnections { get { return "-mstRoomMaxConnections"; } }

        /// <summary>
        /// Use this cmd to setup the path to room server executable file that you need to spawn
        /// </summary>
        public string RoomExecutablePath { get { return "-mstRoomExe"; } }

        /// <summary>
        /// Use this cmd to setup the region, to which the spawner belongs
        /// </summary>
        public string RoomRegion { get { return "-mstRoomRegion"; } }

        /// <summary>
        /// Use this cmd to setup WebSockets mode on room server if you use WebGL version of client
        /// this feature works only is you server supports web sockets
        /// </summary>
        public string UseWebSockets { get { return "-mstUseWebSockets"; } }

        /// <summary>
        /// Send this cmd to load room gameplay scene or another one when connected to room server
        /// </summary>
        public string LoadScene { get { return "-mstLoadScene"; } }

        /// <summary>
        /// Use this cmd if youwant to connect to you database with some connection string
        /// </summary>
        public string DbConnectionString { get { return "-mstDbConnectionString"; } }

        /// <summary>
        /// Id of the lobby, for which the process was spawned
        /// </summary>
        public string LobbyId { get { return "-mstLobbyId"; } }

        /// <summary>
        /// Use this cmd to setup the max number of processes the spawner can spawn 
        /// </summary>
        public string MaxProcesses { get { return "-mstMaxProcesses"; } }

        /// <summary>
        /// Application key
        /// </summary>
        public string ApplicationKey { get { return "-mstAppKey"; } }

        /// <summary>
        /// Whether or not to use secure connection
        /// </summary>
        public string UseSecure { get { return "-mstUseSecure"; } }

        /// <summary>
        /// Defines path to certificate
        /// </summary>
        public string CertificatePath { get { return "-mstCertificatePath"; } }

        /// <summary>
        /// Defines path to certificate
        /// </summary>
        public string CertificatePassword { get { return "-mstCertificatePassword"; } }

        /// <summary>
        /// Enable dev mode
        /// </summary>
        public string UseDevMode { get { return "-mstUseDevMode"; } }

        /// <summary>
        /// Instructs the game to try to render at a specified frame rate
        /// </summary>
        public string TargetFrameRate { get { return "-mstTargetFrameRate"; } }

        /// <summary>
        /// Use this cmd to setup web server connection address
        /// </summary>
        public string WebAddress { get { return "-mstWebAddress"; } }

        /// <summary>
        /// Use this cmd to setup web server connection port
        /// </summary>
        public string WebPort { get { return "-mstWebPort"; } }

        /// <summary>
        /// Use this cmd to setup web server root directory
        /// </summary>
        public string WebRootDir { get { return "-mstWebRootDir"; } }
    }
}