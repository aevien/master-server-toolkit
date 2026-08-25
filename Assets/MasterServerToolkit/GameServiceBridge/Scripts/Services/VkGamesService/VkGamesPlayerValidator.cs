using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MasterServerToolkit.GameService
{
    public sealed class VkGamesPlayerValidator : IPlayerValidator
    {
        private const int MaxQueryLength = 16 * 1024;
        private const int MaxParameterCount = 128;

        public Task<bool> Validate(MstProperties data)
        {
            string appId = Mst.Args.AsString(GameServiceArgNames.VKGAMES_APP_ID, string.Empty);
            string secretKey = Mst.Args.AsString(GameServiceArgNames.VKGAMES_SECRET_KEY, string.Empty);
            string bridgePlayerId = data.AsString(GameServiceKeys.BRIDGE_PLAYER_ID);
            string signedQuery = data.AsString(GameServiceKeys.BRIDGE_PLAYER_SIGNATURE);

            if (!TryValidate(
                secretKey,
                appId,
                bridgePlayerId,
                signedQuery,
                out string error))
            {
                Logs.Error($"VK Games player validation failed. Reason={error}");
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }

        internal static bool TryValidate(
            string secretKey,
            string expectedAppId,
            string bridgePlayerId,
            string signedQuery,
            out string error)
        {
            if (!TryValidateLaunchParameters(
                secretKey,
                expectedAppId,
                signedQuery,
                out string signedPlayerId,
                out error))
            {
                return false;
            }

            if (!string.Equals(signedPlayerId, bridgePlayerId, StringComparison.Ordinal))
                return Fail("player_id_mismatch", out error);

            return true;
        }

        private static bool TryValidateLaunchParameters(
            string secretKey,
            string expectedAppId,
            string signedQuery,
            out string signedPlayerId,
            out string error)
        {
            signedPlayerId = string.Empty;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(secretKey) || string.IsNullOrWhiteSpace(expectedAppId))
                return Fail("server_configuration_missing", out error);

            if (!TryParse(signedQuery, out Dictionary<string, string> values, out error))
                return false;

            if (!values.TryGetValue("sign", out string providedSignature) || string.IsNullOrWhiteSpace(providedSignature))
                return Fail("sign_missing", out error);

            if (!values.TryGetValue("sign_keys", out string signKeysValue) || string.IsNullOrWhiteSpace(signKeysValue))
                return Fail("sign_keys_missing", out error);

            if (!values.TryGetValue("api_id", out string appId) ||
                !string.Equals(appId, expectedAppId, StringComparison.Ordinal))
            {
                return Fail("app_id_mismatch", out error);
            }

            if (!values.TryGetValue("viewer_id", out string userId) ||
                !long.TryParse(userId, NumberStyles.None, CultureInfo.InvariantCulture, out long numericUserId) ||
                numericUserId <= 0)
            {
                return Fail("user_id_missing", out error);
            }

            if (!values.TryGetValue("api_url", out string apiUrl) ||
                !Uri.TryCreate(apiUrl, UriKind.Absolute, out Uri apiUri) ||
                apiUri.Scheme != Uri.UriSchemeHttps ||
                (apiUri.Host != "api.vk.ru" && apiUri.Host != "api.vk.com"))
            {
                return Fail("api_url_invalid", out error);
            }

            if (!values.TryGetValue("timestamp", out string timestamp) ||
                !long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out long numericTimestamp) ||
                numericTimestamp <= 0)
            {
                return Fail("timestamp_invalid", out error);
            }

            if (!TryBuildCanonicalQuery(values, signKeysValue, out string canonicalQuery, out error))
                return false;

            string expectedSignature;
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey)))
            {
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonicalQuery));
                expectedSignature = Convert.ToBase64String(hash)
                    .TrimEnd('=')
                    .Replace('+', '-')
                    .Replace('/', '_');
            }

            if (!FixedTimeEquals(expectedSignature, providedSignature))
                return Fail("signature_mismatch", out error);

            if (!values.TryGetValue("auth_key", out string providedAuthKey) ||
                !FixedTimeEquals(CreateAuthKey(appId, userId, secretKey), providedAuthKey))
            {
                return Fail("auth_key_mismatch", out error);
            }

            signedPlayerId = userId;
            return true;
        }

        private static bool TryBuildCanonicalQuery(
            Dictionary<string, string> values,
            string signKeysValue,
            out string canonicalQuery,
            out string error)
        {
            canonicalQuery = string.Empty;
            error = string.Empty;
            var signedKeys = new HashSet<string>(StringComparer.Ordinal);
            var signedValues = new SortedDictionary<string, string>(StringComparer.Ordinal);

            foreach (string key in signKeysValue.Split(','))
            {
                if (string.IsNullOrWhiteSpace(key) ||
                    key == "sign" ||
                    key == "sign_keys" ||
                    !signedKeys.Add(key))
                {
                    return Fail("sign_keys_invalid", out error);
                }

                if (!values.TryGetValue(key, out string value))
                    return Fail("signed_parameter_missing", out error);

                signedValues.Add(key, value);
            }

            if (!signedKeys.Contains("api_url") ||
                !signedKeys.Contains("api_id") ||
                !signedKeys.Contains("viewer_id") ||
                !signedKeys.Contains("auth_key") ||
                !signedKeys.Contains("timestamp"))
            {
                return Fail("signed_identity_missing", out error);
            }

            var builder = new StringBuilder();
            foreach (KeyValuePair<string, string> pair in signedValues)
            {
                if (builder.Length > 0)
                    builder.Append('&');

                builder
                    .Append(EncodeQueryComponent(pair.Key))
                    .Append('=')
                    .Append(EncodeQueryComponent(pair.Value));
            }

            canonicalQuery = builder.ToString();
            return true;
        }

        private static string EncodeQueryComponent(string value)
        {
            return Uri.EscapeDataString(value ?? string.Empty).Replace("%20", "+");
        }

        private static string CreateAuthKey(string appId, string userId, string secretKey)
        {
            byte[] source = Encoding.UTF8.GetBytes($"{appId}_{userId}_{secretKey}");
            byte[] hash;
            using (var md5 = MD5.Create())
                hash = md5.ComputeHash(source);

            var builder = new StringBuilder(hash.Length * 2);
            foreach (byte value in hash)
                builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));

            return builder.ToString();
        }

        private static bool TryParse(string query, out Dictionary<string, string> values, out string error)
        {
            values = new Dictionary<string, string>(StringComparer.Ordinal);
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(query))
                return Fail("query_missing", out error);

            if (query.Length > MaxQueryLength)
                return Fail("query_too_large", out error);

            string source = query[0] == '?' ? query.Substring(1) : query;
            string[] fields = source.Split('&');
            if (fields.Length > MaxParameterCount)
                return Fail("too_many_parameters", out error);

            foreach (string field in fields)
            {
                int separator = field.IndexOf('=');
                if (separator <= 0)
                    return Fail("query_malformed", out error);

                try
                {
                    string encodedKey = field.Substring(0, separator);
                    string encodedValue = field.Substring(separator + 1);
                    if (!HasValidPercentEncoding(encodedKey) || !HasValidPercentEncoding(encodedValue))
                        return Fail("query_encoding_invalid", out error);

                    string key = Uri.UnescapeDataString(encodedKey.Replace("+", " "));
                    string value = Uri.UnescapeDataString(encodedValue.Replace("+", " "));
                    if (values.ContainsKey(key))
                        return Fail("duplicate_parameter", out error);

                    values.Add(key, value);
                }
                catch (UriFormatException)
                {
                    return Fail("query_encoding_invalid", out error);
                }
            }

            return true;
        }

        private static bool HasValidPercentEncoding(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] != '%')
                    continue;

                if (i + 2 >= value.Length || !IsHex(value[i + 1]) || !IsHex(value[i + 2]))
                    return false;

                i += 2;
            }

            return true;
        }

        private static bool IsHex(char value)
        {
            return (value >= '0' && value <= '9') ||
                (value >= 'a' && value <= 'f') ||
                (value >= 'A' && value <= 'F');
        }

        private static bool FixedTimeEquals(string expected, string actual)
        {
            byte[] left = Encoding.UTF8.GetBytes(expected ?? string.Empty);
            byte[] right = Encoding.UTF8.GetBytes(actual ?? string.Empty);
            int difference = left.Length ^ right.Length;
            int count = Math.Max(left.Length, right.Length);
            for (int i = 0; i < count; i++)
            {
                byte leftValue = i < left.Length ? left[i] : (byte)0;
                byte rightValue = i < right.Length ? right[i] : (byte)0;
                difference |= leftValue ^ rightValue;
            }

            return difference == 0;
        }

        private static bool Fail(string value, out string error)
        {
            error = value;
            return false;
        }
    }
}
