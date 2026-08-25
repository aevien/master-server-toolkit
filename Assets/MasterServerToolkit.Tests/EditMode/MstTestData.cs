using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;
using System.IO;

namespace MasterServerToolkit.Tests.EditMode
{
    internal static class MstTestData
    {
        private static readonly object securitySync = new object();
        private static readonly string securityKeyRingPath = Path.Combine(
            Path.GetTempPath(),
            "master-server-toolkit-tests",
            "mst-security.keys.json");

        internal sealed class SealContext
        {
            public IPeer Peer { get; set; }
            public string Purpose { get; set; }
            public ushort TargetOpCode { get; set; }
        }

        public static void InitializeServerSecurity(MstSecurity security)
        {
            lock (securitySync)
            {
                string directory = Path.GetDirectoryName(securityKeyRingPath);

                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                if (!security.InitializeServer(securityKeyRingPath, out string error))
                    throw new InvalidOperationException(error);
            }
        }

        public static SealContext CompleteSealChallenge(
            FakeClientSocket socket,
            MstSecurity security = null)
        {
            security ??= Mst.Security;
            InitializeServerSecurity(security);
            FakeClientSocket.SentRequest pending =
                socket.GetPendingRequest(MstOpCodes.SealChallengeRequest);
            MstSealChallengeRequestPacket request =
                SerializablePacket.FromBytes<MstSealChallengeRequestPacket>(
                    pending.Message.Data);
            var peer = new SealTestPeer();
            var extension = new SecurityInfoPeerExtension(peer);
            peer.AddExtension(extension);

            if (!security.TryCreateSealChallenge(
                    extension,
                    request,
                    out MstSealChallengePacket challenge,
                    out string error))
            {
                throw new InvalidOperationException(error);
            }

            pending.Respond(ResponseStatus.Success, challenge.ToBytes());
            return new SealContext
            {
                Peer = peer,
                Purpose = request.Purpose,
                TargetOpCode = request.TargetOpCode
            };
        }

        public static byte[] OpenSealedRequest(
            FakeClientSocket socket,
            SealContext context,
            MstSecurity security = null)
        {
            security ??= Mst.Security;
            FakeClientSocket.SentRequest request =
                socket.GetPendingRequest(context.TargetOpCode);

            if (!security.TryDecryptFromClient(
                    request.Message.Data,
                    context.Purpose,
                    context.TargetOpCode,
                    context.Peer,
                    out byte[] data))
            {
                throw new InvalidDataException("Could not open sealed test request");
            }

            return data;
        }

        public static byte[] CreateSealedPayload(
            MstSecurity security,
            IPeer peer,
            byte[] data,
            string purpose,
            ushort targetOpCode)
        {
            InitializeServerSecurity(security);
            SecurityInfoPeerExtension extension =
                peer.GetExtension<SecurityInfoPeerExtension>();

            if (extension == null)
            {
                extension = new SecurityInfoPeerExtension(peer);
                peer.AddExtension(extension);
            }

            var request = new MstSealChallengeRequestPacket
            {
                Purpose = purpose,
                TargetOpCode = targetOpCode
            };

            if (!security.TryCreateSealChallenge(
                    extension,
                    request,
                    out MstSealChallengePacket challenge,
                    out string error))
            {
                throw new InvalidOperationException(error);
            }

            byte[] dataKey =
                MstManagedCryptoBackend.RandomBytes(MstSecurityProtocol.DataKeySize);

            try
            {
                byte[] nonce =
                    MstManagedCryptoBackend.RandomBytes(MstSecurityProtocol.NonceSize);
                byte[] associatedData = MstSecurityProtocol.CreateAssociatedData(
                    purpose,
                    targetOpCode,
                    challenge.KeyId,
                    challenge.ChallengeId,
                    challenge.ChallengeNonce);
                byte[] cipherText = MstManagedCryptoBackend.EncryptAead(
                    dataKey,
                    nonce,
                    data,
                    associatedData,
                    out byte[] authenticationTag);

                return new MstSealedMessagePacket
                {
                    KeyId = challenge.KeyId,
                    ChallengeId = challenge.ChallengeId,
                    EncryptedDataKey =
                        MstManagedCryptoBackend.WrapKey(challenge.PublicKeySpki, dataKey),
                    Nonce = nonce,
                    CipherText = cipherText,
                    AuthenticationTag = authenticationTag
                }.ToBytes();
            }
            finally
            {
                Array.Clear(dataKey, 0, dataKey.Length);
            }
        }

        public static void CompletePermissionHandshake(FakeClientSocket socket, string permissionKey,
            int permissionLevel)
        {
            FakeClientSocket.SentRequest challengeRequest =
                socket.GetPendingRequest(MstOpCodes.ServerAccessChallengeRequest);
            ServerAccessChallengeRequestPacket requestPacket =
                SerializablePacket.FromBytes<ServerAccessChallengeRequestPacket>(challengeRequest.Message.Data);

            if (!string.Equals(requestPacket.PermissionKey, permissionKey, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Expected permission '{permissionKey}', but client requested '{requestPacket.PermissionKey}'");
            }

            var challenge = new ServerAccessChallengePacket
            {
                ChallengeId = CreateSequence(ServerAccessChallengePacket.ChallengeIdSize, 1),
                Nonce = CreateSequence(ServerAccessChallengePacket.NonceSize, 33),
                ExpiresAtUtcTicks = DateTime.UtcNow.AddMinutes(1).Ticks
            };

            challengeRequest.Respond(ResponseStatus.Success, challenge.ToBytes());
            socket.RespondNext(MstOpCodes.ServerAccessRequest, ResponseStatus.Success,
                MessageHelper.Create(MstOpCodes.ServerAccessRequest, permissionLevel).Data);
        }

        public static byte[] CreateAccountPacketBytes(
            string id = "account-1",
            string username = "survivor",
            bool isGuest = false,
            string token = "")
        {
            var account = new TestAccountInfoData
            {
                Id = id,
                Username = username,
                Email = $"{username}@example.test",
                Token = token,
                IsGuest = isGuest,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                LastLoginAt = new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc),
                ExtraProperties = new Dictionary<string, string>()
            };

            return new AccountInfoPacket(account).ToBytes();
        }

        private static byte[] CreateSequence(int length, int firstValue)
        {
            var bytes = new byte[length];

            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = (byte)(firstValue + i);

            return bytes;
        }

        private sealed class SealTestPeer : BasePeer
        {
            public override bool IsConnected => true;

            public override void SendMessage(
                IOutgoingMessage message,
                DeliveryMethod deliveryMethod)
            {
            }

            public override void Disconnect(string reason = "")
            {
            }

            public override void Disconnect(ushort code, string reason = "")
            {
            }
        }

        private sealed class TestAccountInfoData : IAccountInfoData
        {
            public string Id { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
            public string Email { get; set; }
            public string Token { get; set; }
            public DateTime LastLoginAt { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
            public bool IsAdmin { get; set; }
            public bool IsGuest { get; set; }
            public bool IsEmailConfirmed { get; set; }
            public Dictionary<string, string> ExtraProperties { get; set; }

            public event Action<IAccountInfoData> OnChangedEvent;

            public void MarkAsDirty()
            {
                OnChangedEvent?.Invoke(this);
            }

            public MstJson ToJson()
            {
                return MstJson.CreateObject();
            }
        }
    }
}
