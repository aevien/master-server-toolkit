#if UNITY_WEBGL && !UNITY_EDITOR
using MasterServerToolkit.Logging;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Scripting;

namespace MasterServerToolkit.MasterServer
{
    internal sealed class MstWebGlCryptoBridge : MonoBehaviour
    {
        private const string BridgeObjectName = "__MstSecurityWebGlBridge";
        private const float RequestTimeoutSeconds = 30f;

        private sealed class PendingRequest
        {
            public Action<WebGlResult> Callback { get; set; }
            public float ExpiresAtRealtime { get; set; }
        }

        private static readonly Dictionary<string, PendingRequest> callbacks =
            new Dictionary<string, PendingRequest>(StringComparer.Ordinal);
        private static MstWebGlCryptoBridge instance;

        internal sealed class WebGlResult
        {
            public byte[] EncryptedDataKey { get; set; }
            public byte[] Nonce { get; set; }
            public byte[] CipherText { get; set; }
            public byte[] AuthenticationTag { get; set; }
            public string Error { get; set; }
        }

        [DllImport("__Internal")]
        private static extern void MstSecurity_EncryptForMaster(
            string bridgeObjectName,
            string requestId,
            string publicKeySpki,
            string plaintext,
            string associatedData);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            callbacks.Clear();
            instance = null;
        }

        public static void EncryptForMaster(
            byte[] publicKeySpki,
            byte[] plaintext,
            byte[] associatedData,
            Action<WebGlResult> callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            EnsureInstance();
            string requestId = Guid.NewGuid().ToString("N");
            callbacks.Add(requestId, new PendingRequest
            {
                Callback = callback,
                ExpiresAtRealtime = Time.realtimeSinceStartup + RequestTimeoutSeconds
            });

            try
            {
                MstSecurity_EncryptForMaster(
                    BridgeObjectName,
                    requestId,
                    Convert.ToBase64String(publicKeySpki),
                    Convert.ToBase64String(plaintext),
                    Convert.ToBase64String(associatedData));
            }
            catch (Exception exception)
            {
                callbacks.Remove(requestId);
                callback(new WebGlResult
                {
                    Error = exception.Message
                });
            }
        }

        [Preserve]
        public void OnMstSecurityResult(string payload)
        {
            if (string.IsNullOrEmpty(payload))
                return;

            string[] parts = payload.Split('|');

            if (parts.Length < 3 ||
                !callbacks.TryGetValue(parts[0], out PendingRequest pendingRequest))
            {
                return;
            }

            callbacks.Remove(parts[0]);
            var result = new WebGlResult();

            try
            {
                if (parts[1] != "1")
                {
                    result.Error = DecodeText(parts[2], "Web Crypto encryption failed");
                }
                else
                {
                    if (parts.Length != 6)
                        throw new FormatException("Malformed Web Crypto response");

                    result.EncryptedDataKey = Convert.FromBase64String(parts[2]);
                    result.Nonce = Convert.FromBase64String(parts[3]);
                    result.CipherText = Convert.FromBase64String(parts[4]);
                    result.AuthenticationTag = Convert.FromBase64String(parts[5]);
                }
            }
            catch (Exception exception)
            {
                result = new WebGlResult
                {
                    Error = exception.Message
                };
            }

            InvokeCallback(pendingRequest.Callback, result);
        }

        internal static void CancelAll(string error)
        {
            if (callbacks.Count == 0)
                return;

            var pendingRequests = new List<PendingRequest>(callbacks.Values);
            callbacks.Clear();

            foreach (PendingRequest pendingRequest in pendingRequests)
            {
                InvokeCallback(pendingRequest.Callback, new WebGlResult
                {
                    Error = error
                });
            }
        }

        private void Update()
        {
            if (callbacks.Count == 0)
                return;

            float currentTime = Time.realtimeSinceStartup;
            List<string> expiredRequestIds = null;

            foreach (KeyValuePair<string, PendingRequest> pair in callbacks)
            {
                if (currentTime < pair.Value.ExpiresAtRealtime)
                    continue;

                expiredRequestIds ??= new List<string>();
                expiredRequestIds.Add(pair.Key);
            }

            if (expiredRequestIds == null)
                return;

            foreach (string requestId in expiredRequestIds)
            {
                if (!callbacks.TryGetValue(requestId, out PendingRequest pendingRequest))
                    continue;

                callbacks.Remove(requestId);
                InvokeCallback(pendingRequest.Callback, new WebGlResult
                {
                    Error = "Web Crypto encryption timed out"
                });
            }
        }

        private void OnDestroy()
        {
            if (instance != this)
                return;

            instance = null;
            CancelAll("WebGL security bridge was destroyed");
        }

        private static void EnsureInstance()
        {
            if (instance != null)
                return;

            GameObject existing = GameObject.Find(BridgeObjectName);

            if (existing != null)
                instance = existing.GetComponent<MstWebGlCryptoBridge>();

            if (instance != null)
                return;

            var bridgeObject = new GameObject(BridgeObjectName)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            DontDestroyOnLoad(bridgeObject);
            instance = bridgeObject.AddComponent<MstWebGlCryptoBridge>();
        }

        private static string DecodeText(string base64, string fallback)
        {
            try
            {
                return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            }
            catch
            {
                return fallback;
            }
        }

        private static void InvokeCallback(
            Action<WebGlResult> callback,
            WebGlResult result)
        {
            try
            {
                callback(result);
            }
            catch (Exception exception)
            {
                Logs.Error(
                    $"WebGL security callback failed: {exception}",
                    LogChannels.Security);
            }
        }
    }
}
#endif
