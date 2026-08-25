using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using NUnit.Framework;
using System.IO;

namespace MasterServerToolkit.Tests.EditMode
{
    [TestFixture]
    public class ProfilesServerTests
    {
        private const ushort PropertyKey = 601;

        [Test]
        public void FillProfileValues_WhenDisconnected_CompletesOnceWithoutSending()
        {
            var socket = new FakeClientSocket(false);
            var profiles = new ProfilesServer(socket);
            int callbackCount = 0;

            using (var profile = CreateProfile("user-1", 10, out ObservableInt property))
            {
                profiles.FillProfileValues(profile, (success, error) => callbackCount++, socket);

                Assert.That(callbackCount, Is.EqualTo(1));
                Assert.That(property.Value, Is.EqualTo(10));
                Assert.That(socket.Requests, Is.Empty);
                Assert.That(profiles.TryGetById("user-1", out _), Is.False);
            }
        }

        [Test]
        public void FillProfileValues_WhenSendThrows_CompletesOnceWithoutRegisteringProfile()
        {
            var socket = new FakeClientSocket
            {
                ThrowOnSendOpcode = MstOpCodes.ServerFillInProfileValues
            };
            var profiles = new ProfilesServer(socket);
            int callbackCount = 0;
            string callbackError = null;

            using (var profile = CreateProfile("user-2", 10, out ObservableInt property))
            {
                profiles.FillProfileValues(profile, (success, error) =>
                {
                    callbackCount++;
                    callbackError = error;
                }, socket);

                Assert.That(callbackCount, Is.EqualTo(1));
                Assert.That(callbackError, Is.EqualTo(Mst.Errors.Localize("ui.error.response.internal.message")));
                Assert.That(property.Value, Is.EqualTo(10));
                Assert.That(profiles.TryGetById("user-2", out _), Is.False);
            }
        }

        [Test]
        public void FillProfileValues_WhenPayloadIsMalformed_RollsBackAndDoesNotRegisterProfile()
        {
            var socket = new FakeClientSocket();
            var profiles = new ProfilesServer(socket);
            int callbackCount = 0;
            string callbackError = null;

            using (var profile = CreateProfile("user-3", 10, out ObservableInt property))
            {
                profiles.FillProfileValues(profile, (success, error) =>
                {
                    callbackCount++;
                    callbackError = error;
                }, socket);

                socket.RespondNext(MstOpCodes.ServerFillInProfileValues, ResponseStatus.Success,
                    new byte[] { 0, 0, 0, 1 });

                Assert.That(callbackCount, Is.EqualTo(1));
                Assert.That(callbackError, Is.EqualTo(Mst.Errors.Localize("ui.error.response.invalid.message")));
                Assert.That(property.Value, Is.EqualTo(10));
                Assert.That(profiles.TryGetById("user-3", out _), Is.False);
            }
        }

        [Test]
        public void FillProfileValues_WhenPayloadIsValid_AppliesAndRegistersProfile()
        {
            var socket = new FakeClientSocket();
            var profiles = new ProfilesServer(socket);
            int callbackCount = 0;
            bool callbackSuccess = false;

            using (var source = new ObservableProfile())
            using (var target = CreateProfile("user-4", 10, out ObservableInt targetProperty))
            {
                source.Add(new ObservableInt(PropertyKey, 42));

                profiles.FillProfileValues(target, (success, error) =>
                {
                    callbackCount++;
                    callbackSuccess = success;
                }, socket);

                socket.RespondNext(MstOpCodes.ServerFillInProfileValues, ResponseStatus.Success, source.ToBytes());

                Assert.That(callbackCount, Is.EqualTo(1));
                Assert.That(callbackSuccess, Is.True);
                Assert.That(targetProperty.Value, Is.EqualTo(42));
                Assert.That(profiles.TryGetById("user-4", out ObservableServerProfile registered), Is.True);
                Assert.That(registered, Is.SameAs(target));
            }
        }

        [Test]
        public void SaveProfile_SendsCurrentDeltaAndCompletesAfterMasterResponse()
        {
            var socket = new FakeClientSocket();
            var profiles = new ProfilesServer(socket);
            int callbackCount = 0;
            bool callbackSuccess = false;

            using (var profile = CreateProfile("user-save", 10, out ObservableInt property))
            using (var replica = CreateProfile("user-save", 10, out ObservableInt replicaProperty))
            {
                RegisterProfile(profiles, socket, profile);
                property.Add(5);

                profiles.SaveProfile(profile, (success, error) =>
                {
                    callbackCount++;
                    callbackSuccess = success;
                });

                FakeClientSocket.SentRequest request = socket.GetPendingRequest(
                    MstOpCodes.ServerUpdateProfileValues);
                ApplySingleProfileUpdate(request.Message.Data, replica);

                Assert.That(callbackCount, Is.EqualTo(0));
                Assert.That(profile.HasDirtyProperties, Is.False);
                Assert.That(replicaProperty.Value, Is.EqualTo(15));

                socket.RespondNext(MstOpCodes.ServerUpdateProfileValues,
                    ResponseStatus.Success);

                Assert.That(callbackCount, Is.EqualTo(1));
                Assert.That(callbackSuccess, Is.True);
            }
        }

