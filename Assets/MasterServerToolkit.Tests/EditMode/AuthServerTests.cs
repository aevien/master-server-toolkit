using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using NUnit.Framework;
using System.Collections.Generic;
using System.Text;

namespace MasterServerToolkit.Tests.EditMode
{
    [TestFixture]
    public class AuthServerTests
    {
        private const string Username = "test-player";
        private const int PeerId = 321;

        public enum LookupType
        {
            Username,
            Peer
        }

        [TestCase(LookupType.Username)]
        [TestCase(LookupType.Peer)]
        public void GetAccountInfo_WhenDisconnected_CompletesOnceWithoutSending(LookupType lookupType)
        {
            var socket = new FakeClientSocket(false);
            AuthServer auth = CreateAuthServer(socket);
            var callback = new CallbackResult();

            SendRequest(auth, socket, lookupType, callback);

            Assert.That(callback.Count, Is.EqualTo(1));
            Assert.That(callback.AccountInfo, Is.Null);
            Assert.That(callback.Error,
                Is.EqualTo(Mst.Errors.Localize("ui.status.connection.notConnected")));
            Assert.That(socket.Requests, Is.Empty);
        }

        [TestCase(LookupType.Username)]
        [TestCase(LookupType.Peer)]
        public void GetAccountInfo_WhenServerReturnsStructuredError_ReturnsLocalizedMessageOnce(
            LookupType lookupType)
        {
            var socket = new FakeClientSocket();
            AuthServer auth = CreateAuthServer(socket);
            var callback = new CallbackResult();
            var properties = new MstProperties();
            properties.Set(MstErrorPropertyKeys.CODE, MstErrorCodes.AUTH_PERMISSION_DENIED);

            SendRequest(auth, socket, lookupType, callback);
            FakeClientSocket.SentRequest request = AssertRequest(socket, lookupType);
            request.Respond(ResponseStatus.Forbidden, properties.ToBytes());

            Assert.That(callback.Count, Is.EqualTo(1));
            Assert.That(callback.AccountInfo, Is.Null);
            Assert.That(callback.Error,
                Is.EqualTo(Mst.Errors.Localize("ui.error.auth.permission_denied.message")));
        }

        [TestCase(LookupType.Username)]
        [TestCase(LookupType.Peer)]
        public void GetAccountInfo_WhenServerErrorHasNullResponse_ReturnsFallbackOnce(LookupType lookupType)
        {
            var socket = new FakeClientSocket();
            AuthServer auth = CreateAuthServer(socket);
            var callback = new CallbackResult();

            SendRequest(auth, socket, lookupType, callback);
            FakeClientSocket.SentRequest request = AssertRequest(socket, lookupType);
            request.RespondWithNull(ResponseStatus.Error);

            Assert.That(callback.Count, Is.EqualTo(1));
            Assert.That(callback.AccountInfo, Is.Null);
            Assert.That(callback.Error,
                Is.EqualTo(Mst.Errors.Localize("ui.error.response.internal.message")));
        }

        [TestCase(LookupType.Username)]
        [TestCase(LookupType.Peer)]
        public void GetAccountInfo_WhenSuccessPacketIsMalformed_ReturnsFallbackOnce(LookupType lookupType)
        {
            var socket = new FakeClientSocket();
            AuthServer auth = CreateAuthServer(socket);
            var callback = new CallbackResult();

            SendRequest(auth, socket, lookupType, callback);
            FakeClientSocket.SentRequest request = AssertRequest(socket, lookupType);
            request.Respond(ResponseStatus.Success, new byte[] { 1, 2, 3 });

            Assert.That(callback.Count, Is.EqualTo(1));
            Assert.That(callback.AccountInfo, Is.Null);
            Assert.That(callback.Error,
                Is.EqualTo(Mst.Errors.Localize("ui.error.response.internal.message")));
        }

        [TestCase(LookupType.Username)]
        [TestCase(LookupType.Peer)]
        public void GetAccountInfo_WhenSuccessPacketIsValid_ReturnsPacketOnce(LookupType lookupType)
        {
            var socket = new FakeClientSocket();
            AuthServer auth = CreateAuthServer(socket);
            var callback = new CallbackResult();
            RoomUserAccountInfoPacket expected = CreateAccountInfoPacket();

            SendRequest(auth, socket, lookupType, callback);
            FakeClientSocket.SentRequest request = AssertRequest(socket, lookupType);
            request.Respond(ResponseStatus.Success, expected.ToBytes());

            Assert.That(callback.Count, Is.EqualTo(1));
            Assert.That(callback.Error, Is.Null);
            Assert.That(callback.AccountInfo, Is.Not.Null);
            Assert.That(callback.AccountInfo.PeerId, Is.EqualTo(expected.PeerId));
            Assert.That(callback.AccountInfo.Username, Is.EqualTo(expected.Username));
            Assert.That(callback.AccountInfo.UserId, Is.EqualTo(expected.UserId));
            Assert.That(callback.AccountInfo.IsGuest, Is.EqualTo(expected.IsGuest));
            Assert.That(callback.AccountInfo.IsAdmin, Is.EqualTo(expected.IsAdmin));
            Assert.That(callback.AccountInfo.ExtraProperties, Is.Not.Null);
            Assert.That(callback.AccountInfo.ExtraProperties.Count, Is.EqualTo(2));
            Assert.That(callback.AccountInfo.ExtraProperties["region"], Is.EqualTo("test-region"));
            Assert.That(callback.AccountInfo.ExtraProperties["character"], Is.EqualTo("survivor-01"));
        }

        private static void SendRequest(AuthServer auth, FakeClientSocket socket, LookupType lookupType,
            CallbackResult callback)
        {
            if (lookupType == LookupType.Username)
            {
                auth.GetAccountInfoByUsername(Username, callback.Capture, socket);
                return;
            }

            auth.GetAccountInfoByPeer(PeerId, callback.Capture, socket);
        }

        private static AuthServer CreateAuthServer(FakeClientSocket socket)
        {
            return new AuthServer(socket);
        }

        private static FakeClientSocket.SentRequest AssertRequest(FakeClientSocket socket, LookupType lookupType)
        {
            Assert.That(socket.Requests.Count, Is.EqualTo(1));

            FakeClientSocket.SentRequest request = socket.Requests[0];
            ushort expectedOpCode = lookupType == LookupType.Username
                ? MstOpCodes.GetAccountInfoByUsername
                : MstOpCodes.GetAccountInfoByPeer;
            byte[] expectedPayload = lookupType == LookupType.Username
                ? Encoding.UTF8.GetBytes(Username)
                : EndianBitConverter.Big.GetBytes(PeerId);

            Assert.That(request.Message.OpCode, Is.EqualTo(expectedOpCode));
            Assert.That(request.Message.Data, Is.EqualTo(expectedPayload));
            return request;
        }

        private static RoomUserAccountInfoPacket CreateAccountInfoPacket()
        {
            return new RoomUserAccountInfoPacket
            {
                PeerId = PeerId,
                Username = Username,
                UserId = "account-123",
                IsGuest = false,
                IsAdmin = true,
                ExtraProperties = new Dictionary<string, string>
                {
                    { "region", "test-region" },
                    { "character", "survivor-01" }
                }
            };
        }

        private sealed class CallbackResult
        {
            public int Count { get; private set; }
            public RoomUserAccountInfoPacket AccountInfo { get; private set; }
            public string Error { get; private set; }

            public void Capture(RoomUserAccountInfoPacket accountInfo, string error)
            {
                Count++;
                AccountInfo = accountInfo;
                Error = error;
            }
        }
    }
}
