using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;
using NUnit.Framework;
using System;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine.TestTools;

namespace MasterServerToolkit.Tests.EditMode
{
    [TestFixture]
    public class MstSecurityTests
    {
        private const string TestPermissionKey = MstPermissionKeys.RoomServer;
        private const string TestPermissionSecret = "test-permission-secret";
        private const string TestService = "room";
        private const string LegacyPasswordHash =
            "1000:AAECAwQFBgcICQoLDA0ODxAREhMUFRYX:MrEeqN8TgWVtj2QCI2aYlwg0I79t3QYX";

        [Test]
        public void PasswordHash_NewFormat_ValidatesAndDoesNotRequireRehash()
        {
            MstSecurity security = CreateSecurity(out _);

            string passwordHash = security.CreateHash("current-password");

            Assert.That(passwordHash, Does.StartWith("v2$"));
            Assert.That(security.ValidatePassword("current-password", passwordHash), Is.True);
            Assert.That(security.ValidatePassword("wrong-password", passwordHash), Is.False);
            Assert.That(security.NeedsPasswordRehash(passwordHash), Is.False);
        }

        [Test]
        public void PasswordHash_LegacyFormat_RemainsValidAndRequiresRehash()
        {
            MstSecurity security = CreateSecurity(out _);

            Assert.That(security.ValidatePassword("legacy-password", LegacyPasswordHash), Is.True);
            Assert.That(security.ValidatePassword("wrong-password", LegacyPasswordHash), Is.False);
            Assert.That(security.NeedsPasswordRehash(LegacyPasswordHash), Is.True);
        }

        [Test]
        public void PasswordHash_MalformedOrExcessiveWorkFactor_IsRejected()
        {
            MstSecurity security = CreateSecurity(out _);
            const string excessiveWorkHash =
                "v2$2147483647$AAECAwQFBgcICQoLDA0ODxAREhMUFRYX$" +
                "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

            Assert.That(security.ValidatePassword("password", "not-a-password-hash"), Is.False);
            Assert.That(security.ValidatePassword("password", excessiveWorkHash), Is.False);
        }

        [Test]
        public void Encrypt_AtPlaintextLimit_RoundTripsWithinCiphertextLimit()
        {
            MstSecurity security = CreateSecurity(out _);
            byte[] payload = new byte[MstNetworkLimits.MaxAuthenticationPlaintextByteCount];

            byte[] encrypted = security.Encrypt(payload, "test.payload");
            bool decrypted = security.TryDecrypt(
                encrypted,
                "test.payload",
                out byte[] decryptedPayload);

            Assert.That(
                encrypted.Length,
                Is.LessThanOrEqualTo(MstNetworkLimits.MaxAuthenticationCiphertextByteCount));
            Assert.That(decrypted, Is.True);
            Assert.That(decryptedPayload, Is.EqualTo(payload));
        }

        [Test]
        public void Encrypt_WhenPlaintextExceedsLimit_RejectsBeforeEncrypting()
        {
            MstSecurity security = CreateSecurity(out _);
            byte[] payload = new byte[MstNetworkLimits.MaxAuthenticationPlaintextByteCount + 1];

            Assert.Throws<InvalidDataException>(
                () => security.Encrypt(payload, "test.payload"));
        }

        [Test]
        public void TryDecrypt_WhenCipherTextExceedsLimit_RejectsWithoutAllocating()
        {
            MstSecurity security = CreateSecurity(out _);
            byte[] payload = new byte[MstNetworkLimits.MaxAuthenticationCiphertextByteCount + 1];

            Assert.That(
                security.TryDecrypt(payload, "test.payload", out _),
                Is.False);
        }

        [Test]
        public void TryDecrypt_WhenPurposeDoesNotMatch_RejectsPayload()
        {
            MstSecurity security = CreateSecurity(out _);
            byte[] encrypted = security.Encrypt(
                Encoding.UTF8.GetBytes("sensitive"),
                "test.expected");

            Assert.That(
                security.TryDecrypt(encrypted, "test.other", out _),
                Is.False);
        }

        [Test]
        public void TryDecrypt_WhenAuthenticationTagIsModified_RejectsPayload()
        {
            MstSecurity security = CreateSecurity(out _);
            byte[] encrypted = security.Encrypt(
                Encoding.UTF8.GetBytes("sensitive"),
                "test.tamper");
            encrypted[encrypted.Length - 1] ^= 0x01;

            Assert.That(
                security.TryDecrypt(encrypted, "test.tamper", out _),
                Is.False);
        }

