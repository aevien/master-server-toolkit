using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine.TestTools;

namespace MasterServerToolkit.Tests.EditMode
{
    [TestFixture]
    public class NetworkPayloadLimitTests
    {
        [Test]
        public void ReadBytes_WhenRequestedCountExceedsSeekableStream_ReturnsOnlyRemainingBytes()
        {
            byte[] source = { 1, 2, 3 };

            using (var stream = new MemoryStream(source))
            using (var reader = new EndianBinaryReader(EndianBitConverter.Big, stream))
            {
                byte[] result = reader.ReadBytes(int.MaxValue);

                Assert.That(result, Is.EqualTo(source));
                Assert.That(reader.RemainingByteCount, Is.Zero);
            }
        }

        [Test]
        public void ReadDictionary_WhenDeclaredPayloadIsExcessive_RejectsBeforeAllocation()
        {
            byte[] data = EndianBitConverter.Big.GetBytes(int.MaxValue);

            using (var stream = new MemoryStream(data))
            using (var reader = new EndianBinaryReader(EndianBitConverter.Big, stream))
            {
                Assert.Throws<InvalidDataException>(() => reader.ReadDictionary());
            }
        }

        [Test]
        public void DictionaryFromBytes_WhenEntryCountIsExcessive_RejectsBeforeLooping()
        {
            byte[] data = EndianBitConverter.Big.GetBytes(int.MaxValue);

            Assert.Throws<InvalidDataException>(
                () => new Dictionary<string, string>().FromBytes(data));
        }

        [Test]
        public void ObservableProfile_WhenPropertyCountIsExcessive_RejectsPayload()
        {
            byte[] data = EndianBitConverter.Big.GetBytes(int.MaxValue);

            using (var profile = new ObservableProfile())
            {
                Assert.Throws<InvalidDataException>(() => profile.FromBytes(data));
            }
        }

        [Test]
        public void MessageFactory_WhenPayloadLengthIsExcessive_ReturnsNullWithoutAllocatingPayload()
        {
            byte[] data = new byte[
                sizeof(byte) +
                sizeof(ushort) +
                sizeof(int) +
                MstNetworkLimits.MaxMessagePayloadByteCount +
                1];
            EndianBitConverter.Big.CopyBytes(
                MstNetworkLimits.MaxMessagePayloadByteCount + 1,
                data,
                3);
            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;

            try
            {
                IIncomingMessage message = new MessageFactory().FromBytes(data, 0, null);
                Assert.That(message, Is.Null);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }
        }

        [Test]
        public void MessageFactory_WhenWireMessageExceedsLimit_ReturnsNull()
        {
            byte[] data = new byte[MstNetworkLimits.MaxWireMessageByteCount + 1];
            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;

            try
            {
                IIncomingMessage message = new MessageFactory().FromBytes(data, 0, null);
                Assert.That(message, Is.Null);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }
        }

        [Test]
        public void MessageFactory_WhenPayloadIsTruncated_ReturnsNull()
        {
            byte[] data = new byte[7];
            EndianBitConverter.Big.CopyBytes(128, data, 3);
            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;

            try
            {
                IIncomingMessage message = new MessageFactory().FromBytes(data, 0, null);
                Assert.That(message, Is.Null);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }
        }

        [Test]
        public void MessageHelper_WhenTextIsAtLimit_CreatesMessage()
        {
            string text = new string('a', MstNetworkLimits.MaxTextPayloadByteCount);

            IOutgoingMessage message = MessageHelper.Create(1, text);

            Assert.That(message.Data.Length, Is.EqualTo(MstNetworkLimits.MaxTextPayloadByteCount));
        }

        [Test]
        public void MessageHelper_WhenTextExceedsLimit_RejectsBeforeEncodingArray()
        {
            string text = new string('a', MstNetworkLimits.MaxTextPayloadByteCount + 1);

            Assert.Throws<InvalidDataException>(() => MessageHelper.Create(1, text));
        }

        [Test]
        public void StringListWriter_WhenEntryCountExceedsLimit_RejectsBeforeWriting()
        {
            var items = new string[MstNetworkLimits.MaxCollectionEntryCount + 1];

            Assert.Throws<InvalidDataException>(() => items.ToBytes());
        }

        [Test]
        public void OutgoingMessage_MaxProtocolOverhead_CoversBothAcknowledgementFields()
        {
            var message = new OutgoingMessage(1, Array.Empty<byte>())
            {
                AckRequestId = 1,
                AckResponseId = 2
            };

            int protocolOverhead = message.ToBytes().Length;

            Assert.That(
                protocolOverhead,
                Is.EqualTo(MstNetworkLimits.MaxProtocolOverheadByteCount));
            Assert.That(
                MstNetworkLimits.MaxWireMessageByteCount,
                Is.EqualTo(
                    MstNetworkLimits.MaxMessagePayloadByteCount +
                    MstNetworkLimits.MaxProtocolOverheadByteCount));
        }
    }
}
