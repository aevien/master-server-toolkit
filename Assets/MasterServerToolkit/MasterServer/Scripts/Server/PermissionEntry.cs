using System;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Built-in permission keys used by the master server permission resolver.
    /// </summary>
    public static class MstPermissionKeys
    {
        /// <summary>
        /// Public client permission key.
        /// </summary>
        public const string Default = "default";

        /// <summary>
        /// Full administrative permission key.
        /// </summary>
        public const string Admin = "admin";

        /// <summary>
        /// Trusted room or service process permission key.
        /// </summary>
        public const string RoomServer = "room_server";

        /// <summary>
        /// Trusted process spawner permission key.
        /// </summary>
        public const string Spawner = "spawner";
    }

    /// <summary>
    /// Built-in permission level ranges used by the master server.
    /// </summary>
    public static class MstPermissionLevels
    {
        /// <summary>
        /// Lowest valid permission level.
        /// </summary>
        public const int Min = 0;

        /// <summary>
        /// Default public client permission level.
        /// </summary>
        public const int Default = 0;

        /// <summary>
        /// Lowest trusted user permission level.
        /// </summary>
        public const int TrustedUserMin = 1;

        /// <summary>
        /// Highest trusted user permission level.
        /// </summary>
        public const int TrustedUserMax = 20;

        /// <summary>
        /// Lowest trusted service or server process permission level.
        /// </summary>
        public const int ServiceMin = 100;

        /// <summary>
        /// Default trusted room or service process permission level.
        /// </summary>
        public const int RoomServer = ServiceMin;

        /// <summary>
        /// Default trusted process spawner permission level.
        /// </summary>
        public const int Spawner = ServiceMin;

        /// <summary>
        /// Full administrative permission level.
        /// </summary>
        public const int Admin = 999;

        /// <summary>
        /// Highest valid permission level.
        /// </summary>
        public const int Max = Admin;

        /// <summary>
        /// Clamps a permission level to the valid master server permission range.
        /// </summary>
        /// <param name="permissionLevel">Permission level to clamp.</param>
        /// <returns>Permission level clamped to the valid range.</returns>
        public static int Clamp(int permissionLevel)
        {
            return Math.Max(Min, Math.Min(Max, permissionLevel));
        }

        /// <summary>
        /// Determines whether a current permission level satisfies a required permission level.
        /// </summary>
        /// <param name="currentPermissionLevel">Current peer permission level.</param>
        /// <param name="requiredPermissionLevel">Required permission level.</param>
        /// <returns><c>true</c> when the current level satisfies the required level.</returns>
        public static bool HasAccess(int currentPermissionLevel, int requiredPermissionLevel)
        {
            return Clamp(currentPermissionLevel) >= Clamp(requiredPermissionLevel);
        }
    }

    /// <summary>
    /// Built-in credentials used when a permission secret is not overridden by configuration.
    /// </summary>
    public static class MstPermissionSecrets
    {
        public const string Default = "client-secret";
        public const string RoomServer = "room-secret";
        public const string Spawner = "spawner-secret";

        /// <summary>
        /// Returns the built-in secret assigned to a permission key by default.
        /// </summary>
        public static string GetDefault(string permissionKey)
        {
            return permissionKey switch
            {
                MstPermissionKeys.Default => Default,
                MstPermissionKeys.Admin => string.Empty,
                MstPermissionKeys.RoomServer => RoomServer,
                MstPermissionKeys.Spawner => Spawner,
                _ => permissionKey ?? string.Empty
            };
        }
    }

    /// <summary>
    /// Inspector-configured permission key mapped to a numeric permission level.
    /// </summary>
    [Serializable]
    public struct PermissionEntry
    {
        /// <summary>
        /// Permission key clients or server-side code can request.
        /// </summary>
        [Tooltip("Public permission identifier checked by server modules and requested during the permission handshake. Keys must be unique in the server permission list.")]
        public string key;

        /// <summary>
        /// Numeric permission level granted for this key.
        /// </summary>
        [Range(MstPermissionLevels.Min, MstPermissionLevels.Max), Tooltip("Numeric access level granted with this permission. Valid range is 0 to 999; exact-key checks remain authoritative for role-specific APIs.")]
        public int permissionLevel;

        /// <summary>
        /// Secret used to prove that a connection may receive this permission.
        /// Runtime configuration can override it by <see cref="key"/>.
        /// </summary>
        [Tooltip("HMAC credential required to request this permission during connection setup. An empty value is replaced with the built-in default for the key; deployment configuration may override it.")]
        public string secret;

        /// <summary>
        /// Creates a permission entry.
        /// </summary>
        /// <param name="key">Permission key.</param>
        /// <param name="permissionLevel">Numeric permission level.</param>
        public PermissionEntry(string key, int permissionLevel)
            : this(key, permissionLevel, MstPermissionSecrets.GetDefault(key))
        {
        }

        /// <summary>
        /// Creates a permission entry with an explicit secret.
        /// </summary>
        public PermissionEntry(string key, int permissionLevel, string secret)
        {
            this.key = key;
            this.permissionLevel = MstPermissionLevels.Clamp(permissionLevel);
            this.secret = string.IsNullOrWhiteSpace(secret)
                ? MstPermissionSecrets.GetDefault(key)
                : secret;
        }
    }
}