        [Test]
        public void Encrypt_AfterSecurityReload_RetainsProtectedDataCompatibility()
        {
            MstSecurity firstSecurity = CreateSecurity(out _);
            byte[] payload = Encoding.UTF8.GetBytes("persistent protected data");
            byte[] encrypted = firstSecurity.Encrypt(payload, "test.persistence");
            MstSecurity reloadedSecurity = CreateSecurity(out _);

            firstSecurity.Dispose();

            Assert.That(
                reloadedSecurity.TryDecrypt(
                    encrypted,
                    "test.persistence",
                    out byte[] decrypted),
                Is.True);
            Assert.That(decrypted, Is.EqualTo(payload));
        }

        [Test]
        public void KeyRing_WhenCreatedConcurrently_AllCallersLoadSameKeys()
        {
            const int callerCount = 4;
            string directory = Path.Combine(
                Path.GetTempPath(),
                $"mst-security-concurrency-{Guid.NewGuid():N}");
            string filePath = Path.Combine(directory, "security.keys.json");
            var keyRings = new MstSecurityKeyRing[callerCount];
            var errors = new string[callerCount];
            var succeeded = new bool[callerCount];
            var dataKeyIds = new string[callerCount];
            var sealKeyIds = new string[callerCount];
            var workers = new Thread[callerCount];
            var ready = new CountdownEvent(callerCount);
            var start = new ManualResetEventSlim(false);
            var completed = new CountdownEvent(callerCount);
            bool workersCompleted = false;

            try
            {
                for (int i = 0; i < callerCount; i++)
                {
                    int callerIndex = i;
                    workers[i] = new Thread(() =>
                    {
                        ready.Signal();
                        start.Wait();

                        try
                        {
                            succeeded[callerIndex] = MstSecurityKeyRing.TryLoadOrCreate(
                                filePath,
                                out keyRings[callerIndex],
                                out errors[callerIndex]);

                            if (keyRings[callerIndex] != null)
                            {
                                dataKeyIds[callerIndex] =
                                    keyRings[callerIndex].ActiveDataKey.Id;
                                sealKeyIds[callerIndex] =
                                    keyRings[callerIndex].ActiveSealKey.Id;
                            }
                        }
                        catch (Exception exception)
                        {
                            errors[callerIndex] = exception.ToString();
                        }
                        finally
                        {
                            completed.Signal();
                        }
                    })
                    {
                        IsBackground = true
                    };
                    workers[i].Start();
                }

                Assert.That(
                    ready.Wait(TimeSpan.FromSeconds(10)),
                    Is.True,
                    "Concurrent key-ring workers did not start in time");
                start.Set();
                workersCompleted = completed.Wait(TimeSpan.FromSeconds(120));

                Assert.That(
                    workersCompleted,
                    Is.True,
                    "Concurrent key-ring workers did not finish in time");

                for (int i = 0; i < callerCount; i++)
                {
                    Assert.That(
                        succeeded[i],
                        Is.True,
                        $"Key-ring caller {i} failed: {errors[i]}");
                    Assert.That(dataKeyIds[i], Is.EqualTo(dataKeyIds[0]));
                    Assert.That(sealKeyIds[i], Is.EqualTo(sealKeyIds[0]));
                }

                Assert.That(File.Exists(filePath), Is.True);
            }
            finally
            {
                start.Set();

                if (!workersCompleted)
                    workersCompleted = completed.Wait(TimeSpan.FromSeconds(10));

                if (workersCompleted)
                {
                    foreach (Thread worker in workers)
                        worker?.Join();

                    foreach (MstSecurityKeyRing keyRing in keyRings)
                        keyRing?.Dispose();

                    ready.Dispose();
                    start.Dispose();
                    completed.Dispose();

                    if (Directory.Exists(directory))
                        Directory.Delete(directory, true);
                }
            }
        }

        [Test]
        public void GetPermissionCredential_WhenKeyIsEmpty_ReturnsEmptyString()
        {
            MstSecurity security = CreateSecurity(out _);

            Assert.That(security.GetPermissionCredential(string.Empty), Is.EqualTo(string.Empty));
        }

        [Test]
        public void GetPermissionCredential_WhenCustomKeyIsNotConfigured_UsesKeyAsDefault()
        {
            MstSecurity security = CreateSecurity(out _);

            Assert.That(security.GetPermissionCredential("custom_permission"),
                Is.EqualTo("custom_permission"));
        }

