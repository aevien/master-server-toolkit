using MasterServerToolkit.Networking;
using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace MasterServerToolkit.MasterServer
{
    public delegate void GetPublicIPResult(string ip, string error);

    public class MstHelper
    {
        private const string alphanumericString = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        private const int maxGeneratedStringLength = 512;

        /// <summary>
        /// 
        /// </summary>
        public System.Random Random { get; private set; }

        public MstHelper()
        {
            Random = new System.Random();
        }


        /// <summary>
        /// Creates a random string of a given length. Min length is 1, max length <see cref="maxGeneratedStringLength"/>
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        public string CreateRandomAlphanumericString(int length)
        {
            int clampedLength = Mathf.Clamp(length, 1, maxGeneratedStringLength);
            StringBuilder resultStringBuilder = new StringBuilder();

            for (int i = 0; i < clampedLength; i++)
            {
                int nextChar = Random.Next(0, alphanumericString.Length);
                resultStringBuilder.Append(alphanumericString[nextChar]);
            }

            return resultStringBuilder.ToString();
        }


        /// <summary>
        /// Creates a random string of a given length. Min length is 1, max length <see cref="maxGeneratedStringLength"/>
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        public string CreateRandomDigitsString(int length)
        {
            int clampedLength = Mathf.Clamp(length, 1, maxGeneratedStringLength);
            StringBuilder resultStringBuilder = new StringBuilder();

            for (int i = 0; i < clampedLength; i++)
            {
                int nextChar = Random.Next(0, 10);
                resultStringBuilder.Append(nextChar.ToString());
            }

            return resultStringBuilder.ToString();
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
        /// Creates friendly unique string. There may be duplicates of the identifier
        /// </summary>
        /// <returns></returns>
        public string CreateFriendlyId()
        {
            string id = Guid.NewGuid().ToString("N").Substring(0, 10);
            return $"{id.Substring(0, 3)}-{id.Substring(3, 3)}-{id.Substring(6, 4)}";
        }

        /// <summary>
        /// Creates unique ID
        /// </summary>
        /// <returns></returns>
        public string CreateID_16()
        {
            var startTime = new DateTime(1970, 1, 1);
            TimeSpan timeSpan = (DateTime.Now.ToUniversalTime() - startTime);
            long val = (long)(timeSpan.TotalSeconds);

            string result = val.ToString("X");
            int totalRemained = 24 - result.Length;

            for (int i = 0; i < totalRemained; i++)
            {
                result += Random.Next(0, 16).ToString("X");
            }

            return result.ToLower();
        }

        /// <summary>
        /// Creates unique ID
        /// </summary>
        /// <returns></returns>
        public string CreateID_10()
        {
            var startTime = new DateTime(1970, 1, 1);
            TimeSpan timeSpan = (DateTime.Now.ToUniversalTime() - startTime);
            long val = (long)(timeSpan.TotalSeconds);

            string result = val.ToString();
            int totalRemained = 24 - result.Length;

            for (int i = 0; i < totalRemained; i++)
            {
                result += Random.Next(0, 9).ToString();
            }

            return result.ToLower();
        }

        /// <summary>
        /// Converts color to hex
        /// </summary>
        /// <param name="color"></param>
        /// <returns></returns>
        public string ColorToHex(Color32 color)
        {
            return color.r.ToString("X2") + color.g.ToString("X2") + color.b.ToString("X2") + color.a.ToString("X2");
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
        /// Retrieves current public IP
        /// </summary>
        /// <param name="callback"></param>
        public void GetPublicIp(GetPublicIPResult callback)
        {
            MstTimer.Singleton.StartCoroutine(GetPublicIPCoroutine(callback));
        }

        /// <summary>
        /// Join command terminal arguments to one string
        /// </summary>
        /// <param name="args"></param>
        /// <param name="from"></param>
        /// <returns></returns>
        public string JoinCommandArgs(string[] args, int from)
        {
            StringBuilder sb = new StringBuilder();

            for (int i = from; i < args.Length; i++)
            {
                sb.Append($"{args[i].Trim()} ");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Wait for loading public IP from https://ifconfig.co/ip
        /// </summary>
        /// <param name="callback"></param>
        /// <returns></returns>
        private IEnumerator GetPublicIPCoroutine(GetPublicIPResult callback)
        {
            using (UnityWebRequest www = UnityWebRequest.Get("https://ifconfig.co/ip"))
            {
                yield return www.SendWebRequest();

#if UNITY_2019_1_OR_NEWER && !UNITY_2020_3_OR_NEWER
                if (www.isHttpError || www.isNetworkError)
                {
                    callback?.Invoke(null, www.error);
                    Debug.LogError(www.error);
                }
                else
                {
                    var ipInfo = www.downloadHandler.text;
                    callback?.Invoke(ipInfo, string.Empty);
                }
#elif UNITY_2020_3_OR_NEWER
                if (www.result == UnityWebRequest.Result.ProtocolError
                    || www.result == UnityWebRequest.Result.ProtocolError
                     || www.result == UnityWebRequest.Result.DataProcessingError)
                {
                    callback?.Invoke(null, www.error);
                    Debug.LogError(www.error);
                }
                else if(www.result == UnityWebRequest.Result.Success)
                {
                    var ipInfo = www.downloadHandler.text;
                    callback?.Invoke(ipInfo, string.Empty);
                }
#endif
            }
        }
    }
}