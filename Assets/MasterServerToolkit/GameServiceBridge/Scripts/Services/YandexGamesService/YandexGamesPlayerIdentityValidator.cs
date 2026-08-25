using MasterServerToolkit.Json;

namespace MasterServerToolkit.GameService
{
    public static class YandexGamesPlayerIdentityValidator
    {
        public static bool TryValidate(string secret, string signatureValue, out string uniqueId, out MstJson payload)
        {
            uniqueId = string.Empty;
            payload = null;

            if (!YandexGamesSignatureVerifier.TryVerify(secret, signatureValue, out string payloadJson))
                return false;

            try
            {
                payload = new MstJson(payloadJson);
            }
            catch
            {
                return false;
            }

            return TryGetUniqueId(payload, out uniqueId);
        }

        private static bool TryGetUniqueId(MstJson payload, out string uniqueId)
        {
            uniqueId = string.Empty;

            if (TryReadString(payload, YandexGamesKeys.UniqueId, out uniqueId))
                return true;

            if (TryReadString(payload, YandexGamesKeys.Id, out uniqueId))
                return true;

            if (payload.HasField(YandexGamesKeys.Data))
            {
                MstJson data = payload[YandexGamesKeys.Data];

                if (TryReadString(data, YandexGamesKeys.UniqueId, out uniqueId))
                    return true;

                if (TryReadString(data, YandexGamesKeys.Id, out uniqueId))
                    return true;

                if (data.HasField(YandexGamesKeys.Player) &&
                    TryReadString(data[YandexGamesKeys.Player], YandexGamesKeys.UniqueId, out uniqueId))
                    return true;

                if (data.HasField(YandexGamesKeys.Player) &&
                    TryReadString(data[YandexGamesKeys.Player], YandexGamesKeys.Id, out uniqueId))
                    return true;
            }

            if (payload.HasField(YandexGamesKeys.Player) &&
                TryReadString(payload[YandexGamesKeys.Player], YandexGamesKeys.UniqueId, out uniqueId))
                return true;

            if (payload.HasField(YandexGamesKeys.Player) &&
                TryReadString(payload[YandexGamesKeys.Player], YandexGamesKeys.Id, out uniqueId))
                return true;

            return false;
        }

        private static bool TryReadString(MstJson json, string key, out string value)
        {
            value = string.Empty;

            if (json == null || !json.HasField(key))
                return false;

            value = json[key].StringValue;
            return !string.IsNullOrWhiteSpace(value);
        }
    }
}
