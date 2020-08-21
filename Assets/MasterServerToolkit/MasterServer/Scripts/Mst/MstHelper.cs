using CommandTerminal;
using MasterServerToolkit.Networking;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class MstHelper
    {
        private const string dictionaryString = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        private const int maxGeneratedStringLength = 512;

        /// <summary>
        /// Creates a random string of a given length. Min length is 1, max length <see cref="maxGeneratedStringLength"/>
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        public string CreateRandomString(int length)
        {
            int clampedLength = Mathf.Clamp(length, 1, maxGeneratedStringLength);

            StringBuilder resultStringBuilder = new StringBuilder();

            for (int i = 0; i < clampedLength; i++)
            {
                resultStringBuilder.Append(dictionaryString[UnityEngine.Random.Range(0, dictionaryString.Length)]);
            }

            return resultStringBuilder.ToString();
        }

        /// <summary>
        /// Converts color to hex
        /// </summary>
        /// <param name="color"></param>
        /// <returns></returns>
        public string ColorToHex(Color32 color)
        {
            string hex = color.r.ToString("X2") + color.g.ToString("X2") + color.b.ToString("X2") + color.a.ToString("X2");
            return hex;
        }

        /// <summary>
        /// Converts hex to color
        /// </summary>
        /// <param name="hex"></param>
        /// <returns></returns>
        public Color HexToColor(string hex)
        {
            byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
            return new Color32(r, g, b, 255);
        }

        /// <summary>
        /// Create 128 bit unique string
        /// </summary>
        /// <returns></returns>
        public string CreateGuidString()
        {
            return Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// Retrieves current public IP
        /// </summary>
        /// <param name="callback"></param>
        public void GetPublicIp(Action<MstIpInfo, string> callback)
        {
            MstTimer.Instance.StartCoroutine(GetPublicIPCoroutine(callback));
        }

        /// <summary>
        /// Join command terminal arguments to one string
        /// </summary>
        /// <param name="args"></param>
        /// <param name="from"></param>
        /// <returns></returns>
        public string JoinCommandArgs(CommandArg[] args, int from)
        {
            StringBuilder sb = new StringBuilder();

            for (int i = from; i < args.Length; i++)
            {
                sb.Append($"{args[i].String.Trim()} ");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Wait for loading public IP from https://ifconfig.co/json
        /// </summary>
        /// <param name="callback"></param>
        /// <returns></returns>
        private IEnumerator GetPublicIPCoroutine(Action<MstIpInfo, string> callback)
        {
            UnityWebRequest www = UnityWebRequest.Get("https://ifconfig.co/json");
            yield return www.SendWebRequest();

            if (www.isNetworkError || www.isHttpError)
            {
                callback?.Invoke(null, www.error);
                Debug.LogError(www.error);
            }
            else
            {
                var ipInfo = JsonConvert.DeserializeObject<MstIpInfo>(www.downloadHandler.text);
                callback?.Invoke(ipInfo, string.Empty);
            }
        }
    }
}