using MasterServerToolkit.GameService;
using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace MasterServerToolkit.Tests.EditMode
{
    [TestFixture]
    [Parallelizable(ParallelScope.None)]
    public class EditorPlayerModuleTests
    {
        private const string GuestPlayerInfoKey = "playerGuestInfoKey";

        private readonly List<GameObject> serviceObjects = new();
        private bool hadAuthToken;
        private string originalAuthToken;
        private bool hadGuestPlayerInfo;
        private string originalGuestPlayerInfo;

        [SetUp]
        public void SetUp()
        {
            hadAuthToken = PlayerPrefs.HasKey(MstParamKeys.USER_AUTH_TOKEN);
            originalAuthToken = hadAuthToken
                ? PlayerPrefs.GetString(MstParamKeys.USER_AUTH_TOKEN)
                : string.Empty;
            hadGuestPlayerInfo = PlayerPrefs.HasKey(GuestPlayerInfoKey);
            originalGuestPlayerInfo = hadGuestPlayerInfo
                ? PlayerPrefs.GetString(GuestPlayerInfoKey)
                : string.Empty;

            PlayerPrefs.DeleteKey(MstParamKeys.USER_AUTH_TOKEN);
            PlayerPrefs.DeleteKey(GuestPlayerInfoKey);
            PlayerPrefs.Save();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject serviceObject in serviceObjects)
            {
                if (serviceObject != null)
                    Object.DestroyImmediate(serviceObject);
            }

            if (hadAuthToken)
                PlayerPrefs.SetString(MstParamKeys.USER_AUTH_TOKEN, originalAuthToken);
            else
                PlayerPrefs.DeleteKey(MstParamKeys.USER_AUTH_TOKEN);

            if (hadGuestPlayerInfo)
                PlayerPrefs.SetString(GuestPlayerInfoKey, originalGuestPlayerInfo);
            else
                PlayerPrefs.DeleteKey(GuestPlayerInfoKey);

            PlayerPrefs.Save();
            serviceObjects.Clear();
        }

        [Test]
        public void StartAsGuest_ClearsSavedMstAuthenticationToken()
        {
            PlayerPrefs.SetString(MstParamKeys.USER_AUTH_TOKEN, "saved-editor-account-token");
            PlayerPrefs.Save();

            CreatePlayer(new EditorSdkSettings { startAsGuest = true });

            Assert.That(PlayerPrefs.HasKey(MstParamKeys.USER_AUTH_TOKEN), Is.False);
        }

        [Test]
        public void StartAsAuthenticatedPlayer_PreservesSavedMstAuthenticationToken()
        {
            const string savedToken = "saved-editor-account-token";
            PlayerPrefs.SetString(MstParamKeys.USER_AUTH_TOKEN, savedToken);
            PlayerPrefs.Save();

            CreatePlayer(new EditorSdkSettings { startAsGuest = false });

            Assert.That(
                PlayerPrefs.GetString(MstParamKeys.USER_AUTH_TOKEN),
                Is.EqualTo(savedToken));
        }

        [Test]
        public void GuestIdentity_IsRestoredAcrossServiceInstances()
        {
            IPlayerModule firstPlayer = CreatePlayer(new EditorSdkSettings { startAsGuest = true });
            string firstId = firstPlayer.Id;

            IPlayerModule secondPlayer = CreatePlayer(new EditorSdkSettings { startAsGuest = true });

            Assert.That(firstId, Is.Not.Empty);
            Assert.That(firstPlayer.Name, Is.Empty);
            Assert.That(secondPlayer.IsGuest, Is.True);
            Assert.That(secondPlayer.Id, Is.EqualTo(firstId));
            Assert.That(secondPlayer.Name, Is.Empty);
        }

        [Test]
        public void GuestIdentity_OverridesInspectorAndPreviouslyStoredName()
        {
            var storedGuest = MstJson.CreateObject();
            storedGuest.AddField("id", "persisted-editor-guest");
            storedGuest.AddField("name", "Old Stored Name");
            PlayerPrefs.SetString(GuestPlayerInfoKey, storedGuest.ToString());
            PlayerPrefs.Save();

            IPlayerModule player = CreatePlayer(new EditorSdkSettings
            {
                startAsGuest = true,
                userId = "configured-authenticated-player",
                authenticatedDisplayName = "Configured Authenticated Name"
            });

            Assert.That(player.IsGuest, Is.True);
            Assert.That(player.Id, Is.EqualTo("persisted-editor-guest"));
            Assert.That(player.Name, Is.Empty);
        }

        [Test]
        public void GuestIdentity_CanDisableInteractiveAuthentication()
        {
            IPlayerModule player = CreatePlayer(new EditorSdkSettings
            {
                startAsGuest = true,
                interactiveAuthenticationSupported = false
            });

            Assert.That(player.IsGuest, Is.True);
            Assert.That(player.IsAuthenticationSupported, Is.False);
        }

        [Test]
        public void LegacyOptions_KeepInteractiveAuthenticationEnabled()
        {
            MstJson options = new EditorSdkSettings { startAsGuest = true }.ToJson();
            options.RemoveField(nameof(EditorSdkSettings.interactiveAuthenticationSupported));

            IPlayerModule player = CreatePlayer(options);

            Assert.That(player.IsAuthenticationSupported, Is.True);
        }

        [Test]
        public void Authenticate_WhenInteractiveAuthenticationIsDisabled_FailsWithoutReplacingGuest()
        {
            IPlayerModule player = CreatePlayer(new EditorSdkSettings
            {
                startAsGuest = true,
                interactiveAuthenticationSupported = false,
                userId = "must-not-be-applied",
                authenticatedDisplayName = "Must Not Be Applied"
            });
            string guestId = player.Id;
            bool? callbackResult = null;
            string callbackError = string.Empty;

            player.Authenticate((isSuccess, error) =>
            {
                callbackResult = isSuccess;
                callbackError = error;
            });

            Assert.That(callbackResult, Is.False);
            Assert.That(callbackError, Is.Not.Empty);
            Assert.That(player.IsGuest, Is.True);
            Assert.That(player.Id, Is.EqualTo(guestId));
            Assert.That(player.Name, Is.Empty);
        }

        [Test]
        public void AuthenticatedIdentity_UsesInspectorValues()
        {
            const string expectedId = "editor-authenticated-player";
            const string expectedName = "Editor Authenticated Player";

            CreatePlayer(new EditorSdkSettings { startAsGuest = true });
            IPlayerModule player = CreatePlayer(new EditorSdkSettings
            {
                startAsGuest = false,
                userId = expectedId,
                authenticatedDisplayName = expectedName
            });

            Assert.That(player.IsGuest, Is.False);
            Assert.That(player.Id, Is.EqualTo(expectedId));
            Assert.That(player.Name, Is.EqualTo(expectedName));
        }

        [Test]
        public void AuthenticatedIdentity_AllowsEmptyDisplayName()
        {
            IPlayerModule player = CreatePlayer(new EditorSdkSettings
            {
                startAsGuest = false,
                userId = "editor-player-with-hidden-name",
                authenticatedDisplayName = string.Empty
            });

            Assert.That(player.IsGuest, Is.False);
            Assert.That(player.Id, Is.EqualTo("editor-player-with-hidden-name"));
            Assert.That(player.Name, Is.Empty);
        }

        [UnityTest]
        public IEnumerator Authenticate_GuestIdentity_UsesInspectorValuesBeforeCallback()
        {
            const string expectedId = "editor-authenticated-after-callback";
            const string expectedName = "Authenticated After Callback";
            IPlayerModule player = CreatePlayer(new EditorSdkSettings
            {
                startAsGuest = true,
                userId = expectedId,
                authenticatedDisplayName = expectedName
            });
            bool callbackInvoked = false;
            string callbackPlayerId = string.Empty;
            string callbackPlayerName = string.Empty;
            bool callbackPlayerIsGuest = true;

            player.Service.Storage.OnInit(player.Service);

            player.Authenticate((isSuccess, error) =>
            {
                callbackInvoked = isSuccess;
                callbackPlayerId = player.Id;
                callbackPlayerName = player.Name;
                callbackPlayerIsGuest = player.IsGuest;
            });

            float timeoutAt = Time.realtimeSinceStartup + 2f;

            while (!callbackInvoked && Time.realtimeSinceStartup < timeoutAt)
            {
                EditorApplication.QueuePlayerLoopUpdate();
                yield return null;
            }

            Assert.That(callbackInvoked, Is.True);
            Assert.That(callbackPlayerId, Is.EqualTo(expectedId));
            Assert.That(callbackPlayerName, Is.EqualTo(expectedName));
            Assert.That(callbackPlayerIsGuest, Is.False);
        }

        private IPlayerModule CreatePlayer(EditorSdkSettings settings)
        {
            return CreatePlayer(settings.ToJson());
        }

        private IPlayerModule CreatePlayer(MstJson options)
        {
            var serviceObject = new GameObject($"EditorPlayerTest-{serviceObjects.Count}");
            serviceObjects.Add(serviceObject);

            var service = serviceObject.AddComponent<EditorService>();
            service.Logger = Mst.Create.Logger(nameof(EditorPlayerModuleTests));
            service.OnBeforeInit(options);

            var player = (EditorPlayerModule)service.Player;
            player.OnInit(service);
            return player;
        }
    }
}
