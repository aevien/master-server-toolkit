using System;
using System.Linq;

namespace MasterServerToolkit.MasterServer
{
    public class MstArgs
    {
        private readonly string[] _args;

        /// <summary>
        /// If true, master server should be started
        /// </summary>
        public bool StartMaster { get; private set; }

        /// <summary>
        /// If true, spawner should be started
        /// </summary>
        public bool StartSpawner { get; private set; }

        /// <summary>
        /// If true, client will try to connect to master at start
        /// </summary>
        public bool StartClientConnection { get; private set; }

        /// <summary>
        /// Ip address to the master server
        /// </summary>
        public string MasterIp { get; private set; }

        /// <summary>
        /// Port, which will be open on the master server
        /// </summary>
        public int MasterPort { get; private set; }

        /// <summary>
        /// Room name
        /// </summary>
        public string RoomName { get; private set; }

        /// <summary>
        /// Set room as private or public
        /// </summary>
        public bool RoomIsPrivate { get; private set; }

        /// <summary>
        /// Public ip of the machine, on which the process is running
        /// </summary>
        public string RoomIp { get; private set; }

        /// <summary>
        /// Port, assigned to the spawned process (most likely a game server)
        /// </summary>
        public int RoomPort { get; private set; }

        /// <summary>
        /// Default room port. Set this cmd if you want a spawner to start creating room ports from your own specific value
        /// </summary>
        public int RoomDefaultPort { get; private set; }

        /// <summary>
        /// Max number of connections allowed
        /// </summary>
        public int RoomMaxConnections { get; private set; }

        /// <summary>
        /// Path to the executable (used by the spawner)
        /// </summary>
        public string RoomExecutablePath { get; private set; }

        /// <summary>
        /// Region, to which the spawner belongs
        /// </summary>
        public string RoomRegion { get; private set; }

        /// <summary>
        /// Password of the room
        /// </summary>
        public string RoomPassword { get; private set; }

        /// <summary>
        /// If true, some of the Ui game objects will be destroyed.
        /// (to avoid memory leaks)
        /// </summary>
        public bool DestroyUi { get; private set; }

        /// <summary>
        /// SpawnId of the spawned process
        /// </summary>
        public int SpawnTaskId { get; private set; }

        /// <summary>
        /// Code, which is used to ensure that there's no tampering with 
        /// spawned processes
        /// </summary>
        public string SpawnTaskUniqueCode { get; private set; }

        /// <summary>
        /// Max number of processes that can be spawned by a spawner
        /// </summary>
        public int MaxProcesses { get; private set; }

        /// <summary>
        /// Name of the scene to load
        /// </summary>
        public string LoadScene { get; private set; }

        /// <summary>
        /// Database connection string (user by some of the database implementations)
        /// </summary>
        public string DbConnectionString { get; private set; }

        /// <summary>
        /// LobbyId, which is assigned to a spawned process
        /// </summary>
        public int LobbyId { get; private set; }

        /// <summary>
        /// If true, it will be considered that we want to start server to
        /// support webgl clients
        /// </summary>
        public bool WebGl { get; private set; }

        /// <summary>
        /// Whether or not to use secure connection
        /// </summary>
        public bool UseSsl { get; private set; }

        /// <summary>
        /// Defines path to certificate
        /// </summary>
        public string CertificatePath { get; private set; }

        /// <summary>
        /// Defines path to certificate
        /// </summary>
        public string CertificatePassword { get; private set; }

        public MstArgNames Names { get; set; }

        public MstArgs()
        {
            _args = Environment.GetCommandLineArgs();

            // Android fix
            if (_args == null)
            {
                _args = Array.Empty<string>();
            }

            Names = new MstArgNames();

            StartMaster = IsProvided(Names.StartMaster);
            StartSpawner = IsProvided(Names.StartSpawner);
            StartClientConnection = IsProvided(Names.StartClientConnection);

            MasterPort = ExtractValueInt(Names.MasterPort, 5000);
            MasterIp = ExtractValue(Names.MasterIp, "127.0.0.1");

            RoomName = ExtractValue(Names.RoomName, "Room_" + Mst.Helper.CreateRandomString(5));
            RoomIp = ExtractValue(Names.RoomIp, "127.0.0.1");
            RoomPort = ExtractValueInt(Names.RoomPort, 7777);
            RoomDefaultPort = ExtractValueInt(Names.RoomDefaultPort, 1500);
            RoomExecutablePath = ExtractValue(Names.RoomExecutablePath);
            RoomRegion = ExtractValue(Names.RoomRegion, string.Empty);
            RoomMaxConnections = ExtractValueInt(Names.RoomMaxConnections, 10);
            RoomIsPrivate = !IsProvided(Names.RoomIsPrivate);
            RoomPassword = ExtractValue(Names.RoomPassword, string.Empty);

            SpawnTaskId = ExtractValueInt(Names.SpawnTaskId, -1);
            SpawnTaskUniqueCode = ExtractValue(Names.SpawnTaskUniqueCode);
            MaxProcesses = ExtractValueInt(Names.MaxProcesses, 0);

            LoadScene = ExtractValue(Names.LoadScene);

            DbConnectionString = ExtractValue(Names.DbConnectionString);

            LobbyId = ExtractValueInt(Names.LobbyId);
            WebGl = IsProvided(Names.UseWebSockets);

            UseSsl = IsProvided(Names.UseSsl);
            CertificatePath = ExtractValue(Names.CertificatePath);
            CertificatePassword = ExtractValue(Names.CertificatePassword);
        }

        public override string ToString()
        {
            return string.Join(" ", _args);
        }

        /// <summary>
        /// Extracts a string value for command line arguments provided
        /// </summary>
        /// <param name="argName"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public string ExtractValue(string argName, string defaultValue = null)
        {
            if (!_args.Contains(argName))
            {
                return defaultValue;
            }

            var index = _args.ToList().FindIndex(0, a => a.Equals(argName));
            return _args[index + 1];
        }

        /// <summary>
        /// Extracts an int string value for command line arguments provided
        /// </summary>
        /// <param name="argName"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public int ExtractValueInt(string argName, int defaultValue = -1)
        {
            var number = ExtractValue(argName, defaultValue.ToString());
            return Convert.ToInt32(number);
        }

        /// <summary>
        /// Check is given cmd is provided
        /// </summary>
        /// <param name="argName"></param>
        /// <returns></returns>
        public bool IsProvided(string argName)
        {
            return _args.Contains(argName);
        }
    }
}