        [Test]
        public void AuthenticateConnection_WhenConnectionCredentialIsConfigured_UsesItForDefaultProof()
        {
            const string connectionCredential = "connection-default-secret";
            MstSecurity security = CreateSecurity(out FakeClientSocket socket);
            var result = new AuthenticationResult();
            ServerAccessChallengePacket challenge = CreateChallenge();
            socket.PermissionKey = MstPermissionKeys.Default;
            socket.PermissionCredential = connectionCredential;

            security.AuthenticateConnection(socket, result.Capture);
            socket.RespondNext(MstOpCodes.ServerAccessChallengeRequest,
                ResponseStatus.Success, challenge.ToBytes());

            FakeClientSocket.SentRequest accessRequest =
                socket.GetPendingRequest(MstOpCodes.ServerAccessRequest);
            ProvideServerAccessCheckPacket accessProof =
                SerializablePacket.FromBytes<ProvideServerAccessCheckPacket>(accessRequest.Message.Data);
            byte[] expectedProof = MstSecurity.CreateAccessProof(
                connectionCredential,
                TestService,
                MstPermissionKeys.Default,
                challenge.ChallengeId,
                challenge.Nonce,
                challenge.ExpiresAtUtcTicks);

            Assert.That(accessProof.Proof, Is.EqualTo(expectedProof));
        }

        [Test]
        public void AuthenticateConnection_WhenChallengeFails_CompletesOnceWithoutRequestingAccess()
        {
            MstSecurity security = CreateSecurity(out FakeClientSocket socket);
            var result = new AuthenticationResult();

            security.AuthenticateConnection(socket, result.Capture);
            socket.RespondNext(MstOpCodes.ServerAccessChallengeRequest, ResponseStatus.Error,
                CreateStructuredError(MstErrorCodes.PERMISSION_DENIED));

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(Mst.Errors.Localize("ui.error.response.forbidden.message")));
            Assert.That(socket.Requests.Count, Is.EqualTo(1));
            Assert.That(security.CurrentPermissionLevel, Is.EqualTo(MstPermissionLevels.Default));
        }

