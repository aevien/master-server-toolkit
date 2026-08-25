using MasterServerToolkit.GameService;
using NUnit.Framework;

namespace MasterServerToolkit.Tests.EditMode
{
    [TestFixture]
    public class VkGamesPlayerValidatorTests
    {
        private const string Secret = "vk-games-test-secret";
        private const string AppId = "123456";
        private const string UserId = "987654321";
        private const string AuthKey = "21d20a747f37b7f4d8a20ba712e060b5";
        private const string Signature = "Wtb0x_naGzao4D-FCllC1XkGo0GR9CASOH-_kNwGZyQ";
        private const string SignKeys =
            "api_url,api_id,viewer_id,auth_key,timestamp,language,platform,referrer";
        private const string Query =
            "?api_url=https%3A%2F%2Fapi.vk.ru%2Fapi.php" +
            "&api_id=" + AppId +
            "&viewer_id=" + UserId +
            "&auth_key=" + AuthKey +
            "&timestamp=1700000000" +
            "&language=0" +
            "&platform=web" +
            "&referrer=campaign+summer%2F2026" +
            "&sign=" + Signature +
            "&sign_keys=" + SignKeys;

        [Test]
        public void TryValidate_KnownVkGamesVector_ReturnsTrue()
        {
            bool valid = VkGamesPlayerValidator.TryValidate(
                Secret, AppId, UserId, Query, out string error);

            Assert.That(valid, Is.True, error);
        }

        [Test]
        public void TryValidate_ReorderedParameters_ReturnsTrue()
        {
            string reordered =
                "?sign_keys=" + SignKeys +
                "&referrer=campaign+summer%2F2026" +
                "&platform=web&language=0&timestamp=1700000000" +
                "&auth_key=" + AuthKey +
                "&viewer_id=" + UserId +
                "&api_id=" + AppId +
                "&api_url=https%3A%2F%2Fapi.vk.ru%2Fapi.php" +
                "&sign=" + Signature;

            bool valid = VkGamesPlayerValidator.TryValidate(
                Secret, AppId, UserId, reordered, out string error);

            Assert.That(valid, Is.True, error);
        }

        [Test]
        public void TryValidate_EquivalentSpaceEncoding_ReturnsTrue()
        {
            string percentEncodedSpace = Query.Replace("campaign+summer", "campaign%20summer");

            bool valid = VkGamesPlayerValidator.TryValidate(
                Secret, AppId, UserId, percentEncodedSpace, out string error);

            Assert.That(valid, Is.True, error);
        }

        [TestCase("wrong-secret", AppId, Query, "signature_mismatch")]
        [TestCase(Secret, "654321", Query, "app_id_mismatch")]
        [TestCase(Secret, AppId, Query + "&viewer_id=1", "duplicate_parameter")]
        public void TryValidate_InvalidInput_FailsClosed(
            string secret,
            string appId,
            string query,
            string expectedError)
        {
            bool valid = VkGamesPlayerValidator.TryValidate(
                secret, appId, UserId, query, out string error);

            Assert.That(valid, Is.False);
            Assert.That(error, Is.EqualTo(expectedError));
        }

        [Test]
        public void TryValidate_TamperedUserId_RejectsSignature()
        {
            string tampered = Query.Replace(UserId, "987654322");

            bool valid = VkGamesPlayerValidator.TryValidate(
                Secret, AppId, UserId, tampered, out string error);

            Assert.That(valid, Is.False);
            Assert.That(error, Is.EqualTo("signature_mismatch"));
        }

        [Test]
        public void TryValidate_SignedButIncorrectAuthKey_RejectsRequest()
        {
            string tampered = Query
                .Replace(AuthKey, "11d20a747f37b7f4d8a20ba712e060b5")
                .Replace(Signature, "n2Txd3ywVgDSazKbvTwFSfVI7xdOHzpSHue1A6CqFiI");

            bool valid = VkGamesPlayerValidator.TryValidate(
                Secret, AppId, UserId, tampered, out string error);

            Assert.That(valid, Is.False);
            Assert.That(error, Is.EqualTo("auth_key_mismatch"));
        }

        [Test]
        public void TryValidate_TamperedSignedLaunchValue_RejectsSignature()
        {
            string tampered = Query.Replace("&language=0", "&language=3");

            bool valid = VkGamesPlayerValidator.TryValidate(
                Secret, AppId, UserId, tampered, out string error);

            Assert.That(valid, Is.False);
            Assert.That(error, Is.EqualTo("signature_mismatch"));
        }

        [Test]
        public void TryValidate_NonVkApiHost_RejectsRequest()
        {
            string tampered = Query.Replace("api.vk.ru", "example.com");

            bool valid = VkGamesPlayerValidator.TryValidate(
                Secret, AppId, UserId, tampered, out string error);

            Assert.That(valid, Is.False);
            Assert.That(error, Is.EqualTo("api_url_invalid"));
        }

        [Test]
        public void TryValidate_InvalidPercentEncoding_RejectsRequest()
        {
            string malformed = Query.Replace("campaign+summer", "campaign%2Gsummer");

            bool valid = VkGamesPlayerValidator.TryValidate(
                Secret, AppId, UserId, malformed, out string error);

            Assert.That(valid, Is.False);
            Assert.That(error, Is.EqualTo("query_encoding_invalid"));
        }

        [Test]
        public void TryValidate_MissingSignedParameter_RejectsRequest()
        {
            string missing = Query.Replace("&language=0", string.Empty);

            bool valid = VkGamesPlayerValidator.TryValidate(
                Secret, AppId, UserId, missing, out string error);

            Assert.That(valid, Is.False);
            Assert.That(error, Is.EqualTo("signed_parameter_missing"));
        }

        [Test]
        public void TryValidate_SignatureWithoutIdentityFields_RejectsRequest()
        {
            string incomplete = Query
                .Replace(Signature, "zGo2nqPLLElmiC8iwLx3Wb0kxRlN_W1Z39_KeeeypZQ")
                .Replace(SignKeys, "api_id");

            bool valid = VkGamesPlayerValidator.TryValidate(
                Secret, AppId, UserId, incomplete, out string error);

            Assert.That(valid, Is.False);
            Assert.That(error, Is.EqualTo("signed_identity_missing"));
        }

        [Test]
        public void TryValidate_SignedPlayerDoesNotMatchBridgePlayer_RejectsRequest()
        {
            bool valid = VkGamesPlayerValidator.TryValidate(
                Secret, AppId, "987654322", Query, out string error);

            Assert.That(valid, Is.False);
            Assert.That(error, Is.EqualTo("player_id_mismatch"));
        }
    }
}
