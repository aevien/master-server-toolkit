using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using NUnit.Framework;

namespace MasterServerToolkit.Tests.EditMode
{
    public class MstErrorParserTests
    {
        [Test]
        public void Parse_WhenStatusIsSuccess_ReturnsEmptyMessage()
        {
            var parser = CreateParser();

            string message = parser.Parse(ResponseStatus.Success);

            Assert.That(message, Is.Empty);
        }

        [Test]
        public void Parse_WhenResponseHasRegisteredCode_ReturnsLocalizedCodeMessage()
        {
            var parser = CreateParser();
            parser.Register(MstErrorCodes.INVALID_EMAIL);
            var properties = new MstProperties();
            properties.Set(MstErrorPropertyKeys.CODE, MstErrorCodes.INVALID_EMAIL);
            var response = new IncomingMessage(
                1,
                0,
                properties.ToBytes(),
                DeliveryMethod.Reliable,
                null);

            string message = parser.Parse(ResponseStatus.Invalid, response);

            Assert.That(message, Is.EqualTo("localized:ui.error.auth.invalid_email.message"));
        }

        [Test]
        public void Parse_WhenPayloadIsMalformed_UsesStatusFallback()
        {
            var parser = CreateParser();
            var response = new IncomingMessage(
                1,
                0,
                new byte[] { 1, 2, 3 },
                DeliveryMethod.Reliable,
                null);

            string message = parser.Parse(ResponseStatus.BadRequest, response);

            Assert.That(message, Is.EqualTo("localized:ui.error.response.badRequest.message"));
        }

        [Test]
        public void Parse_WhenSerializedPropertiesComeFromAnotherTransport_ReturnsLocalizedMessage()
        {
            var parser = CreateParser();
            parser.Register(MstErrorCodes.INVALID_EMAIL);
            var properties = new MstProperties();
            properties.Set(MstErrorPropertyKeys.CODE, MstErrorCodes.INVALID_EMAIL);

            string message = parser.Parse(ResponseStatus.Invalid, properties.ToBytes());

            Assert.That(message, Is.EqualTo("localized:ui.error.auth.invalid_email.message"));
        }

        [Test]
        public void Parse_WhenResponseHasUnknownCode_UsesStatusFallback()
        {
            var parser = CreateParser();
            var properties = new MstProperties();
            properties.Set(MstErrorPropertyKeys.CODE, "test.unknown_code");

            string message = parser.Parse(ResponseStatus.NotFound, properties);

            Assert.That(message, Is.EqualTo("localized:ui.error.response.notFound.message"));
        }

        [Test]
        public void Parse_WhenTimeoutHasStructuredCode_UsesRegisteredCode()
        {
            var parser = CreateParser();
            parser.Register("test.timeout");
            var properties = new MstProperties();
            properties.Set(MstErrorPropertyKeys.CODE, "test.timeout");
            var response = new IncomingMessage(
                1,
                0,
                properties.ToBytes(),
                DeliveryMethod.Reliable,
                null);

            string message = parser.Parse(ResponseStatus.Timeout, response);

            Assert.That(message, Is.EqualTo("localized:ui.error.test.timeout.message"));
        }

        [Test]
        public void Parse_WhenRegisteredCodeHasNoTranslation_UsesStatusFallback()
        {
            var parser = new MstErrorParser(key =>
                key == "ui.error.test.not_translated.message" ? key : $"localized:{key}");
            parser.Register("test.not_translated");
            var properties = new MstProperties();
            properties.Set(MstErrorPropertyKeys.CODE, "test.not_translated");

            string message = parser.Parse(ResponseStatus.Invalid, properties);

            Assert.That(message, Is.EqualTo("localized:ui.error.response.invalid.message"));
        }

        [Test]
        public void Parse_WhenFormattedCodeHasNoTranslation_UsesStatusFallback()
        {
            var parser = new MstErrorParser(key =>
                key == "ui.error.test.formatted.message" ? key : $"localized:{key}");
            parser.Register("test.formatted", properties =>
                parser.LocalizeFormat("ui.error.test.formatted.message",
                    properties.AsInt(MstErrorPropertyKeys.COUNT)));
            var properties = new MstProperties();
            properties.Set(MstErrorPropertyKeys.CODE, "test.formatted");
            properties.Set(MstErrorPropertyKeys.COUNT, 2);

            string message = parser.Parse(ResponseStatus.Invalid, properties);

            Assert.That(message, Is.EqualTo("localized:ui.error.response.invalid.message"));
        }

