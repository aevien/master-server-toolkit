using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MasterServerToolkit.MasterServer
{
    public class MstArgs
    {
        private string[] _args;

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
        public ushort RoomPort { get; private set; }

        /// <summary>
        /// Default room port. Set this cmd if you want a spawner to start creating room ports from your own specific value
        /// </summary>
        public int RoomDefaultPort { get; private set; }

        /// <summary>
        /// Max number of connections allowed
        /// </summary>
        public ushort RoomMaxConnections { get; private set; }

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
        public bool UseWebSockets { get; private set; }

        /// <summary>
        /// Whether or not to use secure connection
        /// </summary>
        public bool UseSecure { get; private set; }

        /// <summary>
        /// Defines path to certificate
        /// </summary>
        public string CertificatePath { get; private set; }

        /// <summary>
        /// Defines path to certificate
        /// </summary>
        public string CertificatePassword { get; private set; }

        /// <summary>
        /// Enable dev mode
        /// </summary>
        public bool UseDevMode { get; private set; }

        /// <summary>
        /// Instructs the game to try to render at a specified frame rate
        /// </summary>
        public int TargetFrameRate { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public string WebAddress { get; private set; }

        /// <summary>
        /// Port, which will be open on the web server
        /// </summary>
        public int WebPort { get; private set; }

        /// <summary>
        /// Log file path
        /// </summary>
        public string LogFileDir { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public string DefaultLanguage { get; private set; }

        public MstArgNames Names { get; set; }

        public MstArgs()
        {
            ParseArguments();

            Names = new MstArgNames();

            StartMaster = AsBool(Names.StartMaster, false);
            StartSpawner = AsBool(Names.StartSpawner, false);
            StartClientConnection = AsBool(Names.StartClientConnection, false);

            MasterPort = AsInt(Names.MasterPort, 5000);
            MasterIp = AsString(Names.MasterIp, "127.0.0.1");

            WebAddress = AsString(Names.WebAddress, "127.0.0.1");
            WebPort = AsInt(Names.WebPort, 8080);

            RoomName = AsString(Names.RoomName);
            RoomIp = AsString(Names.RoomIp, "127.0.0.1");
            RoomPort = (ushort)AsInt(Names.RoomPort, 7777);
            RoomDefaultPort = AsInt(Names.RoomDefaultPort, 1500);
            RoomExecutablePath = AsString(Names.RoomExecutablePath);
            RoomRegion = AsString(Names.RoomRegion);
            RoomMaxConnections = (ushort)AsInt(Names.RoomMaxConnections, 10);
            RoomIsPrivate = AsBool(Names.RoomIsPrivate, false);
            RoomPassword = AsString(Names.RoomPassword);

            SpawnTaskId = AsInt(Names.SpawnTaskId, -1);
            SpawnTaskUniqueCode = AsString(Names.SpawnTaskUniqueCode);
            MaxProcesses = AsInt(Names.MaxProcesses, 0);

            LoadScene = AsString(Names.RoomOnlineScene);

            DbConnectionString = AsString(Names.DbConnectionString);

            LobbyId = AsInt(Names.LobbyId);
            UseWebSockets = AsBool(Names.UseWebSockets, false);

            UseSecure = AsBool(Names.UseSecure, false);
            CertificatePath = AsString(Names.CertificatePath);
            CertificatePassword = AsString(Names.CertificatePassword);

            UseDevMode = AsBool(Names.UseDevMode, false);

            TargetFrameRate = AsInt(Names.TargetFrameRate, 60);

            DefaultLanguage = AsString(Names.DefaultLanguage);
        }

        private void ParseArguments()
        {
            _args = Environment.GetCommandLineArgs();

            // Android fix
            if (_args == null)
            {
                _args = Array.Empty<string>();
            }

#if !UNITY_EDITOR
            if (UnityEngine.Application.isMobilePlatform)
            {
                return;
            }
#endif

            string path = AppConfigFile();

            string[] lines = File.ReadAllLines(path);
            List<string> newArgs = new List<string>();
            newArgs.AddRange(_args);

            //Load from .env
            foreach (DictionaryEntry env in Environment.GetEnvironmentVariables())
            {
                newArgs.Add("-" + (string)env.Key);
                newArgs.Add((string)env.Value);
            }

            if (lines != null && lines.Length > 0)
            {
                foreach (string line in lines)
                {
                    if (!IsComment(line))
                    {
                        var kvp = Parse(line, "=");

                        if (!string.IsNullOrEmpty(kvp.Key) && !newArgs.Contains(kvp.Key))
                        {
                            newArgs.Add(kvp.Key);
                            newArgs.Add(kvp.Value);
                        }
                    }
                }
            }

            _args = newArgs.ToArray();
        }

        private KeyValuePair<string, string> Parse(string input, string splitter)
        {
            int splitterIndex = input.IndexOf(splitter);

            if (splitterIndex >= 0)
            {
                string key = input.Substring(0, splitterIndex);
                string value = input.Substring(splitterIndex + 1);
                return new KeyValuePair<string, string>(key, value);
            }

            return default;
        }

        private bool IsComment(string line)
        {
            return line.StartsWith("#");
        }

        /// <summary>
        /// Searches for keys by filter
        /// </summary>
        /// <param name="keysFilter"></param>
        /// <returns></returns>
        public string[] FindKeys(string keysFilter)
        {
            return _args.Where(i => i.StartsWith(keysFilter)).ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="rootPath"></param>
        /// <returns></returns>
        public string AppConfigFile(string rootPath = "")
        {
            string path;

            if (string.IsNullOrEmpty(rootPath))
            {
                path = Path.Combine(Directory.GetCurrentDirectory(), "application.cfg");
            }
            else
            {
                path = Path.Combine(rootPath, "application.cfg");
            }

            if (!File.Exists(path))
            {
                using (var file = File.Create(path))
                    file.Close();
            }

            return path;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
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
        public string AsString(string argName, string defaultValue = "")
        {
            if (!IsProvided(argName))
            {
                return defaultValue;
            }

            var index = _args.ToList().FindIndex(0, a => a.Equals(argName));
            return _args[index + 1];
        }

        /// <summary>
        /// Extracts an int value for command line arguments provided
        /// </summary>
        /// <param name="argName"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public int AsInt(string argName, int defaultValue = -1)
        {
            try
            {
                var value = AsString(argName, defaultValue.ToString());
                return Convert.ToInt32(value);
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Extracts an int value for command line arguments provided
        /// </summary>
        /// <param name="argName"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public float AsFloat(string argName, float defaultValue = -1)
        {
            try
            {
                var value = AsString(argName, defaultValue.ToString());
                return Convert.ToSingle(value);
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Extracts a bool value for command line arguments provided
        /// </summary>
        /// <param name="argName"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public bool AsBool(string argName, bool defaultValue = false)
        {
            try
            {
                var value = AsString(argName, defaultValue.ToString());
                return Convert.ToBoolean(value);
            }
            catch
            {
                return defaultValue;
            }
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