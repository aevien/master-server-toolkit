using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.Tests.EditMode
{
    [TestFixture]
    [Parallelizable(ParallelScope.None)]
    public class ProfilesClientTests
    {
        private const ushort PropertyKey = 501;
        private readonly List<FakeClientSocket> sockets = new List<FakeClientSocket>();
        private bool hadAuthToken;
        private string originalAuthToken;

        [SetUp]
        public void SetUp()
        {
            hadAuthToken = PlayerPrefs.HasKey(MstParamKeys.USER_AUTH_TOKEN);
            originalAuthToken = hadAuthToken ? PlayerPrefs.GetString(MstParamKeys.USER_AUTH_TOKEN) : null;
            PlayerPrefs.DeleteKey(MstParamKeys.USER_AUTH_TOKEN);
            EnsureGlobalAuthIsSignedOut();
        }

        [TearDown]
        public void TearDown()
        {
            EnsureGlobalAuthIsSignedOut();

            foreach (FakeClientSocket socket in sockets)
                socket.Close();

            sockets.Clear();
            PlayerPrefs.DeleteKey(MstParamKeys.USER_AUTH_TOKEN);

            if (hadAuthToken)
                PlayerPrefs.SetString(MstParamKeys.USER_AUTH_TOKEN, originalAuthToken);

            PlayerPrefs.Save();
        }

        [Test]
        public void FillInProfileValues_WhenDisconnected_CompletesOnceWithoutSending()
        {
            FakeClientSocket socket = CreateSocket(false);
            var profiles = CreateProfilesClient(socket);
            int callbackCount = 0;

            using (var profile = CreateProfile(10, out ObservableInt property))
            {
                profiles.FillInProfileValues(profile, (success, error) => callbackCount++, socket);

                Assert.That(callbackCount, Is.EqualTo(1));
                Assert.That(property.Value, Is.EqualTo(10));
                Assert.That(profiles.IsLoaded, Is.False);
                Assert.That(socket.Requests, Is.Empty);
            }
        }

        [Test]
        public void FillInProfileValues_WhenSignedOut_CompletesOnceWithoutSending()
        {
            FakeClientSocket socket = CreateSocket();
            var profiles = CreateProfilesClient(socket);
            int callbackCount = 0;
            bool callbackSuccess = true;

            using (var profile = CreateProfile(10, out ObservableInt property))
            {
                profiles.FillInProfileValues(profile, (success, error) =>
                {
                    callbackCount++;
                    callbackSuccess = success;
                }, socket);

                Assert.That(callbackCount, Is.EqualTo(1));
                Assert.That(callbackSuccess, Is.False);
                Assert.That(property.Value, Is.EqualTo(10));
                Assert.That(profiles.IsLoaded, Is.False);
                Assert.That(socket.Requests, Is.Empty);
            }
        }

        [Test]
        public void FillInProfileValues_WhenSendThrows_CompletesWithRequestFailure()
        {
            SignInGlobalAuth();
            FakeClientSocket socket = CreateSocket();
            socket.ThrowOnSendOpcode = MstOpCodes.ClientFillInProfileValues;
            var profiles = CreateProfilesClient(socket);
            int callbackCount = 0;
            string callbackError = null;

            using (var profile = CreateProfile(10, out ObservableInt property))
            {
                profiles.FillInProfileValues(profile, (success, error) =>
                {
                    callbackCount++;
                    callbackError = error;
                }, socket);

                Assert.That(callbackCount, Is.EqualTo(1));
                Assert.That(callbackError, Is.EqualTo(Mst.Errors.Localize("ui.error.response.internal.message")));
                Assert.That(property.Value, Is.EqualTo(10));
                Assert.That(profiles.IsLoaded, Is.False);
            }
        }

        [Test]
        public void FillInProfileValues_WhenServerRejects_ReturnsServerErrorWithoutLoading()
        {
            SignInGlobalAuth();
            FakeClientSocket socket = CreateSocket();
            var profiles = CreateProfilesClient(socket);
            int callbackCount = 0;
            string callbackError = null;

            using (var profile = CreateProfile(10, out ObservableInt property))
            {
                profiles.FillInProfileValues(profile, (success, error) =>
                {
                    callbackCount++;
                    callbackError = error;
                }, socket);

                socket.RespondNext(MstOpCodes.ClientFillInProfileValues, ResponseStatus.Forbidden,
                    CreateStructuredError(MstErrorCodes.PERMISSION_DENIED));

                Assert.That(callbackCount, Is.EqualTo(1));
                Assert.That(callbackError, Is.EqualTo(Mst.Errors.Localize("ui.error.response.forbidden.message")));
                Assert.That(property.Value, Is.EqualTo(10));
                Assert.That(profiles.IsLoaded, Is.False);
            }
        }

        [Test]
        public void FillInProfileValues_WhenErrorResponseIsNull_ReturnsDecodeFailure()
        {
            SignInGlobalAuth();
            FakeClientSocket socket = CreateSocket();
            var profiles = CreateProfilesClient(socket);
            string callbackError = null;

            using (var profile = CreateProfile(10, out ObservableInt property))
            {
                profiles.FillInProfileValues(profile, (success, error) => callbackError = error, socket);
                socket.RespondNextWithNull(MstOpCodes.ClientFillInProfileValues, ResponseStatus.Error);

                Assert.That(callbackError, Is.EqualTo(Mst.Errors.Localize("ui.error.response.internal.message")));
                Assert.That(property.Value, Is.EqualTo(10));
                Assert.That(profiles.IsLoaded, Is.False);
            }
        }

        [Test]
        public void FillInProfileValues_WhenSuccessPayloadIsMalformed_ReturnsProcessingFailure()
        {
            SignInGlobalAuth();
            FakeClientSocket socket = CreateSocket();
            var profiles = CreateProfilesClient(socket);
            int callbackCount = 0;
            string callbackError = null;

            using (var profile = CreateProfile(10, out ObservableInt property))
            {
                profiles.FillInProfileValues(profile, (success, error) =>
                {
                    callbackCount++;
                    callbackError = error;
                }, socket);

                socket.RespondNext(MstOpCodes.ClientFillInProfileValues, ResponseStatus.Success,
                    new byte[] { 0, 0, 0, 1 });

                Assert.That(callbackCount, Is.EqualTo(1));
                Assert.That(callbackError, Is.EqualTo(Mst.Errors.Localize("ui.error.response.internal.message")));
                Assert.That(property.Value, Is.EqualTo(10));
                Assert.That(profiles.IsLoaded, Is.False);
                Assert.That(socket.RegisteredHandlerCount, Is.Zero);
            }
        }

        [Test]
        public void FillInProfileValues_WhenHandlerRegistrationThrows_DoesNotSetCurrent()
        {
            SignInGlobalAuth();
            FakeClientSocket socket = CreateSocket();
            socket.ThrowOnRegisterOpcode = MstOpCodes.UpdateClientProfile;
            var profiles = CreateProfilesClient(socket);
            int callbackCount = 0;
            string callbackError = null;

            using (var source = CreateProfile(42, out ObservableInt sourceProperty))
            using (var target = CreateProfile(10, out ObservableInt targetProperty))
            {
                profiles.FillInProfileValues(target, (success, error) =>
                {
                    callbackCount++;
                    callbackError = error;
                }, socket);

                socket.RespondNext(MstOpCodes.ClientFillInProfileValues, ResponseStatus.Success, source.ToBytes());

                Assert.That(callbackCount, Is.EqualTo(1));
                Assert.That(callbackError, Is.EqualTo(Mst.Errors.Localize("ui.error.response.internal.message")));
                Assert.That(sourceProperty.Value, Is.EqualTo(42));
                Assert.That(targetProperty.Value, Is.EqualTo(42));
                Assert.That(profiles.IsLoaded, Is.False);
            }
        }

        [Test]
        public void FillInProfileValues_WhenSuccessful_LoadsProfileAndAppliesLaterUpdates()
        {
            SignInGlobalAuth();
            FakeClientSocket socket = CreateSocket();
            var profiles = CreateProfilesClient(socket);
            int callbackCount = 0;
            int loadedEventCount = 0;
            bool callbackSuccess = false;

            profiles.OnProfileLoadedEvent += loadedProfile => loadedEventCount++;

            using (var source = CreateProfile(42, out ObservableInt sourceProperty))
            using (var target = CreateProfile(10, out ObservableInt targetProperty))
            {
                profiles.FillInProfileValues(target, (success, error) =>
                {
                    callbackCount++;
                    callbackSuccess = success;
                }, socket);

                socket.RespondNext(MstOpCodes.ClientFillInProfileValues, ResponseStatus.Success, source.ToBytes());

                source.ClearUpdates();
                sourceProperty.Value = 77;
                socket.Deliver(MstOpCodes.UpdateClientProfile, source.GetUpdates());

                Assert.That(callbackCount, Is.EqualTo(1));
                Assert.That(callbackSuccess, Is.True);
                Assert.That(loadedEventCount, Is.EqualTo(1));
                Assert.That(profiles.Current, Is.SameAs(target));
                Assert.That(profiles.IsLoaded, Is.True);
                Assert.That(targetProperty.Value, Is.EqualTo(77));
                Assert.That(target.HasDirtyProperties, Is.False);
                Assert.That(socket.RegisteredHandlerCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void UnloadProfile_WhenProfileIsLoaded_RemovesProfileAndUpdateHandler()
        {
            SignInGlobalAuth();
            FakeClientSocket socket = CreateSocket();
            var profiles = CreateProfilesClient(socket);

            using (var source = CreateProfile(42, out _))
            using (var target = CreateProfile(10, out _))
            {
                profiles.FillInProfileValues(target, (success, error) => { }, socket);
                socket.RespondNext(MstOpCodes.ClientFillInProfileValues, ResponseStatus.Success, source.ToBytes());

                profiles.UnloadProfile();

                Assert.That(profiles.Current, Is.Null);
                Assert.That(profiles.IsLoaded, Is.False);
                Assert.That(socket.RegisteredHandlerCount, Is.Zero);
                Assert.That(target.Count, Is.Zero);
            }
        }

        [Test]
        public void OnProfileLoadedEvent_WhenSubscribedAfterInitialLoad_ReceivesReplacementProfile()
        {
            SignInGlobalAuth();
            FakeClientSocket socket = CreateSocket();
            var profiles = CreateProfilesClient(socket);
            int eventCount = 0;
            ObservableProfile observedProfile = null;

            using (var firstSource = CreateProfile(10, out _))
            using (var firstTarget = CreateProfile(0, out _))
            using (var secondSource = CreateProfile(20, out _))
            using (var secondTarget = CreateProfile(0, out _))
            {
                profiles.FillInProfileValues(firstTarget, (success, error) => { }, socket);
                socket.RespondNext(MstOpCodes.ClientFillInProfileValues, ResponseStatus.Success, firstSource.ToBytes());

                profiles.OnProfileLoadedEvent += profile =>
                {
                    eventCount++;
                    observedProfile = profile;
                };

                profiles.FillInProfileValues(secondTarget, (success, error) => { }, socket);
                socket.RespondNext(MstOpCodes.ClientFillInProfileValues, ResponseStatus.Success, secondSource.ToBytes());

                Assert.That(eventCount, Is.EqualTo(2));
                Assert.That(observedProfile, Is.SameAs(secondTarget));
                Assert.That(profiles.Current, Is.SameAs(secondTarget));
                Assert.That(socket.RegisteredHandlerCount, Is.EqualTo(1));
            }
        }

        private void SignInGlobalAuth()
        {
            FakeClientSocket socket = CreateSocket();
            AccountInfoPacket account = null;

            Mst.Client.Auth.SignIn(
                new MstProperties(),
                (status, result, error) => account = result,
                socket);
            MstTestData.CompleteSealChallenge(socket);
            socket.RespondNext(MstOpCodes.SignIn, ResponseStatus.Success, MstTestData.CreateAccountPacketBytes());

            Assert.That(account, Is.Not.Null, "Profile test setup could not sign in the MST client");
        }

        private void EnsureGlobalAuthIsSignedOut()
        {
            if (!Mst.Client.Auth.IsSignedIn)
                return;

            var disconnectedSocket = new FakeClientSocket(false);
            Mst.Client.Auth.SignOut(disconnectedSocket);
        }

        private FakeClientSocket CreateSocket(bool isConnected = true)
        {
            var socket = new FakeClientSocket(isConnected);
            sockets.Add(socket);
            return socket;
        }

        private static ProfilesClient CreateProfilesClient(IClientSocket socket)
        {
            return new ProfilesClient(socket);
        }

        private static byte[] CreateStructuredError(string code)
        {
            var properties = new MstProperties();
            properties.Set(MstErrorPropertyKeys.CODE, code);
            return properties.ToBytes();
        }

        private static ObservableProfile CreateProfile(int value, out ObservableInt property)
        {
            var profile = new ObservableProfile();
            property = new ObservableInt(PropertyKey, value);
            profile.Add(property);
            return profile;
        }
    }
}
