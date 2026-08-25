using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public class SecurityInfoPeerExtension : IPeerExtension
    {
        private sealed class PendingPermissionChallenge
        {
            public string PermissionKey { get; set; }
            public byte[] Nonce { get; set; }
            public long ExpiresAtUtcTicks { get; set; }
        }

        private sealed class PendingSealChallenge
        {
            public string Purpose { get; set; }
            public ushort TargetOpCode { get; set; }
            public string KeyId { get; set; }
            public byte[] Nonce { get; set; }
            public long ExpiresAtUtcTicks { get; set; }
        }

        private const int MaxPendingPermissionChallenges = 16;
        private const int MaxPendingSealChallenges = 16;

        private readonly object permissionsSync = new();
        private readonly object sealChallengesSync = new();
        private readonly Dictionary<string, int> grantedPermissions = new(StringComparer.Ordinal);
        private readonly Dictionary<string, PendingPermissionChallenge> pendingPermissionChallenges =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, PendingSealChallenge> pendingSealChallenges =
            new(StringComparer.Ordinal);
        private int accountPermissionLevel;

        internal object PermissionLifecycleSync => permissionsSync;

        public int PermissionLevel
        {
            get
            {
                lock (permissionsSync)
                    return Math.Max(GetConnectionPermissionLevelUnsafe(), accountPermissionLevel);
            }
        }

        public int ConnectionPermissionLevel
        {
            get
            {
                lock (permissionsSync)
                    return GetConnectionPermissionLevelUnsafe();
            }
        }

        public int AccountPermissionLevel
        {
            get
            {
                lock (permissionsSync)
                    return accountPermissionLevel;
            }
        }

        public bool IsConnectionAuthenticated => HasPermission(MstPermissionKeys.Default);

        public Guid UniqueGuid { get; set; }
        public IPeer Peer { get; private set; }

        public SecurityInfoPeerExtension(IPeer peer)
        {
            Peer = peer ?? throw new ArgumentNullException(nameof(peer));
        }

        public bool TryIssuePermissionChallenge(string permissionKey, byte[] challengeId,
            byte[] nonce, long expiresAtUtcTicks)
        {
            if (string.IsNullOrWhiteSpace(permissionKey) || challengeId == null || nonce == null)
                return false;

            string challengeKey = Convert.ToBase64String(challengeId);

            lock (permissionsSync)
            {
                RemoveExpiredPermissionChallengesUnsafe(DateTime.UtcNow.Ticks);

                if (pendingPermissionChallenges.Count >= MaxPendingPermissionChallenges ||
                    pendingPermissionChallenges.ContainsKey(challengeKey))
                {
                    return false;
                }

                foreach (PendingPermissionChallenge pending in pendingPermissionChallenges.Values)
                {
                    if (string.Equals(pending.PermissionKey, permissionKey, StringComparison.Ordinal))
                        return false;
                }

                pendingPermissionChallenges.Add(challengeKey, new PendingPermissionChallenge
                {
                    PermissionKey = permissionKey,
                    Nonce = nonce,
                    ExpiresAtUtcTicks = expiresAtUtcTicks
                });
                return true;
            }
        }

        internal bool TryIssueSealChallenge(
            string purpose,
            ushort targetOpCode,
            string keyId,
            byte[] challengeId,
            byte[] nonce,
            long expiresAtUtcTicks)
        {
            if (string.IsNullOrWhiteSpace(purpose) ||
                targetOpCode == 0 ||
                string.IsNullOrWhiteSpace(keyId) ||
                challengeId == null ||
                challengeId.Length != MstSecurityProtocol.ChallengeIdSize ||
                nonce == null ||
                nonce.Length != MstSecurityProtocol.ChallengeNonceSize)
            {
                return false;
            }

            string challengeKey = Convert.ToBase64String(challengeId);

            lock (sealChallengesSync)
            {
                RemoveExpiredSealChallengesUnsafe(DateTime.UtcNow.Ticks);

                if (pendingSealChallenges.Count >= MaxPendingSealChallenges ||
                    pendingSealChallenges.ContainsKey(challengeKey))
                {
                    return false;
                }

                pendingSealChallenges.Add(challengeKey, new PendingSealChallenge
                {
                    Purpose = purpose,
                    TargetOpCode = targetOpCode,
                    KeyId = keyId,
                    Nonce = (byte[])nonce.Clone(),
                    ExpiresAtUtcTicks = expiresAtUtcTicks
                });
                return true;
            }
        }

        internal bool TryConsumeSealChallenge(
            byte[] challengeId,
            out string purpose,
            out ushort targetOpCode,
            out string keyId,
            out byte[] nonce)
        {
            purpose = null;
            targetOpCode = 0;
            keyId = null;
            nonce = null;

            if (challengeId == null ||
                challengeId.Length != MstSecurityProtocol.ChallengeIdSize)
            {
                return false;
            }

            string challengeKey = Convert.ToBase64String(challengeId);

            lock (sealChallengesSync)
            {
                if (!pendingSealChallenges.TryGetValue(
                        challengeKey,
                        out PendingSealChallenge challenge))
                {
                    return false;
                }

                pendingSealChallenges.Remove(challengeKey);

                if (challenge.ExpiresAtUtcTicks < DateTime.UtcNow.Ticks)
                    return false;

                purpose = challenge.Purpose;
                targetOpCode = challenge.TargetOpCode;
                keyId = challenge.KeyId;
                nonce = (byte[])challenge.Nonce.Clone();
                return true;
            }
        }

        public bool TryConsumePermissionChallenge(byte[] challengeId, out string permissionKey,
            out byte[] nonce, out long expiresAtUtcTicks)
        {
            permissionKey = string.Empty;
            nonce = null;
            expiresAtUtcTicks = 0L;

            if (challengeId == null)
                return false;

            string challengeKey = Convert.ToBase64String(challengeId);

            lock (permissionsSync)
            {
                if (!pendingPermissionChallenges.TryGetValue(challengeKey,
                        out PendingPermissionChallenge challenge))
                {
                    return false;
                }

                pendingPermissionChallenges.Remove(challengeKey);

                if (challenge.ExpiresAtUtcTicks < DateTime.UtcNow.Ticks)
                    return false;

                permissionKey = challenge.PermissionKey;
                nonce = challenge.Nonce;
                expiresAtUtcTicks = challenge.ExpiresAtUtcTicks;
                return nonce != null && !string.IsNullOrWhiteSpace(permissionKey);
            }
        }

        public bool GrantPermission(string permissionKey, int permissionLevel)
        {
            if (string.IsNullOrWhiteSpace(permissionKey))
                return false;

            lock (permissionsSync)
            {
                int clampedLevel = MstPermissionLevels.Clamp(permissionLevel);

                if (grantedPermissions.TryGetValue(permissionKey, out int currentLevel))
                {
                    if (currentLevel != clampedLevel)
                        grantedPermissions[permissionKey] = clampedLevel;

                    return false;
                }

                grantedPermissions.Add(permissionKey, clampedLevel);
                return true;
            }
        }

        public bool HasPermission(string permissionKey)
        {
            if (string.IsNullOrWhiteSpace(permissionKey))
                return false;

            lock (permissionsSync)
                return grantedPermissions.ContainsKey(permissionKey);
        }

        public bool HasAccountPermission(int permissionLevel)
        {
            lock (permissionsSync)
                return MstPermissionLevels.HasAccess(accountPermissionLevel, permissionLevel);
        }

        public void SetAccountPermissionLevel(int permissionLevel)
        {
            lock (permissionsSync)
                accountPermissionLevel = MstPermissionLevels.Clamp(permissionLevel);
        }

        public void ResetAccountPermissionLevel()
        {
            lock (permissionsSync)
                accountPermissionLevel = MstPermissionLevels.Default;
        }

        private int GetConnectionPermissionLevelUnsafe()
        {
            int permissionLevel = MstPermissionLevels.Default;

            foreach (int grantedLevel in grantedPermissions.Values)
                permissionLevel = Math.Max(permissionLevel, grantedLevel);

            return permissionLevel;
        }

        private void RemoveExpiredPermissionChallengesUnsafe(long utcNowTicks)
        {
            if (pendingPermissionChallenges.Count == 0)
                return;

            var expiredKeys = new List<string>();

            foreach (KeyValuePair<string, PendingPermissionChallenge> pair in pendingPermissionChallenges)
            {
                if (pair.Value.ExpiresAtUtcTicks < utcNowTicks)
                    expiredKeys.Add(pair.Key);
            }

            foreach (string expiredKey in expiredKeys)
                pendingPermissionChallenges.Remove(expiredKey);
        }

        private void RemoveExpiredSealChallengesUnsafe(long utcNowTicks)
        {
            if (pendingSealChallenges.Count == 0)
                return;

            var expiredKeys = new List<string>();

            foreach (KeyValuePair<string, PendingSealChallenge> pair in pendingSealChallenges)
            {
                if (pair.Value.ExpiresAtUtcTicks < utcNowTicks)
                    expiredKeys.Add(pair.Key);
            }

            foreach (string expiredKey in expiredKeys)
                pendingSealChallenges.Remove(expiredKey);
        }
    }
}