        [Test]
        public void AuthenticateConnection_WhenChallengeResponseIsNull_CompletesOnceWithoutRequestingAccess()
        {
            MstSecurity security = CreateSecurity(out FakeClientSocket socket);
            var result = new AuthenticationResult();

            security.AuthenticateConnection(socket, result.Capture);
            socket.RespondNextWithNull(MstOpCodes.ServerAccessChallengeRequest, ResponseStatus.Success);

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(Mst.Errors.Localize("ui.error.security.permission_challenge_invalid.message")));
            Assert.That(socket.Requests.Count, Is.EqualTo(1));
            Assert.That(security.CurrentPermissionLevel, Is.EqualTo(MstPermissionLevels.Default));
        }

        [Test]
        public void AuthenticateConnection_WhenChallengeIsMalformed_CompletesOnceWithoutRequestingAccess()
        {
            MstSecurity security = CreateSecurity(out FakeClientSocket socket);
            var result = new AuthenticationResult();

            security.AuthenticateConnection(socket, result.Capture);
            socket.RespondNext(MstOpCodes.ServerAccessChallengeRequest, ResponseStatus.Success,
                new byte[] { ServerAccessChallengePacket.CurrentVersion, 1, 2, 3 });

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(Mst.Errors.Localize("ui.error.security.permission_challenge_invalid.message")));
            Assert.That(socket.Requests.Count, Is.EqualTo(1));
            Assert.That(security.CurrentPermissionLevel, Is.EqualTo(MstPermissionLevels.Default));
        }

        [Test]
        public void AuthenticateConnection_WhenChallengeVersionIsUnsupported_CompletesOnceWithoutRequestingAccess()
        {
            MstSecurity security = CreateSecurity(out FakeClientSocket socket);
            var result = new AuthenticationResult();
            ServerAccessChallengePacket challenge = CreateChallenge(
                (byte)(ServerAccessChallengePacket.CurrentVersion + 1));

            security.AuthenticateConnection(socket, result.Capture);
            socket.RespondNext(MstOpCodes.ServerAccessChallengeRequest, ResponseStatus.Success, challenge.ToBytes());

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(Mst.Errors.Localize("ui.error.security.permission_protocol_unsupported.message")));
            Assert.That(socket.Requests.Count, Is.EqualTo(1));
            Assert.That(security.CurrentPermissionLevel, Is.EqualTo(MstPermissionLevels.Default));
        }

        [Test]
        public void AuthenticateConnection_WhenAccessFails_CompletesOnceWithoutChangingPermission()
        {
            MstSecurity security = CreateSecurity(out FakeClientSocket socket);
            var result = new AuthenticationResult();

            security.AuthenticateConnection(socket, result.Capture);
            RespondWithChallenge(socket);
            socket.RespondNext(MstOpCodes.ServerAccessRequest, ResponseStatus.Error,
                CreateStructuredError(MstErrorCodes.PERMISSION_DENIED));

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(Mst.Errors.Localize("ui.error.response.forbidden.message")));
            Assert.That(socket.Requests.Count, Is.EqualTo(2));
            Assert.That(security.CurrentPermissionLevel, Is.EqualTo(MstPermissionLevels.Default));
        }

        [Test]
        public void AuthenticateConnection_WhenAccessResponseIsNull_CompletesOnceWithoutChangingPermission()
        {
            MstSecurity security = CreateSecurity(out FakeClientSocket socket);
            var result = new AuthenticationResult();

            security.AuthenticateConnection(socket, result.Capture);
            RespondWithChallenge(socket);
            socket.RespondNextWithNull(MstOpCodes.ServerAccessRequest, ResponseStatus.Success);

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(Mst.Errors.Localize("ui.error.security.permission_response_invalid.message")));
            Assert.That(socket.Requests.Count, Is.EqualTo(2));
            Assert.That(security.CurrentPermissionLevel, Is.EqualTo(MstPermissionLevels.Default));
        }

        [Test]
        public void AuthenticateConnection_WhenPermissionResponseIsMalformed_CompletesOnceWithoutChangingPermission()
        {
            MstSecurity security = CreateSecurity(out FakeClientSocket socket);
            var result = new AuthenticationResult();
            int permissionChangedCount = 0;
            security.OnPermissionsLevelChangedEvent += () => permissionChangedCount++;

            security.AuthenticateConnection(socket, result.Capture);
            RespondWithChallenge(socket);
            socket.RespondNext(MstOpCodes.ServerAccessRequest, ResponseStatus.Success, new byte[] { 0, 0, 1 });

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(Mst.Errors.Localize("ui.error.security.permission_response_invalid.message")));
            Assert.That(permissionChangedCount, Is.Zero);
            Assert.That(security.CurrentPermissionLevel, Is.EqualTo(MstPermissionLevels.Default));
        }

        [Test]
        public void AuthenticateConnection_WhenHandshakeIsValid_SetsPermissionAndFiresEvent()
        {
            MstSecurity security = CreateSecurity(out FakeClientSocket socket);
            var result = new AuthenticationResult();
            int permissionChangedCount = 0;
            ServerAccessChallengePacket challenge = CreateChallenge();
            security.OnPermissionsLevelChangedEvent += () => permissionChangedCount++;

            security.AuthenticateConnection(socket, result.Capture);
            socket.RespondNext(MstOpCodes.ServerAccessChallengeRequest, ResponseStatus.Success, challenge.ToBytes());

            FakeClientSocket.SentRequest accessRequest = socket.GetPendingRequest(MstOpCodes.ServerAccessRequest);
            ProvideServerAccessCheckPacket accessProof =
                SerializablePacket.FromBytes<ProvideServerAccessCheckPacket>(accessRequest.Message.Data);
            ServerAccessChallengeRequestPacket challengeRequest =
                SerializablePacket.FromBytes<ServerAccessChallengeRequestPacket>(
                    socket.Requests[0].Message.Data);
            byte[] expectedProof = MstSecurity.CreateAccessProof(
                security.GetPermissionCredential(MstPermissionKeys.Default),
                TestService,
                MstPermissionKeys.Default,
                challenge.ChallengeId,
                challenge.Nonce,
                challenge.ExpiresAtUtcTicks);

            Assert.That(challengeRequest.PermissionKey, Is.EqualTo(MstPermissionKeys.Default));
            Assert.That(accessProof.Version, Is.EqualTo(ProvideServerAccessCheckPacket.CurrentVersion));
            Assert.That(accessProof.ChallengeId, Is.EqualTo(challenge.ChallengeId));
            Assert.That(accessProof.Proof, Is.EqualTo(expectedProof));

            accessRequest.Respond(ResponseStatus.Success, PermissionBytes(MstPermissionLevels.Default));

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result.Success, Is.True);
            Assert.That(result.Error, Is.EqualTo(string.Empty));
            Assert.That(security.HasPermission(MstPermissionKeys.Default), Is.True);
            Assert.That(security.CurrentPermissionLevel, Is.EqualTo(MstPermissionLevels.Default));
            Assert.That(permissionChangedCount, Is.EqualTo(1));
        }

        [TestCase(-1, MstPermissionLevels.Min)]
        [TestCase(1000, MstPermissionLevels.Max)]
        public void AuthenticateConnection_WhenPermissionIsOutsideRange_ClampsPermission(
            int serverPermissionLevel,
            int expectedPermissionLevel)
        {
            MstSecurity security = CreateSecurity(out FakeClientSocket socket);
            var result = new AuthenticationResult();

            security.AuthenticateConnection(socket, result.Capture);
            RespondWithChallenge(socket);
            socket.RespondNext(MstOpCodes.ServerAccessRequest, ResponseStatus.Success,
                PermissionBytes(serverPermissionLevel));

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result.Success, Is.True);
            Assert.That(security.CurrentPermissionLevel, Is.EqualTo(expectedPermissionLevel));
        }

        [Test]
        public void Disconnect_AfterSuccessfulHandshake_ResetsPermissionAndFiresEvent()
        {
            MstSecurity security = CreateSecurity(out FakeClientSocket socket);
            var result = new AuthenticationResult();
            int permissionChangedCount = 0;
            security.OnPermissionsLevelChangedEvent += () => permissionChangedCount++;

            security.AuthenticateConnection(socket, result.Capture);
            RespondWithChallenge(socket);
            socket.RespondNext(MstOpCodes.ServerAccessRequest, ResponseStatus.Success,
                PermissionBytes(MstPermissionLevels.RoomServer));

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(security.CurrentPermissionLevel, Is.EqualTo(MstPermissionLevels.RoomServer));
            Assert.That(permissionChangedCount, Is.EqualTo(1));

            socket.Close();

            Assert.That(security.CurrentPermissionLevel, Is.EqualTo(MstPermissionLevels.Default));
            Assert.That(permissionChangedCount, Is.EqualTo(2));
        }

        [Test]
        public void AuthenticateConnection_WhenResponsesCompleteSynchronously_InvokesCallbackExactlyOnce()
        {
            MstSecurity security = CreateSecurity(out FakeClientSocket socket);
            var result = new AuthenticationResult();
            ServerAccessChallengePacket challenge = CreateChallenge();

            socket.OnRequestSent = request =>
            {
                if (request.Message.OpCode == MstOpCodes.ServerAccessChallengeRequest)
                {
                    request.Respond(ResponseStatus.Success, challenge.ToBytes());
                    return;
                }

                if (request.Message.OpCode == MstOpCodes.ServerAccessRequest)
                    request.Respond(ResponseStatus.Success, PermissionBytes(MstPermissionLevels.TrustedUserMin));
            };

            security.AuthenticateConnection(socket, result.Capture);

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result.Success, Is.True);
            Assert.That(socket.Requests.Count, Is.EqualTo(2));
            Assert.That(security.CurrentPermissionLevel, Is.EqualTo(MstPermissionLevels.TrustedUserMin));
        }

        [Test]
        public void AuthenticateConnection_WhenDefaultHandshakeIsAlreadyPending_CoalescesCallbacks()
        {
            MstSecurity security = CreateSecurity(out FakeClientSocket socket);
            var first = new AuthenticationResult();
            var second = new AuthenticationResult();

            security.AuthenticateConnection(socket, first.Capture);
            security.AuthenticateConnection(socket, second.Capture);

            Assert.That(socket.Requests.Count, Is.EqualTo(1));
            RespondWithChallenge(socket);
            socket.RespondNext(MstOpCodes.ServerAccessRequest, ResponseStatus.Success,
                PermissionBytes(MstPermissionLevels.Default));

            Assert.That(first.Count, Is.EqualTo(1));
            Assert.That(first.Success, Is.True);
            Assert.That(second.Count, Is.EqualTo(1));
            Assert.That(second.Success, Is.True);
            Assert.That(socket.Requests.Count, Is.EqualTo(2));
        }

        [Test]
        public void RequestPermission_AfterDefaultAuthentication_StoresExactPermission()
        {
            MstSecurity security = CreateSecurity(out FakeClientSocket socket);
            AuthenticateDefault(security, socket);
            var result = new AuthenticationResult();
            ServerAccessChallengePacket challenge = CreateChallenge();

            security.RequestPermission(TestPermissionKey, TestPermissionSecret, result.Capture, socket);

            FakeClientSocket.SentRequest challengeRequest =
                socket.GetPendingRequest(MstOpCodes.ServerAccessChallengeRequest);
            ServerAccessChallengeRequestPacket requestPacket =
                SerializablePacket.FromBytes<ServerAccessChallengeRequestPacket>(challengeRequest.Message.Data);
            Assert.That(requestPacket.PermissionKey, Is.EqualTo(TestPermissionKey));

            challengeRequest.Respond(ResponseStatus.Success, challenge.ToBytes());

            FakeClientSocket.SentRequest proofRequest =
                socket.GetPendingRequest(MstOpCodes.ServerAccessRequest);
            ProvideServerAccessCheckPacket proof =
                SerializablePacket.FromBytes<ProvideServerAccessCheckPacket>(proofRequest.Message.Data);
            Assert.That(proof.Proof, Is.EqualTo(MstSecurity.CreateAccessProof(
                TestPermissionSecret,
                TestService,
                TestPermissionKey,
                challenge.ChallengeId,
                challenge.Nonce,
                challenge.ExpiresAtUtcTicks)));

            proofRequest.Respond(ResponseStatus.Success, PermissionBytes(MstPermissionLevels.RoomServer));

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result.Success, Is.True);
            Assert.That(security.HasPermission(MstPermissionKeys.Default), Is.True);
            Assert.That(security.HasPermission(TestPermissionKey), Is.True);
            Assert.That(security.CurrentPermissionLevel, Is.EqualTo(MstPermissionLevels.RoomServer));
        }

        [Test]
        public void RequestPermission_WhenConnectionCredentialMatches_UsesItForProof()
        {
            MstSecurity security = CreateSecurity(out FakeClientSocket socket);
            AuthenticateDefault(security, socket);
            var result = new AuthenticationResult();
            ServerAccessChallengePacket challenge = CreateChallenge();
            socket.PermissionKey = TestPermissionKey;
            socket.PermissionCredential = TestPermissionSecret;

            security.RequestPermission(TestPermissionKey, result.Capture, socket);
            socket.RespondNext(MstOpCodes.ServerAccessChallengeRequest,
                ResponseStatus.Success, challenge.ToBytes());

            FakeClientSocket.SentRequest proofRequest =
                socket.GetPendingRequest(MstOpCodes.ServerAccessRequest);
            ProvideServerAccessCheckPacket proof =
                SerializablePacket.FromBytes<ProvideServerAccessCheckPacket>(proofRequest.Message.Data);
            byte[] expectedProof = MstSecurity.CreateAccessProof(
                TestPermissionSecret,
                TestService,
                TestPermissionKey,
                challenge.ChallengeId,
                challenge.Nonce,
                challenge.ExpiresAtUtcTicks);

            Assert.That(proof.Proof, Is.EqualTo(expectedProof));
        }

        [Test]
        public void RequestPermission_WhenSameKeyIsPending_CoalescesHandshakeAndCompletesBothCallbacks()
        {
            MstSecurity security = CreateSecurity(out FakeClientSocket socket);
            AuthenticateDefault(security, socket);
            var first = new AuthenticationResult();
            var second = new AuthenticationResult();

            security.RequestPermission(TestPermissionKey, TestPermissionSecret, first.Capture, socket);
            security.RequestPermission(TestPermissionKey, TestPermissionSecret, second.Capture, socket);

            Assert.That(socket.Requests.Count, Is.EqualTo(3));
            RespondWithChallenge(socket);
            socket.RespondNext(MstOpCodes.ServerAccessRequest, ResponseStatus.Success,
                PermissionBytes(MstPermissionLevels.RoomServer));

            Assert.That(first.Count, Is.EqualTo(1));
            Assert.That(first.Success, Is.True);
            Assert.That(second.Count, Is.EqualTo(1));
            Assert.That(second.Success, Is.True);
            Assert.That(socket.Requests.Count, Is.EqualTo(4));
        }

        [Test]
        public void RequestPermission_WhenAdditionalPermissionFails_KeepsDefaultPermission()
        {
            MstSecurity security = CreateSecurity(out FakeClientSocket socket);
            AuthenticateDefault(security, socket);
            var result = new AuthenticationResult();

            security.RequestPermission(TestPermissionKey, TestPermissionSecret, result.Capture, socket);
            socket.RespondNext(MstOpCodes.ServerAccessChallengeRequest, ResponseStatus.Unauthorized,
                CreateStructuredError(MstErrorCodes.PERMISSION_DENIED));

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(Mst.Errors.Localize("ui.error.response.forbidden.message")));
            Assert.That(security.HasPermission(MstPermissionKeys.Default), Is.True);
            Assert.That(security.HasPermission(TestPermissionKey), Is.False);
            Assert.That(security.CurrentPermissionLevel, Is.EqualTo(MstPermissionLevels.Default));
        }

        [Test]
        public void RequestPermission_WhenDifferentKeysAreGranted_RetainsBothPermissions()
        {
            MstSecurity security = CreateSecurity(out FakeClientSocket socket);
            AuthenticateDefault(security, socket);

            GrantPermission(security, socket, MstPermissionKeys.RoomServer,
                TestPermissionSecret, MstPermissionLevels.RoomServer);
            GrantPermission(security, socket, MstPermissionKeys.Spawner,
                "test-spawner-secret", MstPermissionLevels.Spawner);

            Assert.That(security.HasPermission(MstPermissionKeys.Default), Is.True);
            Assert.That(security.HasPermission(MstPermissionKeys.RoomServer), Is.True);
            Assert.That(security.HasPermission(MstPermissionKeys.Spawner), Is.True);
        }

        [Test]
        public void AuthenticateConnection_WhileAuthenticating_ExposesDefaultPermissionToEventSubscribers()
        {
            MstSecurity security = CreateSecurity(out FakeClientSocket socket);
            socket.SetStatusForTest(ConnectionStatus.Authenticating);
            bool permissionVisibleFromEvent = false;
            security.OnPermissionsLevelChangedEvent += () =>
                permissionVisibleFromEvent = security.HasPermission(socket, MstPermissionKeys.Default);

            AuthenticateDefault(security, socket);

            Assert.That(permissionVisibleFromEvent, Is.True);
            Assert.That(security.HasPermission(socket, MstPermissionKeys.Default), Is.True);
        }

        [Test]
        public void RequestPermission_WhenPermissionEventSubscriberThrows_StillCompletesRequest()
        {
            MstSecurity security = CreateSecurity(out FakeClientSocket socket);
            AuthenticateDefault(security, socket);
            var result = new AuthenticationResult();
            int successfulSubscriberCalls = 0;
            bool subscriberFailureLogged = false;
            security.OnPermissionsLevelChangedEvent += () => throw new InvalidOperationException("Expected test failure");
            security.OnPermissionsLevelChangedEvent += () => successfulSubscriberCalls++;

            LogHandler captureAppender = (_, level, channel, message) =>
            {
                if (level == LogLevel.Error &&
                    channel == LogChannels.Security &&
                    message?.ToString().Contains("Permission level change subscriber failed") == true)
                {
                    subscriberFailureLogged = true;
                }
            };

            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogManager.AddAppender(captureAppender);
            LogAssert.ignoreFailingMessages = true;

            try
            {
                GrantPermission(security, socket, TestPermissionKey,
                    TestPermissionSecret, MstPermissionLevels.RoomServer, result);
            }
            finally
            {
                LogManager.RemoveAppender(captureAppender);
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }

            Assert.That(successfulSubscriberCalls, Is.EqualTo(1));
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result.Success, Is.True);
            Assert.That(subscriberFailureLogged, Is.True);
        }

        [Test]
        public void RequestPermission_WhenKeyIsAlreadyGranted_DoesNotSendAnotherHandshake()
        {
            MstSecurity security = CreateSecurity(out FakeClientSocket socket);
            AuthenticateDefault(security, socket);
            GrantPermission(security, socket, TestPermissionKey,
                TestPermissionSecret, MstPermissionLevels.RoomServer);
            int requestCount = socket.Requests.Count;
            var result = new AuthenticationResult();

            security.RequestPermission(TestPermissionKey, "different-secret", result.Capture, socket);

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result.Success, Is.True);
            Assert.That(socket.Requests.Count, Is.EqualTo(requestCount));
        }

        [Test]
        public void Reconnect_ClearsAllPermissionsAndAllowsFreshDefaultAuthentication()
        {
            MstSecurity security = CreateSecurity(out FakeClientSocket socket);
            AuthenticateDefault(security, socket);
            GrantPermission(security, socket, TestPermissionKey,
                TestPermissionSecret, MstPermissionLevels.RoomServer);

            socket.Close();
            socket.Connect("127.0.0.1", 5000);

            Assert.That(security.HasPermission(socket, MstPermissionKeys.Default), Is.False);
            Assert.That(security.HasPermission(socket, TestPermissionKey), Is.False);

            AuthenticateDefault(security, socket);

            Assert.That(security.HasPermission(socket, MstPermissionKeys.Default), Is.True);
            Assert.That(security.HasPermission(socket, TestPermissionKey), Is.False);
        }

        [Test]
        public void EncryptForMaster_WhenChallengeIsValid_RoundTripsAndRejectsReplay()
        {
            MstSecurity security = CreateSecurity(out FakeClientSocket socket);
            byte[] payload = Encoding.UTF8.GetBytes("sealed credentials");
            byte[] encrypted = null;
            string error = null;
            int callbackCount = 0;

            security.EncryptForMaster(
                payload,
                "test.master",
                MstOpCodes.SignIn,
                (result, resultError) =>
                {
                    callbackCount++;
                    encrypted = result;
                    error = resultError;
                },
                socket);

            MstTestData.SealContext context =
                MstTestData.CompleteSealChallenge(socket, security);

            Assert.That(callbackCount, Is.EqualTo(1));
            Assert.That(error, Is.Null.Or.Empty);
            Assert.That(encrypted, Is.Not.Null.And.Not.Empty);
            Assert.That(
                security.TryDecryptFromClient(
                    encrypted,
                    context.Purpose,
                    context.TargetOpCode,
                    context.Peer,
                    out byte[] decrypted),
                Is.True);
            Assert.That(decrypted, Is.EqualTo(payload));
            Assert.That(
                security.TryDecryptFromClient(
                    encrypted,
                    context.Purpose,
                    context.TargetOpCode,
                    context.Peer,
                    out _),
                Is.False);
        }

        [Test]
        public void EncryptForMaster_WhenChallengeRequestFails_CompletesOnceWithError()
        {
            MstSecurity security = CreateSecurity(out FakeClientSocket socket);
            int callbackCount = 0;
            byte[] encrypted = null;
            string error = null;

            security.EncryptForMaster(
                Encoding.UTF8.GetBytes("sealed credentials"),
                "test.master",
                MstOpCodes.SignIn,
                (result, resultError) =>
                {
                    callbackCount++;
                    encrypted = result;
                    error = resultError;
                },
                socket);
            socket.RespondNext(
                MstOpCodes.SealChallengeRequest,
                ResponseStatus.Error,
                CreateStructuredError(MstErrorCodes.ENCRYPTION_CHALLENGE_UNAVAILABLE));

            Assert.That(callbackCount, Is.EqualTo(1));
            Assert.That(encrypted, Is.Null);
            Assert.That(error, Is.EqualTo(Mst.Errors.Localize("ui.error.security.encryption_challenge_unavailable.message")));
        }

        private static MstSecurity CreateSecurity(out FakeClientSocket socket)
        {
            socket = new FakeClientSocket { Service = TestService };
            var security = new MstSecurity(socket);
            MstTestData.InitializeServerSecurity(security);
            return security;
        }

        private static byte[] CreateStructuredError(string code)
        {
            var properties = new MstProperties();
            properties.Set(MstErrorPropertyKeys.CODE, code);
            return properties.ToBytes();
        }

        private static void RespondWithChallenge(FakeClientSocket socket)
        {
            socket.RespondNext(MstOpCodes.ServerAccessChallengeRequest, ResponseStatus.Success,
                CreateChallenge().ToBytes());
        }

        private static void AuthenticateDefault(MstSecurity security, FakeClientSocket socket)
        {
            var result = new AuthenticationResult();
            security.AuthenticateConnection(socket, result.Capture);
            RespondWithChallenge(socket);
            socket.RespondNext(MstOpCodes.ServerAccessRequest, ResponseStatus.Success,
                PermissionBytes(MstPermissionLevels.Default));

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result.Success, Is.True);
        }

        private static void GrantPermission(MstSecurity security, FakeClientSocket socket,
            string permissionKey, string secret, int permissionLevel, AuthenticationResult result = null)
        {
            result ??= new AuthenticationResult();
            security.RequestPermission(permissionKey, secret, result.Capture, socket);
            RespondWithChallenge(socket);
            socket.RespondNext(MstOpCodes.ServerAccessRequest, ResponseStatus.Success,
                PermissionBytes(permissionLevel));

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result.Success, Is.True);
        }

        private static ServerAccessChallengePacket CreateChallenge(
            byte version = ServerAccessChallengePacket.CurrentVersion)
        {
            return new ServerAccessChallengePacket
            {
                Version = version,
                ChallengeId = CreateBytes(ServerAccessChallengePacket.ChallengeIdSize, 1),
                Nonce = CreateBytes(ServerAccessChallengePacket.NonceSize, 17),
                ExpiresAtUtcTicks = DateTime.UtcNow.AddMinutes(1).Ticks
            };
        }

        private static byte[] CreateBytes(int length, int start)
        {
            var bytes = new byte[length];

            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = (byte)(start + i);

            return bytes;
        }

        private static byte[] PermissionBytes(int permissionLevel)
        {
            return MessageHelper.Create(MstOpCodes.ServerAccessRequest, permissionLevel).Data;
        }

        private sealed class AuthenticationResult
        {
            public int Count { get; private set; }
            public bool Success { get; private set; }
            public string Error { get; private set; }

            public void Capture(bool success, string error)
            {
                Count++;
                Success = success;
                Error = error;
            }
        }
    }
}
