#if UNITY_EDITOR
using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using System;
using System.IO;
using System.Security.Cryptography;

namespace MasterServerToolkit.Utils.Editor
{
    internal static class MstDemoAccessConfig
    {
        private const string RoomSecretFileName = ".mst-room-secret";
        private const string SpawnerSecretFileName = ".mst-spawner-secret";
        private const string TokenSecretFileName = ".mst-token-secret";

        public static void ConfigureClient(MstProperties properties)
        {
            properties.Set(Mst.Args.Names.PermissionCredentials,
                CreateCredentials(includeRoom: false, includeSpawner: false,
                    roomSecret: string.Empty, spawnerSecret: string.Empty));
        }

        public static void ConfigureRoom(MstProperties properties, string buildFolder)
        {
            properties.Set(Mst.Args.Names.PermissionCredentials,
                CreateCredentials(includeRoom: true, includeSpawner: false,
                    roomSecret: GetOrCreateSecret(buildFolder, RoomSecretFileName),
                    spawnerSecret: string.Empty));
        }

        public static void ConfigureMaster(MstProperties properties, string buildFolder)
        {
            properties.Set(
                Mst.Args.Names.TokenSecret,
                GetOrCreateSecret(buildFolder, TokenSecretFileName));
        }

        public static void ConfigureSpawner(MstProperties properties, string buildFolder,
            bool includeMasterCredentials)
        {
            string spawnerSecret = GetOrCreateSecret(buildFolder, SpawnerSecretFileName);
            string roomSecret = includeMasterCredentials
                ? GetOrCreateSecret(buildFolder, RoomSecretFileName)
                : string.Empty;

            if (includeMasterCredentials)
                ConfigureMaster(properties, buildFolder);

            properties.Set(Mst.Args.Names.PermissionCredentials,
                CreateCredentials(includeRoom: includeMasterCredentials, includeSpawner: true,
                    roomSecret: roomSecret, spawnerSecret: spawnerSecret));
        }

        private static string CreateCredentials(bool includeRoom, bool includeSpawner,
            string roomSecret, string spawnerSecret)
        {
            MstJson credentials = MstJson.CreateObject();
            credentials.AddField(MstPermissionKeys.Default, MstPermissionSecrets.Default);

            if (includeRoom)
                credentials.AddField(MstPermissionKeys.RoomServer, roomSecret);

            if (includeSpawner)
                credentials.AddField(MstPermissionKeys.Spawner, spawnerSecret);

            return credentials.ToString();
        }

        private static string GetOrCreateSecret(string buildFolder, string secretFileName)
        {
            string fullBuildFolder = Path.GetFullPath(buildFolder);
            string familyFolder = Directory.GetParent(fullBuildFolder)?.FullName ?? fullBuildFolder;
            string secretPath = Path.Combine(familyFolder, secretFileName);

            Directory.CreateDirectory(familyFolder);

            if (File.Exists(secretPath))
            {
                string existingSecret = File.ReadAllText(secretPath).Trim();

                if (!string.IsNullOrEmpty(existingSecret))
                    return existingSecret;
            }

            byte[] secretBytes = new byte[32];

            using (RandomNumberGenerator random = RandomNumberGenerator.Create())
                random.GetBytes(secretBytes);

            string secret = Convert.ToBase64String(secretBytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

            File.WriteAllText(secretPath, secret);
            return secret;
        }
    }
}
#endif
