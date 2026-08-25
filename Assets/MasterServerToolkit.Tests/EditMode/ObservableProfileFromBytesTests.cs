using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using NUnit.Framework;
using System;
using System.IO;

namespace MasterServerToolkit.Tests.EditMode
{
    [TestFixture]
    public class ObservableProfileFromBytesTests
    {
        private const ushort IntPropertyKey = 101;
        private const ushort StringPropertyKey = 202;
        private const ushort ThrowingPropertyKey = 303;

        [Test]
        public void TryGet_WhenPropertyTypeDoesNotMatch_ReturnsFalseAndNull()
        {
            using (var profile = new ObservableProfile())
            {
                profile.Add(new ObservableInt(IntPropertyKey, 42));

                bool found = profile.TryGet(IntPropertyKey, out ObservableLong property);

                Assert.That(found, Is.False);
                Assert.That(property, Is.Null);
            }
        }

        [Test]
        public void FromBytes_ValidPayload_AppliesAllKnownProperties()
        {
            using (var source = new ObservableProfile())
            using (var target = new ObservableProfile())
            {
                source.Add(new ObservableInt(IntPropertyKey, 42));
                source.Add(new ObservableString(StringPropertyKey, "survivor"));

                var targetInt = new ObservableInt(IntPropertyKey, -1);
                var targetString = new ObservableString(StringPropertyKey, "before");
                target.Add(targetInt);
                target.Add(targetString);

                target.FromBytes(source.ToBytes());

                Assert.That(targetInt.Value, Is.EqualTo(42));
                Assert.That(targetString.Value, Is.EqualTo("survivor"));
            }
        }

        [Test]
        public void FromBytes_NegativePropertyCount_ThrowsInvalidDataException()
        {
            using (var profile = new ObservableProfile())
            {
                Assert.Throws<InvalidDataException>(() => profile.FromBytes(SerializeInt(-1)));
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void FromBytes_TruncatedCountHeader_ThrowsInvalidDataException(int byteCount)
        {
            var completeHeader = SerializeInt(1);
            var truncatedHeader = new byte[byteCount];
            Array.Copy(completeHeader, truncatedHeader, byteCount);

            using (var profile = new ObservableProfile())
            {
                Assert.Throws<InvalidDataException>(() => profile.FromBytes(truncatedHeader));
            }
        }

        [Test]
        public void FromBytes_TruncatedPropertyHeader_ThrowsInvalidDataException()
        {
            var payload = BuildRawPayload(writer =>
            {
                writer.Write(1);
                writer.Write(IntPropertyKey);
            });

            using (var profile = new ObservableProfile())
            {
                Assert.Throws<InvalidDataException>(() => profile.FromBytes(payload));
            }
        }

        [Test]
        public void FromBytes_TruncatedPropertyPayload_ThrowsInvalidDataException()
        {
            var payload = BuildRawPayload(writer =>
            {
                writer.Write(1);
                writer.Write(IntPropertyKey);
                writer.Write(sizeof(int));
                writer.Write(new byte[] { 0, 0, 0 });
            });

            using (var profile = new ObservableProfile())
            {
                Assert.Throws<InvalidDataException>(() => profile.FromBytes(payload));
            }
        }

        [Test]
        public void FromBytes_DuplicatePropertyKey_ThrowsWithoutApplyingPayload()
        {
            var payload = BuildPayload(
                new PayloadEntry(IntPropertyKey, SerializeInt(20)),
                new PayloadEntry(IntPropertyKey, SerializeInt(30)));

            using (var profile = new ObservableProfile())
            {
                var property = new ObservableInt(IntPropertyKey, 10);
                profile.Add(property);

                Assert.Throws<InvalidDataException>(() => profile.FromBytes(payload));
                Assert.That(property.Value, Is.EqualTo(10));
            }
        }

        [Test]
        public void FromBytes_TrailingBytes_ThrowsWithoutApplyingPayload()
        {
            var validPayload = BuildPayload(new PayloadEntry(IntPropertyKey, SerializeInt(20)));
            var payloadWithTrailingByte = new byte[validPayload.Length + 1];
            Buffer.BlockCopy(validPayload, 0, payloadWithTrailingByte, 0, validPayload.Length);
            payloadWithTrailingByte[payloadWithTrailingByte.Length - 1] = 0x7F;

            using (var profile = new ObservableProfile())
            {
                var property = new ObservableInt(IntPropertyKey, 10);
                profile.Add(property);

                Assert.Throws<InvalidDataException>(() => profile.FromBytes(payloadWithTrailingByte));
                Assert.That(property.Value, Is.EqualTo(10));
            }
        }

        [Test]
        public void FromBytes_PropertyDecoderThrows_RollsBackEveryAppliedProperty()
        {
            const int rejectedValue = 999;
            var payload = BuildPayload(
                new PayloadEntry(IntPropertyKey, SerializeInt(100)),
                new PayloadEntry(ThrowingPropertyKey, SerializeInt(rejectedValue)));

            using (var profile = new ObservableProfile())
            {
                var firstProperty = new ObservableInt(IntPropertyKey, 10);
                var throwingProperty = new ThrowingObservableInt(ThrowingPropertyKey, 20, rejectedValue);
                profile.Add(firstProperty);
                profile.Add(throwingProperty);

                Assert.Throws<InvalidOperationException>(() => profile.FromBytes(payload));

                Assert.That(firstProperty.Value, Is.EqualTo(10));
                Assert.That(throwingProperty.Value, Is.EqualTo(20));
            }
        }

        private static byte[] BuildPayload(params PayloadEntry[] entries)
        {
            return BuildRawPayload(writer =>
            {
                writer.Write(entries.Length);

                foreach (var entry in entries)
                {
                    writer.Write(entry.Key);
                    writer.Write(entry.Data.Length);
                    writer.Write(entry.Data);
                }
            });
        }

        private static byte[] BuildRawPayload(Action<EndianBinaryWriter> writePayload)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new EndianBinaryWriter(EndianBitConverter.Big, stream))
                {
                    writePayload(writer);
                }

                return stream.ToArray();
            }
        }

        private static byte[] SerializeInt(int value)
        {
            var data = new byte[sizeof(int)];
            EndianBitConverter.Big.CopyBytes(value, data, 0);
            return data;
        }

        private struct PayloadEntry
        {
            public PayloadEntry(ushort key, byte[] data)
            {
                Key = key;
                Data = data;
            }

            public ushort Key { get; }
            public byte[] Data { get; }
        }

        private sealed class ThrowingObservableInt : ObservableInt
        {
            private readonly int rejectedValue;

            public ThrowingObservableInt(ushort key, int defaultValue, int rejectedValue)
                : base(key, defaultValue)
            {
                this.rejectedValue = rejectedValue;
            }

            public override void FromBytes(byte[] data)
            {
                base.FromBytes(data);

                if (Value == rejectedValue)
                    throw new InvalidOperationException("Rejected test value");
            }
        }
    }
}
