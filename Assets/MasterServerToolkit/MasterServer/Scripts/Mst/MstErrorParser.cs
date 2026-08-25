using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Converts structured MST error responses into localized client-facing messages.
    /// </summary>
    public sealed class MstErrorParser
    {
        private readonly object registryLock = new object();
        private readonly Dictionary<string, Func<MstProperties, string>> parsers =
            new Dictionary<string, Func<MstProperties, string>>(StringComparer.Ordinal);
        private readonly Dictionary<ResponseStatus, string> statusLocalizationKeys =
            new Dictionary<ResponseStatus, string>();
        private readonly Func<string, string> localizationProvider;

        public MstErrorParser(Func<string, string> localizationProvider = null)
        {
            this.localizationProvider = localizationProvider ?? LocalizeWithMst;
            RegisterStatusLocalizationKeys();
            RegisterCommonParsers();
        }

        /// <summary>
        /// Registers a code that uses the conventional ui.error.&lt;code&gt;.message localization key.
        /// </summary>
        public void Register(string code)
        {
            Register(code, _ => LocalizeErrorCode(code));
        }

        /// <summary>
        /// Registers a stateless formatter for a stable MST error code.
        /// </summary>
        public void Register(string code, Func<MstProperties, string> parser)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Error code cannot be empty", nameof(code));

            if (parser == null)
                throw new ArgumentNullException(nameof(parser));

            lock (registryLock)
            {
                if (parsers.ContainsKey(code))
                    throw new InvalidOperationException($"Error parser for code '{code}' is already registered");

                parsers.Add(code, parser);
            }
        }

        /// <summary>
        /// Registers an error code that resolves directly to a localization key.
        /// </summary>
        public void Register(string code, string localizationKey)
        {
            if (string.IsNullOrWhiteSpace(localizationKey))
                throw new ArgumentException("Localization key cannot be empty", nameof(localizationKey));

            Register(code, _ => LocalizeOptional(localizationKey));
        }

        /// <summary>
        /// Registers a formatter unless another module already owns the same code.
        /// </summary>
        public bool TryRegister(string code, Func<MstProperties, string> parser)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Error code cannot be empty", nameof(code));

            if (parser == null)
                throw new ArgumentNullException(nameof(parser));

            lock (registryLock)
            {
                if (parsers.ContainsKey(code))
                    return false;

                parsers.Add(code, parser);
                return true;
            }
        }

        /// <summary>
        /// Registers a direct localization key unless another module already owns the code.
        /// </summary>
        public bool TryRegister(string code, string localizationKey)
        {
            if (string.IsNullOrWhiteSpace(localizationKey))
                throw new ArgumentException("Localization key cannot be empty", nameof(localizationKey));

            return TryRegister(code, _ => LocalizeOptional(localizationKey));
        }

        /// <summary>
        /// Registers a conventional localized formatter unless it is already registered.
        /// </summary>
        public bool TryRegister(string code)
        {
            return TryRegister(code, _ => LocalizeErrorCode(code));
        }

        /// <summary>
        /// Returns a localized fallback for a local or body-less failure status.
        /// </summary>
        public string Parse(ResponseStatus status)
        {
            return Parse(status, (MstProperties)null);
        }

        /// <summary>
        /// Parses a response body and returns a localized error message.
        /// </summary>
        public string Parse(ResponseStatus status, IIncomingMessage response)
        {
            if (status == ResponseStatus.Success)
                return string.Empty;

            MstProperties properties = TryReadProperties(response);
            return Parse(status, properties);
        }

        /// <summary>
        /// Parses serialized error properties received through a transport other than MST sockets.
        /// </summary>
        public string Parse(ResponseStatus status, byte[] responseData)
        {
            if (status == ResponseStatus.Success)
                return string.Empty;

            return Parse(status, TryReadProperties(responseData));
        }

        /// <summary>
        /// Returns a localized error message from already decoded properties.
        /// </summary>
        public string Parse(ResponseStatus status, MstProperties properties)
        {
            if (status == ResponseStatus.Success)
                return string.Empty;

            string code = properties?.AsString(MstErrorPropertyKeys.CODE);

            if (!string.IsNullOrWhiteSpace(code))
            {
                Func<MstProperties, string> parser;

                lock (registryLock)
                {
                    parsers.TryGetValue(code, out parser);
                }

                if (parser != null)
                {
                    try
                    {
                        string message = parser(properties);

                        if (!string.IsNullOrWhiteSpace(message))
                            return message;
                    }
                    catch (Exception exception)
                    {
                        Logs.Error($"Failed to parse MST error '{code}': {exception}");
                    }
                }
                else
                {
                    Logs.Warn($"MST error parser is not registered for code '{code}'");
                }
            }

            return GetStatusFallback(status);
        }

        /// <summary>
        /// Resolves a localization key through the current MST language.
        /// </summary>
        public string Localize(string localizationKey)
        {
            if (string.IsNullOrWhiteSpace(localizationKey))
                return string.Empty;

            return localizationProvider(localizationKey) ?? localizationKey;
        }

        /// <summary>
        /// Formats a localized message or returns null when the translation is unavailable.
        /// </summary>
        public string LocalizeFormat(string localizationKey, params object[] arguments)
        {
            string format = LocalizeOptional(localizationKey);
            return string.IsNullOrWhiteSpace(format) ? null : string.Format(format, arguments);
        }

        private MstProperties TryReadProperties(IIncomingMessage response)
        {
            if (response == null)
                return null;

            try
            {
                byte[] data = response.AsBytes();
                return TryReadProperties(data);
            }
            catch (Exception exception)
            {
                Logs.Warn($"Failed to decode MST error response: {exception.Message}");
                return null;
            }
        }

        private MstProperties TryReadProperties(byte[] data)
        {
            if (data == null || data.Length == 0)
                return null;

            try
            {
                return MstProperties.FromBytes(data);
            }
            catch (Exception exception)
            {
                Logs.Warn($"Failed to decode MST error response: {exception.Message}");
                return null;
            }
        }

        private string GetStatusFallback(ResponseStatus status)
        {
            if (!statusLocalizationKeys.TryGetValue(status, out string localizationKey))
                localizationKey = "ui.notification.error.unknown.message";

            return Localize(localizationKey);
        }

        private void RegisterStatusLocalizationKeys()
        {
            statusLocalizationKeys[ResponseStatus.Timeout] = "ui.error.response.timeout.message";
            statusLocalizationKeys[ResponseStatus.NotConnected] = "ui.status.connection.notConnected";
            statusLocalizationKeys[ResponseStatus.Error] = "ui.error.response.internal.message";
            statusLocalizationKeys[ResponseStatus.DependencyError] = "ui.error.response.dependency.message";
            statusLocalizationKeys[ResponseStatus.ServiceUnavailable] = "ui.error.response.unavailable.message";
            statusLocalizationKeys[ResponseStatus.Unhandled] = "ui.notification.error.unknown.message";
            statusLocalizationKeys[ResponseStatus.Unauthorized] = "ui.status.auth.unauthorized";
            statusLocalizationKeys[ResponseStatus.Forbidden] = "ui.error.response.forbidden.message";
            statusLocalizationKeys[ResponseStatus.TokenExpired] = "ui.notification.signIn.tokenExpired.message";
            statusLocalizationKeys[ResponseStatus.Banned] = "ui.error.response.banned.message";
            statusLocalizationKeys[ResponseStatus.DuplicateLogin] = "ui.notification.signIn.alreadySignedIn.message";
            statusLocalizationKeys[ResponseStatus.BadRequest] = "ui.error.response.badRequest.message";
            statusLocalizationKeys[ResponseStatus.Invalid] = "ui.error.response.invalid.message";
            statusLocalizationKeys[ResponseStatus.NotFound] = "ui.error.response.notFound.message";
            statusLocalizationKeys[ResponseStatus.AlreadyExists] = "ui.error.response.alreadyExists.message";
            statusLocalizationKeys[ResponseStatus.Conflict] = "ui.error.response.conflict.message";
            statusLocalizationKeys[ResponseStatus.Cancelled] = "ui.error.response.cancelled.message";
        }

        private void RegisterCommonParsers()
        {
            TryRegister(MstErrorCodes.UNKNOWN,
                _ => Localize("ui.notification.error.unknown.message"));
            TryRegister(MstErrorCodes.INTERNAL_ERROR,
                _ => Localize("ui.error.response.internal.message"));
            TryRegister(MstErrorCodes.PERMISSION_DENIED,
                _ => Localize("ui.error.response.forbidden.message"));
            TryRegister(MstErrorCodes.RESPONSE_INVALID,
                _ => Localize("ui.error.response.invalid.message"));
            TryRegister(MstErrorCodes.REQUEST_HANDLER_NOT_FOUND,
                _ => Localize("ui.error.response.notFound.message"));
            TryRegister(MstErrorCodes.REQUEST_HANDLER_FAILED,
                _ => Localize("ui.error.response.internal.message"));
            TryRegister(MstErrorCodes.RESOURCE_INSUFFICIENT, properties =>
                LocalizeFormat(GetLocalizationKey(MstErrorCodes.RESOURCE_INSUFFICIENT),
                    properties.AsString(MstErrorPropertyKeys.RESOURCE)));
            TryRegister(MstErrorCodes.RESOURCE_UNSUPPORTED, properties =>
                LocalizeFormat(GetLocalizationKey(MstErrorCodes.RESOURCE_UNSUPPORTED),
                    properties.AsString(MstErrorPropertyKeys.RESOURCE)));
        }

        private static string GetLocalizationKey(string code)
        {
            return $"ui.error.{code}.message";
        }

        private string LocalizeErrorCode(string code)
        {
            return LocalizeOptional(GetLocalizationKey(code));
        }

        private string LocalizeOptional(string localizationKey)
        {
            string localizedMessage = Localize(localizationKey);
            return string.Equals(localizedMessage, localizationKey, StringComparison.Ordinal)
                ? null
                : localizedMessage;
        }

        private static string LocalizeWithMst(string localizationKey)
        {
            return Mst.Localization == null ? localizationKey : Mst.Localization[localizationKey];
        }
    }
}