        [Test]
        public void SaveProfile_WhenCalledAgain_QueuesOnlyChangesMadeAfterFirstCapture()
        {
            var socket = new FakeClientSocket();
            var profiles = new ProfilesServer(socket);
            int firstCallbackCount = 0;
            int secondCallbackCount = 0;

            using (var profile = CreateProfile("user-save-queue", 10, out ObservableInt property))
            using (var replica = CreateProfile("user-save-queue", 10, out ObservableInt replicaProperty))
            {
                RegisterProfile(profiles, socket, profile);

                property.Add(5);
                profiles.SaveProfile(profile, (success, error) => firstCallbackCount++);
                FakeClientSocket.SentRequest firstRequest = socket.GetPendingRequest(
                    MstOpCodes.ServerUpdateProfileValues);

                property.Add(2);
                profiles.SaveProfile(profile, (success, error) => secondCallbackCount++);

                Assert.That(socket.Requests.FindAll(request =>
                    request.Message.OpCode == MstOpCodes.ServerUpdateProfileValues).Count,
                    Is.EqualTo(1));

                ApplySingleProfileUpdate(firstRequest.Message.Data, replica);
                Assert.That(replicaProperty.Value, Is.EqualTo(15));

                socket.RespondNext(MstOpCodes.ServerUpdateProfileValues,
                    ResponseStatus.Success);

                Assert.That(firstCallbackCount, Is.EqualTo(1));
                Assert.That(secondCallbackCount, Is.EqualTo(0));
                Assert.That(socket.Requests.FindAll(request =>
                    request.Message.OpCode == MstOpCodes.ServerUpdateProfileValues).Count,
                    Is.EqualTo(2));

                FakeClientSocket.SentRequest secondRequest = socket.GetPendingRequest(
                    MstOpCodes.ServerUpdateProfileValues);
                ApplySingleProfileUpdate(secondRequest.Message.Data, replica);
                Assert.That(replicaProperty.Value, Is.EqualTo(17));

                socket.RespondNext(MstOpCodes.ServerUpdateProfileValues,
                    ResponseStatus.Success);

                Assert.That(secondCallbackCount, Is.EqualTo(1));
                Assert.That(profile.HasDirtyProperties, Is.False);
            }
        }

        [Test]
        public void SaveProfile_WhenFirstResultIsUnknown_DoesNotSendQueuedDelta()
        {
            var socket = new FakeClientSocket();
            var profiles = new ProfilesServer(socket);
            int firstCallbackCount = 0;
            int secondCallbackCount = 0;
            bool firstSuccess = true;
            bool secondSuccess = true;

            using (var profile = CreateProfile("user-save-failure", 10,
                out ObservableInt property))
            {
                RegisterProfile(profiles, socket, profile);

                property.Add(5);
                profiles.SaveProfile(profile, (success, error) =>
                {
                    firstCallbackCount++;
                    firstSuccess = success;
                });

                property.Add(2);
                profiles.SaveProfile(profile, (success, error) =>
                {
                    secondCallbackCount++;
                    secondSuccess = success;
                });

                socket.RespondNext(MstOpCodes.ServerUpdateProfileValues,
                    ResponseStatus.Timeout);

                Assert.That(firstCallbackCount, Is.EqualTo(1));
                Assert.That(secondCallbackCount, Is.EqualTo(1));
                Assert.That(firstSuccess, Is.False);
                Assert.That(secondSuccess, Is.False);
                Assert.That(socket.Requests.FindAll(request =>
                    request.Message.OpCode == MstOpCodes.ServerUpdateProfileValues).Count,
                    Is.EqualTo(1));
            }
        }

        [Test]
        public void ObservableInt_DeltaUpdates_MergeConcurrentRoomAndMasterChanges()
        {
            var master = new ObservableInt(PropertyKey, 100, true);
            var room = new ObservableInt(PropertyKey, 100, true);
            var client = new ObservableInt(PropertyKey, 100, true);

            master.Add(5);
            room.Add(10);

            master.ApplyUpdates(room.GetUpdates());
            client.ApplyUpdates(master.GetUpdates());

            Assert.That(master.Value, Is.EqualTo(115));
            Assert.That(client.Value, Is.EqualTo(115));
        }

        [Test]
        public void ObservableInt_DeltaUpdates_AfterClearSendOnlyNewChanges()
        {
            var source = new ObservableInt(PropertyKey, 100, true);
            var target = new ObservableInt(PropertyKey, 100, true);

            source.Add(10);
            target.ApplyUpdates(source.GetUpdates());
            source.ClearUpdates();
            source.Subtract(3);
            target.ApplyUpdates(source.GetUpdates());

            Assert.That(source.Value, Is.EqualTo(107));
            Assert.That(target.Value, Is.EqualTo(107));
        }

        private static ObservableServerProfile CreateProfile(string userId, int value, out ObservableInt property)
        {
            var profile = new ObservableServerProfile(userId);
            property = new ObservableInt(PropertyKey, value);
            profile.Add(property);
            return profile;
        }

        private static void RegisterProfile(ProfilesServer profiles,
            FakeClientSocket socket, ObservableServerProfile profile)
        {
            profiles.FillProfileValues(profile, null, socket);
            socket.RespondNext(MstOpCodes.ServerFillInProfileValues,
                ResponseStatus.Success, profile.ToBytes());
        }

        private static void ApplySingleProfileUpdate(byte[] payload,
            ObservableServerProfile profile)
        {
            using (var stream = new MemoryStream(payload))
            using (var reader = new EndianBinaryReader(
                EndianBitConverter.Big, stream))
            {
                int count = reader.ReadCount32(1, "Profile update batch");
                Assert.That(count, Is.EqualTo(1));
                Assert.That(reader.ReadString(), Is.EqualTo(profile.UserId));
                int updateLength = reader.ReadLength32(
                    MstNetworkLimits.MaxMessagePayloadByteCount,
                    "Profile update");
                profile.ApplyUpdates(reader.ReadBytesExact(updateLength,
                    MstNetworkLimits.MaxMessagePayloadByteCount));
                Assert.That(stream.Position, Is.EqualTo(stream.Length));
            }
        }
    }
}
