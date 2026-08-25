using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MasterServerToolkit.Tests.EditMode
{
    [TestFixture]
    public class ServerPermissionSecurityTests
    {
        private GameObject testObject;
        private TestServerBehaviour server;
        private TestPeer peer;

        [SetUp]
        public void SetUp()
        {
            testObject = new GameObject(nameof(ServerPermissionSecurityTests));
            server = testObject.AddComponent<TestServerBehaviour>();
            server.InitializeForTest();

            peer = new TestPeer();
            SecurityInfoPeerExtension security = peer.AddExtension(new SecurityInfoPeerExtension(peer));
            security.GrantPermission(MstPermissionKeys.Default, MstPermissionLevels.Default);
        }

        [TearDown]
        public void TearDown()
        {
            peer?.Dispose();
            UnityEngine.Object.DestroyImmediate(testObject);
        }

        [Test]
        public async Task AdminPermissionChallenge_FromAuthenticatedClient_IsRejected()
        {
            var requestPacket = new ServerAccessChallengeRequestPacket
            {
                PermissionKey = MstPermissionKeys.Admin
            };
            var request = new IncomingMessage(
                MstOpCodes.ServerAccessChallengeRequest,
                0,
                requestPacket.ToBytes(),
                DeliveryMethod.Reliable,
                peer)
            {
                AckResponseId = 17
            };

            await server.InvokePermissionChallenge(request);

            Assert.That(peer.SentMessages.Count, Is.EqualTo(1));
            IOutgoingMessage response = peer.SentMessages[0];
            Assert.That(response.Status, Is.EqualTo(ResponseStatus.Unauthorized));
            Assert.That(response.AckResponseId, Is.EqualTo(17));
            MstProperties error = MstProperties.FromBytes(response.Data);
            Assert.That(error.AsString(MstErrorPropertyKeys.CODE),
                Is.EqualTo(MstErrorCodes.PERMISSION_DENIED));

            SecurityInfoPeerExtension security = peer.GetExtension<SecurityInfoPeerExtension>();
            Assert.That(security.HasPermission(MstPermissionKeys.Admin), Is.False);
            Assert.That(security.AccountPermissionLevel, Is.EqualTo(MstPermissionLevels.Default));
            Assert.That(peer.IsConnected, Is.True);
        }

        private sealed class TestServerBehaviour : ServerBehaviour
        {
            public void InitializeForTest()
            {
                base.Awake();
            }

            public Task InvokePermissionChallenge(IIncomingMessage message)
            {
                return ServerAccessChallengeRequestHandler(message, CancellationToken.None);
            }
        }

        private sealed class TestPeer : BasePeer
        {
            private bool isConnected = true;

            public List<IOutgoingMessage> SentMessages { get; } = new List<IOutgoingMessage>();
            public override bool IsConnected => isConnected;

            public override void SendMessage(IOutgoingMessage message, DeliveryMethod deliveryMethod)
            {
                SentMessages.Add(message);
            }

            public override void Disconnect(string reason = "")
            {
                isConnected = false;
            }

            public override void Disconnect(ushort code, string reason = "")
            {
                CloseCode = code;
                isConnected = false;
            }

            protected override void Dispose(bool disposing)
            {
                isConnected = false;
                base.Dispose(disposing);
            }
        }
    }
}
