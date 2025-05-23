﻿namespace MasterServerToolkit.MasterServer
{
    public class MstArgNames
    {
        /// <summary>
        /// Use this cmd to start master server after unity player is started
        /// </summary>
        public string StartMaster => "-mstStartMaster";

        /// <summary>
        /// Use this cmd to start server room spawner after unity player is started
        /// </summary>
        public string StartSpawner => "-mstStartSpawner";

        /// <summary>
        /// Use this cmd to start client connection to server after unity player is started
        /// </summary>
        public string StartClientConnection => "-mstStartClientConnection";

        /// <summary>
        /// Use this cmd to setup master server connection port
        /// </summary>
        public string MasterPort => "-mstMasterPort";

        /// <summary>
        /// Use this cmd to setup dashboard info id
        /// </summary>
        public string DashboardInfoId => "-mstDashboardInfoId";

        /// <summary>
        /// Use this cmd to setup dashboard server connection port
        /// </summary>
        public string DashboardPort => "-mstDashboardPort";

        /// <summary>
        /// Use this cmd to setup dashboard server connection IP address
        /// </summary>
        public string DashboardIp => "-mstDashboardIp";

        /// <summary>
        /// Use this cmd to setup master server connection IP address
        /// </summary>
        public string MasterIp => "-mstMasterIp";

        /// <summary>
        /// Use this cmd to setup spawned process task ID
        /// </summary>
        public string SpawnTaskId => "-mstSpawnTaskId";

        /// <summary>
        /// Use this cmd to check if there's no tampering with spawned processes
        /// </summary>
        public string SpawnTaskUniqueCode => "-mstSpawnTaskUniqueCode";

        /// <summary>
        /// 
        /// </summary>
        public string RoomCpuLimit => "-mstRoomCpuLimit";

        /// <summary>
        /// Use this cmd to setup IP address of the spawned room server
        /// </summary>
        public string RoomIp => "-mstRoomIp";

        /// <summary>
        /// Use this cmd to setup local IP address of the spawned room server. Fro reverse-proxy or NAT
        /// </summary>
        public string RoomLocalIp => "-mstLoalRoomIp";

        /// <summary>
        /// Use this cmd to setup port of the spawned room server
        /// </summary>
        public string RoomPort => "-mstRoomPort";

        /// <summary>
        /// Port, assigned to the spawned process (most likely a game server). Reverse-proxy version
        /// </summary>
        public string RoomLocalPort => "-mstRoomLocalPort";

        /// <summary>
        /// This parameter is passed to the client to specify which connection to the room it should make, secure or not. Reverse-proxy version
        /// </summary>
        public string RoomClientUseSecure => "-mstRoomClientUseSecure";

        /// <summary>
        /// Use this cmd if you want a spawner to start creating room ports from your own specific value
        /// </summary>
        public string RoomDefaultPort => "-mstRoomDefaultPort";

        /// <summary>
        /// Use this cmd to setup server room as provate or not
        /// </summary>
        public string RoomIsPrivate => "-mstRoomIsPrivate";

        /// <summary>
        /// Use this cmd to setup server room name
        /// </summary>
        public string RoomName => "-mstRoomName";

        /// <summary>
        /// Use this cmd to setup server room password
        /// </summary>
        public string RoomPassword => "-mstRoomPassword";

        /// <summary>
        /// Use this cmd to setup the max number of connections of the spawned room server
        /// </summary>
        public string RoomMaxConnections => "-mstRoomMaxConnections";

        /// <summary>
        /// Use this cmd to setup the path to room server executable file that you need to spawn
        /// </summary>
        public string RoomExecutablePath => "-mstRoomExe";

        /// <summary>
        /// Use this cmd to setup the region, to which the spawner belongs
        /// </summary>
        public string RoomRegion => "-mstRoomRegion";

        /// <summary>
        /// Send this cmd to load room gameplay scene or another one when connected to room server
        /// </summary>
        public string RoomOnlineScene => "-mstRoomOnlineScene";

        /// <summary>
        /// Use this cmd to setup WebSockets mode on room server if you use WebGL version of client
        /// this feature works only is you server supports web sockets
        /// </summary>
        public string UseWebSockets => "-mstUseWebSockets";

        /// <summary>
        /// Use this cmd if youwant to connect to you database with some connection string
        /// </summary>
        public string DatabaseConnectionString => "-mstDatabaseConnectionString";

        /// <summary>
        /// 
        /// </summary>
        public string DatabaseConfiguration => "-mstDatabaseConfiguration";

        /// <summary>
        /// 
        /// </summary>
        public string DatabaseAutoCloseConnection => "-mstDatabaseAutoCloseConnection";

        /// <summary>
        /// 
        /// </summary>
        public string DatabaseLanguageType => "-mstDatabaseLanguageType";

        /// <summary>
        /// 
        /// </summary>
        public string DatabaseProvider => "-mstDatabaseProvider";

        /// <summary>
        /// Id of the lobby, for which the process was spawned
        /// </summary>
        public string LobbyId => "-mstLobbyId";

        /// <summary>
        /// Use this cmd to setup the max number of processes the spawner can spawn 
        /// </summary>
        public string MaxProcesses => "-mstMaxProcesses";

        /// <summary>
        /// Whether or not to use secure connection to server(not room)
        /// </summary>
        public string UseSecure => "-mstUseSecure";

        /// <summary>
        /// Defines path to certificate
        /// </summary>
        public string CertificatePath => "-mstCertificatePath";

        /// <summary>
        /// Defines path to certificate
        /// </summary>
        public string CertificatePassword => "-mstCertificatePassword";

        /// <summary>
        /// Enable dev mode
        /// </summary>
        public string UseDevMode => "-mstUseDevMode";

        /// <summary>
        /// Instructs the game to try to render at a specified frame rate
        /// </summary>
        public string TargetFrameRate => "-mstTargetFrameRate";

        /// <summary>
        /// Use this cmd to setup web server connection address
        /// </summary>
        public string WebAddress => "-mstWebAddress";

        /// <summary>
        /// Use this cmd to setup web server connection port
        /// </summary>
        public string WebPort => "-mstWebPort";

        /// <summary>
        /// Defines username of web server
        /// </summary>
        public string WebUsername => "-mstWebServerUsername";

        /// <summary>
        /// Defines password of web server
        /// </summary>
        public string WebPassword => "-mstWebServerPassword";

        /// <summary>
        /// 
        /// </summary>
        public string WebServerHeartbeatCheckInterval => "-mstWebServerHeartbeatCheckInterval";

        /// <summary>
        /// 
        /// </summary>
        public string WebServerHeartbeatCheckPage => "-mstWebServerHeartbeatCheckPage";

        /// <summary>
        /// 
        /// </summary>
        public string AnalyticsSendInterval => "-mstAnalyticsSendInterval";

        /// <summary>
        /// 
        /// </summary>
        public string ClientInactivityTimeout => "-mstClientInactivityTimeout";

        /// <summary>
        /// 
        /// </summary>
        public string ClientValidationTimeout => "-mstClientValidationTimeout";

        /// <summary>
        /// 
        /// </summary>
        public string SmtpHost => "-mstSmtpHost";

        /// <summary>
        /// 
        /// </summary>
        public string SmtpUsername => "-mstSmtpUsername";

        /// <summary>
        /// 
        /// </summary>
        public string SmtpPassword => "-mstSmtpPassword";

        /// <summary>
        /// 
        /// </summary>
        public string SmtpPort => "-mstSmtpPort";

        /// <summary>
        /// 
        /// </summary>
        public string SmtpEnableSSL => "-mstSmtpEnableSSL";

        /// <summary>
        /// 
        /// </summary>
        public string SmtpTimeout => "-mstSmtpTimeout";

        /// <summary>
        /// 
        /// </summary>
        public string SmtpMailFrom => "-mstSmtpMailFrom";

        /// <summary>
        /// 
        /// </summary>
        public string SmtpSenderDisplayName => "-mstSmtpSenderDisplayName";

        /// <summary>
        /// 
        /// </summary>
        public string DefaultLanguage => "-mstDefaultLanguage";

        /// <summary>
        /// Use this cmd to set the secret key for token encryption and decryption.
        /// </summary>
        public string TokenSecret => "-mstTokenSecret";

        /// <summary>
        /// Use this cmd to define the number of days before the generated token expires.
        /// </summary>
        public string TokenExpiresInDays => "-mstTokenExpiresInDays";

        /// <summary>
        /// Use this cmd to specify the issuer of the token for authentication purposes.
        /// </summary>
        public string TokenIssuer => "-mstTokenIssuer";

        /// <summary>
        /// Use this cmd to define the audience for which the token is intended.
        /// </summary>
        public string TokenAudience => "-mstTokenAudience";

        /// <summary>
        /// Use this cmd to enable/disable analytics module
        /// </summary>
        public string UseAnalyticsModule => "-mstUseAnalyticsModule";
    }
}