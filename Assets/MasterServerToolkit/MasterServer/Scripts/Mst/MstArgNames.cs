namespace MasterServerToolkit.MasterServer
{
    public class MstArgNames
    {
        /// <summary>
        /// Starts the master server automatically after the Unity player launches.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstStartMaster=true
        /// </code>
        /// </example>
        public string StartMaster => "-mstStartMaster";

        /// <summary>
        /// Starts the client connection to the master server automatically after the Unity player launches.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstStartClientConnection=true
        /// </code>
        /// </example>
        public string StartClientConnection => "-mstStartClientConnection";

        /// <summary>
        /// Overrides the default application.cfg path used to load MST arguments. Relative paths are resolved from the build or project root; the derived environment key is MST_CONFIG_FILE. Config files may import additional defaults with @import "relative/path.cfg".
        /// </summary>
        /// <example>
        /// <code>
        /// -mstConfigFile=Configs/master.cfg
        /// </code>
        /// </example>
        public string ConfigFile => "-mstConfigFile";

        /// <summary>
        /// Sets the port opened by the master server socket.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstMasterPort=25200
        /// </code>
        /// </example>
        public string MasterPort => "-mstMasterPort";

        /// <summary>
        /// Sets the dashboard information identifier used by dashboard integrations.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstDashboardInfoId=master
        /// </code>
        /// </example>
        public string DashboardInfoId => "-mstDashboardInfoId";

        /// <summary>
        /// Sets the port opened by the dashboard server.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstDashboardPort=25201
        /// </code>
        /// </example>
        public string DashboardPort => "-mstDashboardPort";

        /// <summary>
        /// Sets the IP address or host used by the dashboard server.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstDashboardIp=localhost
        /// </code>
        /// </example>
        public string DashboardIp => "-mstDashboardIp";

        /// <summary>
        /// Sets the IP address or host used by clients and room servers to reach the master server.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstMasterIp=master.example.com
        /// </code>
        /// </example>
        public string MasterIp => "-mstMasterIp";

        /// <summary>
        /// Sets the spawn task identifier assigned to a spawned room process.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstSpawnerTaskId=0
        /// </code>
        /// </example>
        public string SpawnerTaskId => "-mstSpawnerTaskId";

        /// <summary>
        /// Sets the unique verification code used to detect tampering with spawned room processes.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstSpawnerTaskUniqueCode=spawn-task-secret
        /// </code>
        /// </example>
        public string SpawnerTaskUniqueCode => "-mstSpawnerTaskUniqueCode";

        /// <summary>
        /// Sets the maximum number of room processes that a spawner can run at the same time.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstSpawnerMaxProcesses=10
        /// </code>
        /// </example>
        public string SpawnerMaxProcesses => "-mstSpawnerMaxProcesses";

        /// <summary>
        /// Starts the room spawner automatically after the Unity player launches.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstSpawnerStart=true
        /// </code>
        /// </example>
        public string SpawnerStart => "-mstSpawnerStart";

        /// <summary>
        /// Sets the first room port that the spawner should use when allocating room server ports.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstSpawnerRoomDefaultPort=25300
        /// </code>
        /// </example>
        public string SpawnerRoomDefaultPort => "-mstSpawnerRoomDefaultPort";

        /// <summary>
        /// Sets the first redirected room port that the spawner should use when room servers listen behind a reverse proxy.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstSpawnerRoomDefaultRedirectPort=5300
        /// </code>
        /// </example>
        public string SpawnerRoomDefaultRedirectPort => "-mstSpawnerRoomDefaultRedirectPort";

        /// <summary>
        /// Sets the CPU usage limit for a spawned room process when the spawner supports process limits.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstRoomCpuLimit=80
        /// </code>
        /// </example>
        public string RoomCpuLimit => "-mstRoomCpuLimit";

        /// <summary>
        /// Sets the public IP address or host advertised by a spawned room server.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstRoomIp=room.example.com
        /// </code>
        /// </example>
        public string RoomIp => "-mstRoomIp";

        /// <summary>
        /// Sets the internal or redirected IP address used when rooms are behind NAT or a reverse proxy.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstRoomRedirectIp=room.example.com
        /// </code>
        /// </example>
        public string RoomRedirectIp => "-mstRoomRedirectIp";

        /// <summary>
        /// Sets the public port opened by a spawned room server.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstRoomPort=25300
        /// </code>
        /// </example>
        public string RoomPort => "-mstRoomPort";

        /// <summary>
        /// Sets the redirected room port used when rooms are behind NAT or a reverse proxy.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstRoomRedirectPort=5300
        /// </code>
        /// </example>
        public string RoomRedirectPort => "-mstRoomRedirectPort";

        /// <summary>
        /// Tells clients whether they should use a secure connection when connecting through the redirected room endpoint.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstRoomClientUseSecure=true
        /// </code>
        /// </example>
        public string RoomClientUseSecure => "-mstRoomClientUseSecure";

        /// <summary>
        /// Marks a room as private so clients must provide a password or invitation flow.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstRoomIsPrivate=false
        /// </code>
        /// </example>
        public string RoomIsPrivate => "-mstRoomIsPrivate";

        /// <summary>
        /// Sets the room process name, commonly used for console window titles and diagnostics.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstRoomName=The Last Dawn [PvP] [Europe]
        /// </code>
        /// </example>
        public string RoomName => "-mstRoomName";

        /// <summary>
        /// Sets the room title shown in room and lobby listings.
        /// The value may be a plain string or a single-line JSON object with language keys.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstRoomTitle=The Last Dawn [PvP] [Europe]
        /// -mstRoomTitle={"en":"The Last Dawn [PvP]","ru":"Последний Рассвет [PvP]","tr":"Son Safak [PvP]"}
        /// </code>
        /// </example>
        public string RoomTitle => "-mstRoomTitle";

        /// <summary>
        /// Sets the password required to join a private room.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstRoomPassword=room-password
        /// </code>
        /// </example>
        public string RoomPassword => "-mstRoomPassword";

        /// <summary>
        /// Sets the maximum number of client connections accepted by a room server.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstRoomMaxConnections=40
        /// </code>
        /// </example>
        public string RoomMaxConnections => "-mstRoomMaxConnections";

        /// <summary>
        /// Sets the executable path that the spawner should launch for a room server.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstRoomExe=C:\Games\my_game\Room\Room.exe
        /// </code>
        /// </example>
        public string RoomExecutablePath => "-mstRoomExe";

        /// <summary>
        /// Sets the region label used to group and filter spawners or rooms.
        /// The value may be a plain string or a single-line JSON object with language keys.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstRoomRegion=Europe
        /// -mstRoomRegion={"en":"Europe","ru":"Европа","tr":"Avrupa"}
        /// </code>
        /// </example>
        public string RoomRegion => "-mstRoomRegion";

        /// <summary>
        /// Sets the Unity scene that a room server should load for online gameplay.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstRoomOnlineScene=Online
        /// </code>
        /// </example>
        public string RoomOnlineScene => "-mstRoomOnlineScene";

        /// <summary>
        /// Enables WebSocket transport mode for room servers that support WebGL clients.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstUseWebSockets=true
        /// </code>
        /// </example>
        public string UseWebSockets => "-mstUseWebSockets";

        /// <summary>
        /// Sets the database connection string used by database bridge implementations.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstDatabaseConnectionString=Server=127.0.0.1;Database=cube_survival;Uid=root;Pwd=password;Port=3306;
        /// </code>
        /// </example>
        public string DatabaseConnectionString => "-mstDatabaseConnectionString";

        /// <summary>
        /// Sets database provider configuration data, such as a serialized connection profile or provider-specific connection string.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstDatabaseConfiguration=default
        /// </code>
        /// </example>
        public string DatabaseConfiguration => "-mstDatabaseConfiguration";

        /// <summary>
        /// Controls whether database connections should be closed automatically after operations when the provider supports it.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstDatabaseAutoCloseConnection=true
        /// </code>
        /// </example>
        public string DatabaseAutoCloseConnection => "-mstDatabaseAutoCloseConnection";

        /// <summary>
        /// Sets the database language or dialect type used by SQL-oriented providers.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstDatabaseLanguageType=MySql
        /// </code>
        /// </example>
        public string DatabaseLanguageType => "-mstDatabaseLanguageType";

        /// <summary>
        /// Sets the database provider type used by database bridge implementations.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstDatabaseProvider=MySql
        /// </code>
        /// </example>
        public string DatabaseProvider => "-mstDatabaseProvider";

        /// <summary>
        /// Sets the lobby identifier assigned to a spawned room process.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstLobbyId=1
        /// </code>
        /// </example>
        public string LobbyId => "-mstLobbyId";

        /// <summary>
        /// Enables secure connections to the master server socket.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstUseSecure=false
        /// </code>
        /// </example>
        public string UseSecure => "-mstUseSecure";

        /// <summary>
        /// Sets the path to the certificate file used for secure server connections.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstCertificatePath=certs/master.pfx
        /// </code>
        /// </example>
        public string CertificatePath => "-mstCertificatePath";

        /// <summary>
        /// Sets the password for the certificate used for secure server connections.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstCertificatePassword=certificate-password
        /// </code>
        /// </example>
        public string CertificatePassword => "-mstCertificatePassword";

        /// <summary>
        /// Overrides permission secrets by key for this process.
        /// </summary>
        /// <remarks>
        /// On a master server, keys must already exist in the Inspector permission list; configuration
        /// cannot create permissions or change their levels. On a connecting process, modules use the
        /// entries they need when requesting additional permissions. Values are redacted from MST
        /// argument diagnostics.
        /// </remarks>
        /// <example>
        /// <code>
        /// -mstPermissionCredentials={"default":"client-secret","room_server":"room-secret","spawner":"spawner-secret"}
        /// </code>
        /// </example>
        public string PermissionCredentials => "-mstPermissionCredentials";

        /// <summary>
        /// Enables important MST development mode features in modules that support them.
        /// </summary>
        /// <remarks>
        /// Use this flag only for development and test builds. It may enable dev-only behavior
        /// across different systems, such as allowing Unity Editor clients to connect to a built master server.
        /// Production servers should keep it disabled or omit it.
        /// </remarks>
        /// <example>
        /// <code>
        /// -mstUseDevMode=false
        /// </code>
        /// </example>
        public string UseDevMode => "-mstUseDevMode";

        /// <summary>
        /// Sets the target Unity frame rate for the server process.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstTargetFrameRate=60
        /// </code>
        /// </example>
        public string TargetFrameRate => "-mstTargetFrameRate";

        /// <summary>
        /// Sets the IP address or host used by the built-in web server.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstWebAddress=localhost
        /// </code>
        /// </example>
        public string WebAddress => "-mstWebAddress";

        /// <summary>
        /// Sets the port opened by the built-in web server.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstWebPort=25201
        /// </code>
        /// </example>
        public string WebPort => "-mstWebPort";

        /// <summary>
        /// Requires HTTP Basic Auth for every registered built-in web server route.
        /// Per-route credentials can still protect individual routes when this value is false.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstWebUseCredentials=true
        /// </code>
        /// </example>
        public string WebUseCredentials => "-mstWebUseCredentials";

        /// <summary>
        /// Sets an optional admin/service identifier for server-side service integrations.
        /// The built-in HTTP server does not use this value.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstAdminId=
        /// </code>
        /// </example>
        public string AdminId => "-mstAdminId";

        /// <summary>
        /// Sets the admin username required for built-in web server authentication.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstAdminUsername=admin
        /// </code>
        /// </example>
        public string AdminUsername => "-mstAdminUsername";

        /// <summary>
        /// Sets the admin password required for built-in web server authentication.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstAdminPassword=password
        /// </code>
        /// </example>
        public string AdminPassword => "-mstAdminPassword";

        /// <summary>
        /// Legacy admin username argument for built-in web server authentication.
        /// Prefer <see cref="AdminUsername"/> for new deployments.
        /// </summary>
        public string WebUsername => "-mstWebServerUsername";

        /// <summary>
        /// Legacy admin password argument for built-in web server authentication.
        /// Prefer <see cref="AdminPassword"/> for new deployments.
        /// </summary>
        public string WebPassword => "-mstWebServerPassword";

        /// <summary>
        /// Sets the authentication realm advertised by the built-in web server.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstWebServerRealm=Master Server
        /// </code>
        /// </example>
        public string WebRealm => "-mstWebServerRealm";

        /// <summary>
        /// Enables CORS response headers for the built-in web server.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstWebCorsEnabled=true
        /// </code>
        /// </example>
        public string WebCorsEnabled => "-mstWebCorsEnabled";

        /// <summary>
        /// Sets allowed CORS origins for the built-in web server.
        /// Use '*' for anonymous public access or a comma-separated exact-origin list.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstWebAllowedOrigins=https://admin.example.com,http://localhost:5173
        /// </code>
        /// </example>
        public string WebAllowedOrigins => "-mstWebAllowedOrigins";

        /// <summary>
        /// Sets allowed CORS methods for preflight responses.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstWebAllowedMethods=GET,POST,PUT,DELETE,OPTIONS,HEAD
        /// </code>
        /// </example>
        public string WebAllowedMethods => "-mstWebAllowedMethods";

        /// <summary>
        /// Sets allowed CORS request headers for preflight responses.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstWebAllowedHeaders=Content-Type,Authorization,Accept,X-Requested-With
        /// </code>
        /// </example>
        public string WebAllowedHeaders => "-mstWebAllowedHeaders";

        /// <summary>
        /// Allows credentialed CORS requests only when explicit origins are configured.
        /// This is ignored when allowed origins contains '*'.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstWebAllowCredentials=true
        /// </code>
        /// </example>
        public string WebAllowCredentials => "-mstWebAllowCredentials";

        /// <summary>
        /// Sets the browser CORS preflight cache duration in seconds.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstWebCorsMaxAge=86400
        /// </code>
        /// </example>
        public string WebCorsMaxAge => "-mstWebCorsMaxAge";

        /// <summary>
        /// Sets how often the built-in web server heartbeat check should run.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstWebServerHeartbeatCheckInterval=10
        /// </code>
        /// </example>
        public string WebServerHeartbeatCheckInterval => "-mstWebServerHeartbeatCheckInterval";

        /// <summary>
        /// Sets the built-in web server page used for heartbeat checks.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstWebServerHeartbeatCheckPage=/health
        /// </code>
        /// </example>
        public string WebServerHeartbeatCheckPage => "-mstWebServerHeartbeatCheckPage";

        /// <summary>
        /// Sets how often analytics data should be sent by analytics-enabled modules.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstAnalyticsSendInterval=60
        /// </code>
        /// </example>
        public string AnalyticsSendInterval => "-mstAnalyticsSendInterval";

        /// <summary>
        /// Sets the MST file log path.
        /// </summary>
        /// <remarks>
        /// Relative paths are resolved from the Unity player root. The path may use file-name tokens:
        /// {spawnId}, {pid}, {roomName}, {roomPort}, {roomRedirectPort}, {masterPort}, {date}, and {channelId}.
        /// The {channelId} token routes non-system channels into separate files. The System channel omits
        /// the token value so default system logs keep the base file name.
        /// </remarks>
        /// <example>
        /// <code>
        /// -mstLogFilePath=Logs/master_{date}.log
        /// -mstLogFilePath=Logs/rooms/{spawnId}_{roomName}_{roomPort}_{channelId}_{date}.log
        /// </code>
        /// </example>
        public string LogFilePath => "-mstLogFilePath";

        /// <summary>
        /// Sets the minimum MST log level written to the configured log file.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstLogFileMinLevel=Info
        /// -mstLogFileMinLevel=Debug
        /// </code>
        /// </example>
        public string LogFileMinLevel => "-mstLogFileMinLevel";

        /// <summary>
        /// Controls whether the MST file appender should append to existing files when the process starts.
        /// </summary>
        /// <remarks>
        /// The default is false, so matching log file names are overwritten on startup.
        /// </remarks>
        /// <example>
        /// <code>
        /// -mstLogFileAppend=false
        /// </code>
        /// </example>
        public string LogFileAppend => "-mstLogFileAppend";

        /// <summary>
        /// Controls whether Unity log messages should be mirrored into the configured MST log file.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstLogUnityMessages=true
        /// </code>
        /// </example>
        public string LogUnityMessages => "-mstLogUnityMessages";

        /// <summary>
        /// Sets comma-separated channels accepted by the MST file appender.
        /// </summary>
        /// <remarks>
        /// The default is System. Use * to include every channel. When LogFilePath contains {channelId},
        /// accepted channels are routed into files by their actual log channel. System omits the channel id in file names.
        /// </remarks>
        /// <example>
        /// <code>
        /// -mstLogChannels=System
        /// -mstLogChannels=System,Chat,Economy
        /// -mstLogChannels=*
        /// </code>
        /// </example>
        public string LogChannels => "-mstLogChannels";

        /// <summary>
        /// Sets comma-separated channels written by the MST console appender. The default is System; use * to include every channel.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstConsoleLogChannels=System
        /// -mstConsoleLogChannels=System,Chat
        /// </code>
        /// </example>
        public string ConsoleLogChannels => "-mstConsoleLogChannels";

        /// <summary>
        /// Sets the timeout after which inactive clients are disconnected.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstClientInactivityTimeout=120
        /// </code>
        /// </example>
        public string ClientInactivityTimeout => "-mstClientInactivityTimeout";

        /// <summary>
        /// Sets the timeout allowed for client validation during connection or authentication flows.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstClientValidationTimeout=10
        /// </code>
        /// </example>
        public string ClientValidationTimeout => "-mstClientValidationTimeout";

        /// <summary>
        /// Sets the SMTP host used for outgoing email.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstSmtpHost=smtp.example.com
        /// </code>
        /// </example>
        public string SmtpHost => "-mstSmtpHost";

        /// <summary>
        /// Sets the SMTP username used for outgoing email authentication.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstSmtpUsername=user@example.com
        /// </code>
        /// </example>
        public string SmtpUsername => "-mstSmtpUsername";

        /// <summary>
        /// Sets the SMTP password used for outgoing email authentication.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstSmtpPassword=password
        /// </code>
        /// </example>
        public string SmtpPassword => "-mstSmtpPassword";

        /// <summary>
        /// Sets the SMTP port used for outgoing email.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstSmtpPort=587
        /// </code>
        /// </example>
        public string SmtpPort => "-mstSmtpPort";

        /// <summary>
        /// Enables SSL/TLS for SMTP connections.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstSmtpEnableSSL=true
        /// </code>
        /// </example>
        public string SmtpEnableSSL => "-mstSmtpEnableSSL";

        /// <summary>
        /// Sets the SMTP request timeout.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstSmtpTimeout=10
        /// </code>
        /// </example>
        public string SmtpTimeout => "-mstSmtpTimeout";

        /// <summary>
        /// Sets the sender email address used in outgoing email.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstSmtpMailFrom=no-reply@example.com
        /// </code>
        /// </example>
        public string SmtpMailFrom => "-mstSmtpMailFrom";

        /// <summary>
        /// Sets the sender display name used in outgoing email.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstSmtpSenderDisplayName=Cube Survivinator
        /// </code>
        /// </example>
        public string SmtpSenderDisplayName => "-mstSmtpSenderDisplayName";

        /// <summary>
        /// Sets the default language code used by localization-aware MST modules.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstDefaultLanguage=en
        /// </code>
        /// </example>
        public string DefaultLanguage => "-mstDefaultLanguage";

        /// <summary>
        /// Sets the secret key used to sign and validate authentication tokens.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstTokenSecret=change-this-token-secret
        /// </code>
        /// </example>
        public string TokenSecret => "-mstTokenSecret";

        /// <summary>
        /// Sets the persistent master security key-ring file.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstSecurityKeyRingFile=Configs/mst-security.keys.json
        /// </code>
        /// </example>
        public string SecurityKeyRingFile => "-mstSecurityKeyRingFile";

        /// <summary>
        /// Sets the number of days before generated authentication tokens expire.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstTokenExpiresInDays=7
        /// </code>
        /// </example>
        public string TokenExpiresInDays => "-mstTokenExpiresInDays";

        /// <summary>
        /// Sets the issuer value written into generated authentication tokens.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstTokenIssuer=my-game
        /// </code>
        /// </example>
        public string TokenIssuer => "-mstTokenIssuer";

        /// <summary>
        /// Sets the audience value written into generated authentication tokens.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstTokenAudience=my-game-client
        /// </code>
        /// </example>
        public string TokenAudience => "-mstTokenAudience";

        /// <summary>
        /// Enables or disables the MST analytics module.
        /// </summary>
        /// <example>
        /// <code>
        /// -mstUseAnalyticsModule=false
        /// </code>
        /// </example>
        public string UseAnalyticsModule => "-mstUseAnalyticsModule";
    }
}