        [Test]
        public void Parse_WhenRegisteredFormatterUsesProperties_ReturnsFormattedMessage()
        {
            var parser = CreateParser();
            parser.Register("test.parameterized", properties =>
                $"{properties.AsString(MstErrorPropertyKeys.REASON)}|" +
                properties.AsString(MstErrorPropertyKeys.EXPIRES_AT));
            var error = new MstProperties();
            error.Set(MstErrorPropertyKeys.CODE, "test.parameterized");
            error.Set(MstErrorPropertyKeys.REASON, "reason");
            error.Set(MstErrorPropertyKeys.EXPIRES_AT, "2030-01-02T03:04:05.0000000Z");

            string message = parser.Parse(ResponseStatus.Banned, error);

            Assert.That(message, Is.EqualTo("reason|2030-01-02T03:04:05.0000000Z"));
        }

        [Test]
        public void Parse_WhenResourceIsInsufficient_IncludesResourceName()
        {
            var parser = new MstErrorParser(key =>
                key == "ui.error.common.resource_insufficient.message"
                    ? "Not enough resource: {0}"
                    : $"localized:{key}");
            var properties = new MstProperties();
            properties.Set(MstErrorPropertyKeys.CODE, MstErrorCodes.RESOURCE_INSUFFICIENT);
            properties.Set(MstErrorPropertyKeys.RESOURCE, "gold");

            string message = parser.Parse(ResponseStatus.Conflict, properties);

            Assert.That(message, Is.EqualTo("Not enough resource: gold"));
        }

        [Test]
        public void TryRegister_WhenCodeAlreadyExists_PreservesFirstParser()
        {
            var parser = CreateParser();
            Assert.That(parser.TryRegister("test.shared", _ => "first"), Is.True);
            Assert.That(parser.TryRegister("test.shared", _ => "second"), Is.False);
            var properties = new MstProperties();
            properties.Set(MstErrorPropertyKeys.CODE, "test.shared");

            string message = parser.Parse(ResponseStatus.Error, properties);

            Assert.That(message, Is.EqualTo("first"));
        }

        [TestCase(ResponseStatus.Timeout, "ui.error.response.timeout.message")]
        [TestCase(ResponseStatus.NotConnected, "ui.status.connection.notConnected")]
        [TestCase(ResponseStatus.Error, "ui.error.response.internal.message")]
        [TestCase(ResponseStatus.DependencyError, "ui.error.response.dependency.message")]
        [TestCase(ResponseStatus.ServiceUnavailable, "ui.error.response.unavailable.message")]
        [TestCase(ResponseStatus.Unhandled, "ui.notification.error.unknown.message")]
        [TestCase(ResponseStatus.Unauthorized, "ui.status.auth.unauthorized")]
        [TestCase(ResponseStatus.Forbidden, "ui.error.response.forbidden.message")]
        [TestCase(ResponseStatus.TokenExpired, "ui.notification.signIn.tokenExpired.message")]
        [TestCase(ResponseStatus.Banned, "ui.error.response.banned.message")]
        [TestCase(ResponseStatus.DuplicateLogin, "ui.notification.signIn.alreadySignedIn.message")]
        [TestCase(ResponseStatus.BadRequest, "ui.error.response.badRequest.message")]
        [TestCase(ResponseStatus.Invalid, "ui.error.response.invalid.message")]
        [TestCase(ResponseStatus.NotFound, "ui.error.response.notFound.message")]
        [TestCase(ResponseStatus.AlreadyExists, "ui.error.response.alreadyExists.message")]
        [TestCase(ResponseStatus.Conflict, "ui.error.response.conflict.message")]
        [TestCase(ResponseStatus.Cancelled, "ui.error.response.cancelled.message")]
        public void Parse_WhenBodyIsMissing_UsesLocalizedStatusFallback(
            ResponseStatus status,
            string expectedLocalizationKey)
        {
            var parser = CreateParser();

            string message = parser.Parse(status);

            Assert.That(message, Is.EqualTo($"localized:{expectedLocalizationKey}"));
        }

        private static MstErrorParser CreateParser()
        {
            return new MstErrorParser(key => $"localized:{key}");
        }
    }
}
