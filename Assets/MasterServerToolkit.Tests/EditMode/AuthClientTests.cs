using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.Tests.EditMode
{
    [TestFixture]
    [Parallelizable(ParallelScope.None)]
    public class AuthClientTests
    {
        private readonly List<FakeClientSocket> sockets = new List<FakeClientSocket>();
        private bool hadAuthToken;
        private string originalAuthToken;

        [SetUp]
        public void SetUp()
        {
            MstTestData.InitializeServerSecurity(Mst.Security);
            hadAuthToken = PlayerPrefs.HasKey(MstParamKeys.USER_AUTH_TOKEN);
            originalAuthToken = hadAuthToken ? PlayerPrefs.GetString(MstParamKeys.USER_AUTH_TOKEN) : null;
            PlayerPrefs.DeleteKey(MstParamKeys.USER_AUTH_TOKEN);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (FakeClientSocket socket in sockets)
                socket.Close();

            sockets.Clear();
            PlayerPrefs.DeleteKey(MstParamKeys.USER_AUTH_TOKEN);

            if (hadAuthToken)
                PlayerPrefs.SetString(MstParamKeys.USER_AUTH_TOKEN, originalAuthToken);

            PlayerPrefs.Save();
        }

        [Test]
        public void SignIn_WhenDisconnected_ReturnsNotConnectedOnceWithoutSending()
        {
            FakeClientSocket socket = CreateSocket(false);
            var auth = new AuthClient(socket);
            int callbackCount = 0;
            ResponseStatus callbackStatus = ResponseStatus.Success;

            auth.SignIn(new MstProperties(), (status, account, error) =>
            {
                callbackCount++;
                callbackStatus = status;
            }, socket);

            Assert.That(callbackCount, Is.EqualTo(1));
            Assert.That(callbackStatus, Is.EqualTo(ResponseStatus.NotConnected));
            Assert.That(auth.IsNowSigningIn, Is.False);
            Assert.That(auth.IsSignedIn, Is.False);
            Assert.That(socket.Requests, Is.Empty);
        }

        [Test]
        public void ClearAuthToken_RemovesOnlySavedAuthenticationToken()
        {
            const string unrelatedPreferenceKey = "mst.tests.auth.unrelated-preference";
            var auth = new AuthClient(CreateSocket());
            PlayerPrefs.SetString(MstParamKeys.USER_AUTH_TOKEN, "saved-authentication-token");
            PlayerPrefs.SetString(unrelatedPreferenceKey, "keep-me");
            PlayerPrefs.Save();

            try
            {
                auth.ClearAuthToken();

                Assert.That(auth.HasAuthToken(), Is.False);
                Assert.That(PlayerPrefs.GetString(unrelatedPreferenceKey), Is.EqualTo("keep-me"));
                Assert.That(auth.IsSignedIn, Is.False);
            }
            finally
            {
                PlayerPrefs.DeleteKey(unrelatedPreferenceKey);
                PlayerPrefs.Save();
            }
        }

        [Test]
        public void ClearAuthToken_RemovesStoredKeyWithEmptyValue()
        {
            var auth = new AuthClient(CreateSocket());
            PlayerPrefs.SetString(MstParamKeys.USER_AUTH_TOKEN, string.Empty);
            PlayerPrefs.Save();

            auth.ClearAuthToken();

            Assert.That(PlayerPrefs.HasKey(MstParamKeys.USER_AUTH_TOKEN), Is.False);
        }

        [Test]
        public void SignIn_WhenAnotherAttemptIsPending_RejectsOnlySecondAttempt()
        {
            FakeClientSocket socket = CreateSocket();
            var auth = new AuthClient(socket);
            int firstCallbackCount = 0;
            int secondCallbackCount = 0;
            ResponseStatus secondCallbackStatus = ResponseStatus.Success;

            auth.SignIn(new MstProperties(), (status, account, error) => firstCallbackCount++, socket);
            auth.SignIn(new MstProperties(), (status, account, error) =>
            {
                secondCallbackCount++;
                secondCallbackStatus = status;
            }, socket);

            Assert.That(firstCallbackCount, Is.Zero);
            Assert.That(secondCallbackCount, Is.EqualTo(1));
            Assert.That(secondCallbackStatus, Is.EqualTo(ResponseStatus.Conflict));
            Assert.That(auth.IsNowSigningIn, Is.True);
            Assert.That(socket.Requests.Count, Is.EqualTo(1));
            Assert.That(socket.Requests[0].Message.OpCode, Is.EqualTo(MstOpCodes.SealChallengeRequest));

            MstTestData.CompleteSealChallenge(socket);
            socket.RespondNextWithNull(MstOpCodes.SignIn, ResponseStatus.Error);

            Assert.That(firstCallbackCount, Is.EqualTo(1));
            Assert.That(secondCallbackCount, Is.EqualTo(1));
            Assert.That(auth.IsNowSigningIn, Is.False);
        }

        [Test]
        public void SignIn_WhenSealChallengeFails_CompletesOnceAndResetsState()
        {
            FakeClientSocket socket = CreateSocket();
            var auth = new AuthClient(socket);
            int callbackCount = 0;
            AccountInfoPacket callbackAccount = null;

            auth.SignIn(new MstProperties(), (status, account, error) =>
            {
                callbackCount++;
                callbackAccount = account;
            }, socket);

            socket.RespondNext(MstOpCodes.SealChallengeRequest, ResponseStatus.Error);

            Assert.That(callbackCount, Is.EqualTo(1));
            Assert.That(callbackAccount, Is.Null);
            Assert.That(auth.IsNowSigningIn, Is.False);
            Assert.That(socket.Requests.Count, Is.EqualTo(1));
        }

        [Test]
        public void SignIn_WhenSealChallengeIsMalformed_DoesNotSendCredentials()
        {
            FakeClientSocket socket = CreateSocket();
            var auth = new AuthClient(socket);
            int callbackCount = 0;

            auth.SignIn(new MstProperties(), (status, account, error) => callbackCount++, socket);
            FakeClientSocket.SentRequest request =
                socket.GetPendingRequest(MstOpCodes.SealChallengeRequest);
            request.Respond(ResponseStatus.Success, new byte[] { 2, 1, 0 });

            Assert.That(callbackCount, Is.EqualTo(1));
            Assert.That(auth.IsNowSigningIn, Is.False);
            Assert.That(auth.IsSignedIn, Is.False);
            Assert.That(socket.Requests.Count, Is.EqualTo(1));
        }

        [Test]
        public void SignIn_WhenCredentialSendThrows_CompletesOnceAndResetsState()
        {
            FakeClientSocket socket = CreateSocket();
            var auth = new AuthClient(socket);
            int callbackCount = 0;

            auth.SignIn(new MstProperties(), (status, account, error) => callbackCount++, socket);
            socket.ThrowOnSendOpcode = MstOpCodes.SignIn;
            MstTestData.CompleteSealChallenge(socket);

            Assert.That(callbackCount, Is.EqualTo(1));
            Assert.That(auth.IsNowSigningIn, Is.False);
            Assert.That(auth.IsSignedIn, Is.False);
        }

        [Test]
        public void SignIn_WhenServerFailureHasNoResponse_CompletesOnce()
        {
            FakeClientSocket socket = CreateSocket();
            var auth = new AuthClient(socket);
            int callbackCount = 0;
            string callbackError = null;

            auth.SignIn(new MstProperties(), (status, account, error) =>
            {
                callbackCount++;
                callbackError = error;
            }, socket);

            MstTestData.CompleteSealChallenge(socket);
            socket.RespondNextWithNull(MstOpCodes.SignIn, ResponseStatus.Error);

            Assert.That(callbackCount, Is.EqualTo(1));
            Assert.That(callbackError, Is.Not.Null.And.Not.Empty);
            Assert.That(auth.IsNowSigningIn, Is.False);
            Assert.That(auth.IsSignedIn, Is.False);
        }

        [Test]
        public void SignIn_WhenSuccessPayloadIsMalformed_CompletesOnceWithoutSigningIn()
        {
            FakeClientSocket socket = CreateSocket();
            var auth = new AuthClient(socket);
            int callbackCount = 0;

            auth.SignIn(new MstProperties(), (status, account, error) => callbackCount++, socket);
            MstTestData.CompleteSealChallenge(socket);
            socket.RespondNext(MstOpCodes.SignIn, ResponseStatus.Success, new byte[] { 1, 2, 3 });

            Assert.That(callbackCount, Is.EqualTo(1));
            Assert.That(auth.IsNowSigningIn, Is.False);
            Assert.That(auth.IsSignedIn, Is.False);
        }

        [Test]
        public void SignIn_WhenSuccessPayloadIsValid_SetsAccountBeforeRaisingEvent()
        {
            FakeClientSocket socket = CreateSocket();
            var auth = new AuthClient(socket);
            var order = new List<string>();
            int callbackCount = 0;
            AccountInfoPacket callbackAccount = null;

            auth.OnSignedInEvent += () => order.Add("event");
            auth.SignIn(new MstProperties(), (status, account, error) =>
            {
                callbackCount++;
                callbackAccount = account;
                order.Add("callback");
            }, socket);

            MstTestData.CompleteSealChallenge(socket);
            socket.RespondNext(MstOpCodes.SignIn, ResponseStatus.Success,
                MstTestData.CreateAccountPacketBytes(username: "player-one"));

            Assert.That(callbackCount, Is.EqualTo(1));
            Assert.That(callbackAccount, Is.SameAs(auth.Account));
            Assert.That(auth.Account.Username, Is.EqualTo("player-one"));
            Assert.That(auth.IsNowSigningIn, Is.False);
            Assert.That(auth.IsSignedIn, Is.True);
            Assert.That(order, Is.EqualTo(new[] { "callback", "event" }));
        }

        [Test]
        public void SignInAsGuest_WhenSuccessful_SavesTokenAndSignOutClearsState()
        {
            FakeClientSocket socket = CreateSocket();
            var auth = new AuthClient(socket);
            int signInCallbackCount = 0;
            int signedOutEventCount = 0;

            auth.OnSignedOutEvent += () => signedOutEventCount++;
            auth.SignInAsGuest((status, account, error) => signInCallbackCount++, socket);

            MstTestData.CompleteSealChallenge(socket);
            socket.RespondNext(MstOpCodes.SignIn, ResponseStatus.Success,
                MstTestData.CreateAccountPacketBytes(isGuest: true, token: "guest-test-token"));

            Assert.That(signInCallbackCount, Is.EqualTo(1));
            Assert.That(auth.Account, Is.Not.Null);
            Assert.That(auth.Account.IsGuest, Is.True);
            Assert.That(PlayerPrefs.GetString(MstParamKeys.USER_AUTH_TOKEN), Is.EqualTo("guest-test-token"));

            auth.SignOut(socket);

            Assert.That(auth.IsSignedIn, Is.False);
            Assert.That(PlayerPrefs.HasKey(MstParamKeys.USER_AUTH_TOKEN), Is.False);
            Assert.That(signedOutEventCount, Is.EqualTo(1));
            Assert.That(socket.SentMessages.Exists(message => message.OpCode == MstOpCodes.SignOut), Is.True);
        }

        [Test]
        public void SignInWithEmail_WhenCodeRequestSucceeds_DoesNotMarkClientAsSignedIn()
        {
            const string email = "player@example.com";
            FakeClientSocket socket = CreateSocket();
            var auth = new AuthClient(socket);
            int callbackCount = 0;
            string callbackError = null;
            bool callbackSucceeded = false;

            auth.SignInWithEmail(email, (isSuccessful, error) =>
            {
                callbackCount++;
                callbackSucceeded = isSuccessful;
                callbackError = error;
            }, socket);

            MstTestData.SealContext sealContext =
                MstTestData.CompleteSealChallenge(socket);
            FakeClientSocket.SentRequest request = socket.GetPendingRequest(MstOpCodes.SignIn);
            MstProperties credentials = MstProperties.FromBytes(
                MstTestData.OpenSealedRequest(socket, sealContext));

            Assert.That(credentials.AsString(MstParamKeys.USER_EMAIL), Is.EqualTo(email));
            Assert.That(credentials.Has(MstParamKeys.USER_EMAIL_SIGN_IN_CODE), Is.False);

            request.Respond(ResponseStatus.Success);

            Assert.That(callbackCount, Is.EqualTo(1));
            Assert.That(callbackSucceeded, Is.True);
            Assert.That(callbackError, Is.Null.Or.Empty);
            Assert.That(auth.IsNowSigningIn, Is.False);
            Assert.That(auth.IsSignedIn, Is.False);
        }

        [Test]
        public void ConfirmEmailSignIn_WhenSuccessful_SendsCodeAndMarksClientAsSignedIn()
        {
            const string email = "player@example.com";
            const string code = "123456";
            FakeClientSocket socket = CreateSocket();
            var auth = new AuthClient(socket);
            int callbackCount = 0;

            auth.ConfirmEmailSignIn(email, code, (status, account, error) => callbackCount++, socket);

            MstTestData.SealContext sealContext =
                MstTestData.CompleteSealChallenge(socket);
            FakeClientSocket.SentRequest request = socket.GetPendingRequest(MstOpCodes.SignIn);
            MstProperties credentials = MstProperties.FromBytes(
                MstTestData.OpenSealedRequest(socket, sealContext));

            Assert.That(credentials.AsString(MstParamKeys.USER_EMAIL), Is.EqualTo(email));
            Assert.That(
                credentials.AsString(MstParamKeys.USER_EMAIL_SIGN_IN_CODE),
                Is.EqualTo(code));

            request.Respond(
                ResponseStatus.Success,
                MstTestData.CreateAccountPacketBytes(username: "email-player"));

            Assert.That(callbackCount, Is.EqualTo(1));
            Assert.That(auth.IsNowSigningIn, Is.False);
            Assert.That(auth.IsSignedIn, Is.True);
            Assert.That(auth.Account.Username, Is.EqualTo("email-player"));
        }

        [Test]
        public void ConfirmEmailSignIn_WhenTemporarilyRejected_PreservesSavedToken()
        {
            const string savedToken = "saved-guest-token";
            FakeClientSocket socket = CreateSocket();
            var auth = new AuthClient(socket);
            PlayerPrefs.SetString(MstParamKeys.USER_AUTH_TOKEN, savedToken);

            auth.ConfirmEmailSignIn("player@example.com", "123456", (_, __, ___) => { }, socket);
            MstTestData.CompleteSealChallenge(socket);
            socket.RespondNext(MstOpCodes.SignIn, ResponseStatus.ServiceUnavailable);

            Assert.That(
                PlayerPrefs.GetString(MstParamKeys.USER_AUTH_TOKEN),
                Is.EqualTo(savedToken));
        }

        [Test]
        public void ChangePassword_WhenSuccessful_ClearsSavedToken()
        {
            const string savedToken = "saved-token-before-password-change";
            FakeClientSocket socket = CreateSocket();
            var auth = new AuthClient(socket);
            PlayerPrefs.SetString(MstParamKeys.USER_AUTH_TOKEN, savedToken);
            bool callbackSucceeded = false;

            auth.ChangePassword(
                "player@example.com",
                "123456",
                "new-secure-password",
                (success, _) => callbackSucceeded = success);
            socket.RespondNext(MstOpCodes.ChangePassword,
                ResponseStatus.Success);

            Assert.That(callbackSucceeded, Is.True);
            Assert.That(PlayerPrefs.HasKey(MstParamKeys.USER_AUTH_TOKEN),
                Is.False);
        }

        [Test]
        public void ChangePassword_WhenRejected_PreservesSavedToken()
        {
            const string savedToken = "saved-token-before-rejected-change";
            FakeClientSocket socket = CreateSocket();
            var auth = new AuthClient(socket);
            PlayerPrefs.SetString(MstParamKeys.USER_AUTH_TOKEN, savedToken);
            bool callbackSucceeded = true;

            auth.ChangePassword(
                "player@example.com",
                "123456",
                "new-secure-password",
                (success, _) => callbackSucceeded = success);
            socket.RespondNext(MstOpCodes.ChangePassword,
                ResponseStatus.Invalid);

            Assert.That(callbackSucceeded, Is.False);
            Assert.That(
                PlayerPrefs.GetString(MstParamKeys.USER_AUTH_TOKEN),
                Is.EqualTo(savedToken));
        }

        [Test]
        public void SignInWithToken_WhenNoTokenIsSaved_ReturnsTokenExpiredWithoutSending()
        {
            FakeClientSocket socket = CreateSocket();
            var auth = new AuthClient(socket);
            ResponseStatus callbackStatus = ResponseStatus.Success;
            AccountInfoPacket callbackAccount = null;

            auth.SignInWithToken((status, account, error) =>
            {
                callbackStatus = status;
                callbackAccount = account;
            }, socket);

            Assert.That(callbackStatus, Is.EqualTo(ResponseStatus.TokenExpired));
            Assert.That(callbackAccount, Is.Null);
            Assert.That(socket.Requests, Is.Empty);
            Assert.That(auth.IsNowSigningIn, Is.False);
        }

        [Test]
        public void SignInWithToken_WhenAccountIsBanned_ParsesStructuredErrorAndPreservesToken()
        {
            const string savedToken = "blocked-account-token";
            const string blockReason = "Test block reason";
            FakeClientSocket socket = CreateSocket();
            var auth = new AuthClient(socket);
            ResponseStatus callbackStatus = ResponseStatus.Success;
            AccountInfoPacket callbackAccount = null;
            string callbackError = null;
            PlayerPrefs.SetString(MstParamKeys.USER_AUTH_TOKEN, savedToken);

            auth.SignInWithToken(savedToken, (status, account, error) =>
            {
                callbackStatus = status;
                callbackAccount = account;
                callbackError = error;
            }, socket);
            MstTestData.CompleteSealChallenge(socket);
            var error = new MstProperties();
            error.Set(MstErrorPropertyKeys.CODE, MstErrorCodes.ACCOUNT_BLOCKED);
            error.Set(MstErrorPropertyKeys.REASON, blockReason);
            error.Set(MstErrorPropertyKeys.EXPIRES_AT, "2030-01-01T00:00:00.0000000Z");
            string expectedError = Mst.Errors.Parse(ResponseStatus.Banned, error);
            socket.RespondNext(MstOpCodes.SignIn, ResponseStatus.Banned, error.ToBytes());

            Assert.That(callbackStatus, Is.EqualTo(ResponseStatus.Banned));
            Assert.That(callbackAccount, Is.Null);
            Assert.That(callbackError, Is.EqualTo(expectedError));
            Assert.That(callbackError, Does.Contain(blockReason));
            Assert.That(PlayerPrefs.GetString(MstParamKeys.USER_AUTH_TOKEN), Is.EqualTo(savedToken));
            Assert.That(auth.IsSignedIn, Is.False);
        }

        [Test]
        public void SignInWithToken_WhenServerRejectsToken_ClearsSavedToken()
        {
            const string savedToken = "expired-token";
            FakeClientSocket socket = CreateSocket();
            var auth = new AuthClient(socket);
            PlayerPrefs.SetString(MstParamKeys.USER_AUTH_TOKEN, savedToken);

            auth.SignInWithToken(savedToken, (_, __, ___) => { }, socket);
            MstTestData.CompleteSealChallenge(socket);
            socket.RespondNext(MstOpCodes.SignIn, ResponseStatus.TokenExpired);

            Assert.That(PlayerPrefs.HasKey(MstParamKeys.USER_AUTH_TOKEN), Is.False);
        }

        [Test]
        public void SignInWithToken_WhenDifferentTokenIsRejected_PreservesSavedToken()
        {
            const string savedToken = "current-saved-token";
            const string attemptedToken = "explicit-foreign-token";
            FakeClientSocket socket = CreateSocket();
            var auth = new AuthClient(socket);
            PlayerPrefs.SetString(MstParamKeys.USER_AUTH_TOKEN, savedToken);

            auth.SignInWithToken(attemptedToken, (_, __, ___) => { }, socket);
            MstTestData.CompleteSealChallenge(socket);
            socket.RespondNext(MstOpCodes.SignIn, ResponseStatus.TokenExpired);

            Assert.That(
                PlayerPrefs.GetString(MstParamKeys.USER_AUTH_TOKEN),
                Is.EqualTo(savedToken));
        }

        [Test]
        public void SignInWithToken_WhenSuccessPayloadIsMalformed_PreservesSavedToken()
        {
            const string savedToken = "saved-token-before-malformed-response";
            FakeClientSocket socket = CreateSocket();
            var auth = new AuthClient(socket);
            PlayerPrefs.SetString(MstParamKeys.USER_AUTH_TOKEN, savedToken);

            auth.SignInWithToken(savedToken, (_, __, ___) => { }, socket);
            MstTestData.CompleteSealChallenge(socket);
            socket.RespondNext(
                MstOpCodes.SignIn,
                ResponseStatus.Success,
                new byte[] { 1, 2, 3 });

            Assert.That(
                PlayerPrefs.GetString(MstParamKeys.USER_AUTH_TOKEN),
                Is.EqualTo(savedToken));
        }

        [Test]
        public void SignInWithToken_WhenSavedTokenSucceeds_ReplacesItWithoutRememberMe()
        {
            const string savedToken = "saved-token-before-rotation";
            const string rotatedToken = "rotated-token-from-server";
            FakeClientSocket socket = CreateSocket();
            var auth = new AuthClient(socket);
            PlayerPrefs.SetString(MstParamKeys.USER_AUTH_TOKEN, savedToken);

            auth.SignInWithToken(savedToken, (_, __, ___) => { }, socket);
            MstTestData.CompleteSealChallenge(socket);
            socket.RespondNext(
                MstOpCodes.SignIn,
                ResponseStatus.Success,
                MstTestData.CreateAccountPacketBytes(
                    isGuest: false,
                    token: rotatedToken));

            Assert.That(auth.RememberMe, Is.False);
            Assert.That(
                PlayerPrefs.GetString(MstParamKeys.USER_AUTH_TOKEN),
                Is.EqualTo(rotatedToken));
        }

        [Test]
        public void SignInWithToken_WhenExplicitDifferentTokenSucceeds_PreservesSavedTokenWithoutRememberMe()
        {
            const string savedToken = "existing-saved-token";
            const string explicitToken = "explicit-token";
            const string rotatedToken = "rotated-explicit-token";
            FakeClientSocket socket = CreateSocket();
            var auth = new AuthClient(socket);
            PlayerPrefs.SetString(MstParamKeys.USER_AUTH_TOKEN, savedToken);

            auth.SignInWithToken(explicitToken, (_, __, ___) => { }, socket);
            MstTestData.CompleteSealChallenge(socket);
            socket.RespondNext(
                MstOpCodes.SignIn,
                ResponseStatus.Success,
                MstTestData.CreateAccountPacketBytes(
                    isGuest: false,
                    token: rotatedToken));

            Assert.That(auth.RememberMe, Is.False);
            Assert.That(
                PlayerPrefs.GetString(MstParamKeys.USER_AUTH_TOKEN),
                Is.EqualTo(savedToken));
        }

        [Test]
        public void SignIn_WhenSynchronousCompletionThenSendThrows_DoesNotCompleteTwice()
        {
            FakeClientSocket socket = CreateSocket();
            var auth = new AuthClient(socket);
            int callbackCount = 0;
            byte[] accountBytes = MstTestData.CreateAccountPacketBytes();

            auth.SignIn(new MstProperties(), (status, account, error) => callbackCount++, socket);
            socket.OnRequestSent = request =>
            {
                if (request.Message.OpCode != MstOpCodes.SignIn)
                    return;

                request.Respond(ResponseStatus.Success, accountBytes);
                throw new InvalidOperationException("Synthetic post-callback send failure");
            };

            MstTestData.CompleteSealChallenge(socket);

            Assert.That(callbackCount, Is.EqualTo(1));
            Assert.That(auth.IsNowSigningIn, Is.False);
            Assert.That(auth.IsSignedIn, Is.True);
        }

        [Test]
        public void SignUp_WhenCredentialsExceedAuthLimit_CompletesOnceWithoutSendingCredentials()
        {
            FakeClientSocket socket = CreateSocket();
            var auth = new AuthClient(socket);
            var credentials = new MstProperties();
            string largeValue = new string('a', short.MaxValue - 128);
            int callbackCount = 0;
            AccountInfoPacket callbackAccount = null;
            string callbackError = null;
            ResponseStatus callbackStatus = ResponseStatus.Success;

            for (int i = 0; i < 9; i++)
                credentials.Add($"large-{i}", largeValue);

            bool previousIgnoreFailingMessages = UnityEngine.TestTools.LogAssert.ignoreFailingMessages;
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;

            try
            {
                auth.SignUp(credentials, (status, account, error) =>
                {
                    callbackCount++;
                    callbackStatus = status;
                    callbackAccount = account;
                    callbackError = error;
                }, socket);
            }
            finally
            {
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }

            Assert.That(callbackCount, Is.EqualTo(1));
            Assert.That(callbackStatus, Is.EqualTo(ResponseStatus.Error));
            Assert.That(callbackAccount, Is.Null);
            Assert.That(callbackError, Is.Not.Null.And.Not.Empty);
            Assert.That(
                socket.Requests.FindAll(request => request.Message.OpCode == MstOpCodes.SignUp),
                Is.Empty);
            Assert.That(
                socket.Requests.FindAll(
                    request => request.Message.OpCode == MstOpCodes.SealChallengeRequest),
                Is.Empty);
        }

        [Test]
        public void SignUp_FromGuestSession_PreservesAccountAndReplacesSavedToken()
        {
            const string accountId = "guest-upgrade-account";
            const string guestToken = "guest-token-before-registration";
            const string registeredToken = "registered-token-after-registration";
            FakeClientSocket socket = CreateSocket();
            var auth = new AuthClient(socket);
            AccountInfoPacket callbackAccount = null;
            var eventOrder = new List<string>();

            auth.SignInAsGuest((_, __, ___) => { }, socket);
            MstTestData.CompleteSealChallenge(socket);
            socket.RespondNext(
                MstOpCodes.SignIn,
                ResponseStatus.Success,
                MstTestData.CreateAccountPacketBytes(
                    id: accountId,
                    username: "guest-before-registration",
                    isGuest: true,
                    token: guestToken));

            auth.OnSignedUpEvent += () => eventOrder.Add("signed-up");
            auth.OnSignedInEvent += () => eventOrder.Add("signed-in");
            auth.SignUp(new MstProperties(), (_, account, __) =>
            {
                callbackAccount = account;
                eventOrder.Add("callback");
            }, socket);

            MstTestData.CompleteSealChallenge(socket);
            socket.RespondNext(
                MstOpCodes.SignUp,
                ResponseStatus.Success,
                MstTestData.CreateAccountPacketBytes(
                    id: accountId,
                    username: "registered-player",
                    isGuest: false,
                    token: registeredToken));

            Assert.That(callbackAccount, Is.SameAs(auth.Account));
            Assert.That(auth.Account.Id, Is.EqualTo(accountId));
            Assert.That(auth.Account.IsGuest, Is.False);
            Assert.That(auth.Account.Username, Is.EqualTo("registered-player"));
            Assert.That(
                PlayerPrefs.GetString(MstParamKeys.USER_AUTH_TOKEN),
                Is.EqualTo(registeredToken));
            Assert.That(eventOrder, Is.EqualTo(new[] { "callback", "signed-up", "signed-in" }));
        }

        [Test]
        public void SignUp_FromUnauthenticatedSession_SetsAuthenticatedAccount()
        {
            FakeClientSocket socket = CreateSocket();
            var auth = new AuthClient(socket);
            AccountInfoPacket callbackAccount = null;

            auth.SignUp(new MstProperties(), (_, account, __) => callbackAccount = account, socket);
            MstTestData.CompleteSealChallenge(socket);
            socket.RespondNext(
                MstOpCodes.SignUp,
                ResponseStatus.Success,
                MstTestData.CreateAccountPacketBytes(
                    id: "new-account",
                    username: "new-user",
                    isGuest: false,
                    token: "session-token"));

            Assert.That(callbackAccount, Is.SameAs(auth.Account));
            Assert.That(auth.IsSignedIn, Is.True);
            Assert.That(auth.Account.Id, Is.EqualTo("new-account"));
            Assert.That(auth.Account.IsGuest, Is.False);
            Assert.That(PlayerPrefs.HasKey(MstParamKeys.USER_AUTH_TOKEN), Is.False);
        }

        [Test]
        public void SignUp_FromRegularSession_IsRejectedWithoutSending()
        {
            FakeClientSocket socket = CreateSocket();
            var auth = new AuthClient(socket);
            int callbackCount = 0;
            AccountInfoPacket callbackAccount = null;
            ResponseStatus callbackStatus = ResponseStatus.Success;

            auth.SignIn(new MstProperties(), (_, __, ___) => { }, socket);
            MstTestData.CompleteSealChallenge(socket);
            socket.RespondNext(
                MstOpCodes.SignIn,
                ResponseStatus.Success,
                MstTestData.CreateAccountPacketBytes(isGuest: false));

            auth.SignUp(new MstProperties(), (status, account, error) =>
            {
                callbackCount++;
                callbackStatus = status;
                callbackAccount = account;
            }, socket);

            Assert.That(callbackCount, Is.EqualTo(1));
            Assert.That(callbackStatus, Is.EqualTo(ResponseStatus.Conflict));
            Assert.That(callbackAccount, Is.Null);
            Assert.That(auth.IsNowSigningIn, Is.False);
            Assert.That(
                socket.Requests.FindAll(request => request.Message.OpCode == MstOpCodes.SignUp),
                Is.Empty);
        }

        private FakeClientSocket CreateSocket(bool isConnected = true)
        {
            var socket = new FakeClientSocket(isConnected);
            sockets.Add(socket);
            return socket;
        }
    }
}
