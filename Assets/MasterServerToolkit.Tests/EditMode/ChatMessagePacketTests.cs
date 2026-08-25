using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.Tests.EditMode
{
    [TestFixture]
    public class ChatMessagePacketTests
    {
        private GameObject testObject;
        private TestChatModule chatModule;

        [SetUp]
        public void SetUp()
        {
            testObject = new GameObject(nameof(ChatMessagePacketTests));
            chatModule = testObject.AddComponent<TestChatModule>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(testObject);
        }

        [Test]
        public void ChatMessagePacket_RoundTrip_PreservesRoutingAndPresentation()
        {
            var source = new ChatMessagePacket
            {
                MessageType = ChatMessageType.Private,
                Sender = "player-a",
                SenderDisplayName = "Alfonso",
                Receiver = "player-b",
                ReceiverDisplayName = "Morgan",
                SenderAvatar = "https://avatars.example.com/player-a.png",
                ReceiverAvatar = "https://avatars.example.com/player-b.png",
                Message = "hello"
            };

            ChatMessagePacket restored = SerializablePacket.FromBytes<ChatMessagePacket>(source.ToBytes());

            Assert.That(restored.MessageType, Is.EqualTo(ChatMessageType.Private));
            Assert.That(restored.Sender, Is.EqualTo("player-a"));
            Assert.That(restored.SenderDisplayName, Is.EqualTo("Alfonso"));
            Assert.That(restored.Receiver, Is.EqualTo("player-b"));
            Assert.That(restored.ReceiverDisplayName, Is.EqualTo("Morgan"));
            Assert.That(restored.SenderAvatar, Is.EqualTo("https://avatars.example.com/player-a.png"));
            Assert.That(restored.ReceiverAvatar, Is.EqualTo("https://avatars.example.com/player-b.png"));
            Assert.That(restored.Message, Is.EqualTo("hello"));
        }

        [Test]
        public void ServerChatMessagePacket_RoundTrip_PreservesPresentationAndRecipients()
        {
            var source = new ServerChatMessagePacket
            {
                MessageType = ChatMessageType.Users,
                Sender = "player-a",
                SenderDisplayName = "Alfonso",
                Receiver = "local",
                ReceiverDisplayName = "Morgan",
                SenderAvatar = "https://avatars.example.com/player-a.png",
                ReceiverAvatar = "https://avatars.example.com/player-b.png",
                Message = "hello",
                Recipients = new List<string> { "player-a", "player-b" }
            };

            ServerChatMessagePacket restored = SerializablePacket.FromBytes<ServerChatMessagePacket>(source.ToBytes());

            Assert.That(restored.MessageType, Is.EqualTo(ChatMessageType.Users));
            Assert.That(restored.Sender, Is.EqualTo("player-a"));
            Assert.That(restored.SenderDisplayName, Is.EqualTo("Alfonso"));
            Assert.That(restored.Receiver, Is.EqualTo("local"));
            Assert.That(restored.ReceiverDisplayName, Is.EqualTo("Morgan"));
            Assert.That(restored.SenderAvatar, Is.EqualTo("https://avatars.example.com/player-a.png"));
            Assert.That(restored.ReceiverAvatar, Is.EqualTo("https://avatars.example.com/player-b.png"));
            Assert.That(restored.Message, Is.EqualTo("hello"));
            Assert.That(restored.Recipients, Is.EqualTo(new[] { "player-a", "player-b" }));

            ChatMessagePacket clientPacket = restored.ToChatMessagePacket();
            Assert.That(clientPacket.SenderDisplayName, Is.EqualTo("Alfonso"));
            Assert.That(clientPacket.ReceiverDisplayName, Is.EqualTo("Morgan"));
            Assert.That(clientPacket.SenderAvatar, Is.EqualTo("https://avatars.example.com/player-a.png"));
            Assert.That(clientPacket.ReceiverAvatar, Is.EqualTo("https://avatars.example.com/player-b.png"));
        }

        [Test]
        public void TrustedPresentation_NormalizesAvatarUrls()
        {
            var packet = new ServerChatMessagePacket
            {
                SenderDisplayName = "  Alfonso  ",
                ReceiverDisplayName = "  Morgan  ",
                SenderAvatar = "  https://avatars.example.com/player-a.png  ",
                ReceiverAvatar = "http://avatars.example.com/player-b.png"
            };

            chatModule.NormalizeTrustedPresentation(packet);

            Assert.That(packet.SenderDisplayName, Is.EqualTo("Alfonso"));
            Assert.That(packet.ReceiverDisplayName, Is.EqualTo("Morgan"));
            Assert.That(packet.SenderAvatar, Is.EqualTo("https://avatars.example.com/player-a.png"));
            Assert.That(packet.ReceiverAvatar, Is.Empty);
        }

        [Test]
        public void TrustedPresentation_RejectsAvatarLongerThanLimit()
        {
            var packet = new ServerChatMessagePacket
            {
                SenderAvatar = $"https://avatars.example.com/{new string('a', ChatMessagePacket.MaxAvatarLength)}"
            };

            chatModule.NormalizeTrustedPresentation(packet);

            Assert.That(packet.SenderAvatar, Is.Empty);
        }

        [Test]
        public void ClientPresentation_ClearsUntrustedDisplayNamesAndAvatars()
        {
            var packet = new ChatMessagePacket
            {
                SenderDisplayName = "Spoofed sender",
                ReceiverDisplayName = "Spoofed receiver",
                SenderAvatar = "https://avatars.example.com/spoofed-sender.png",
                ReceiverAvatar = "https://avatars.example.com/spoofed-receiver.png"
            };

            chatModule.ClearUntrustedPresentation(packet);

            Assert.That(packet.SenderDisplayName, Is.Empty);
            Assert.That(packet.ReceiverDisplayName, Is.Empty);
            Assert.That(packet.SenderAvatar, Is.Empty);
            Assert.That(packet.ReceiverAvatar, Is.Empty);
        }

        [Test]
        public void ChatServerPrivateMessage_WithAvatars_WritesTrustedServerPacket()
        {
            var socket = new FakeClientSocket();
            var chatServer = new ChatServer(socket);

            chatServer.SendPrivateMessage(
                "player-a",
                "Alfonso",
                "https://avatars.example.com/player-a.png",
                "player-b",
                "Morgan",
                "https://avatars.example.com/player-b.png",
                "hello");

            Assert.That(socket.Requests.Count, Is.EqualTo(1));
            Assert.That(socket.Requests[0].Message.OpCode, Is.EqualTo(MstOpCodes.ServerSendChatMessageToUsers));

            ServerChatMessagePacket packet = SerializablePacket.FromBytes<ServerChatMessagePacket>(
                socket.Requests[0].Message.Data);

            Assert.That(packet.SenderAvatar, Is.EqualTo("https://avatars.example.com/player-a.png"));
            Assert.That(packet.ReceiverAvatar, Is.EqualTo("https://avatars.example.com/player-b.png"));
        }

        private sealed class TestChatModule : ChatModule
        {
            public void NormalizeTrustedPresentation(ServerChatMessagePacket packet)
            {
                NormalizeServerProvidedPresentation(packet);
            }

            public void ClearUntrustedPresentation(ChatMessagePacket packet)
            {
                ClearClientProvidedPresentation(packet);
            }
        }
    }
}
