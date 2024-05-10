using MasterServerToolkit.Extensions;
using MasterServerToolkit.Logging;
using System;
using System.IO;
using System.Net;
using System.Text;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class MstHelper
    {
        private const string alphanumericString = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        private const int minGeneratedStringLength = 1;
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
            int clampedLength = Mathf.Clamp(length, minGeneratedStringLength, maxGeneratedStringLength);
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
            int clampedLength = Mathf.Clamp(length, minGeneratedStringLength, maxGeneratedStringLength);
            StringBuilder resultStringBuilder = new StringBuilder();

            for (int i = 0; i < clampedLength; i++)
            {
                int nextChar = Random.Next(0, 10);
                resultStringBuilder.Append(nextChar.ToString());
            }

            return resultStringBuilder.ToString();
        }

        /// <summary>
        /// Create 128 bit unique string without "-" symbol
        /// </summary>
        /// <returns></returns>
        public string CreateGuidStringN()
        {
            return CreateGuid().ToString("N");
        }

        /// <summary>
        /// Create 128 bit unique string
        /// </summary>
        /// <returns></returns>
        public string CreateGuidString()
        {
            return CreateGuid().ToString();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public Guid CreateGuid()
        {
            return Guid.NewGuid();
        }

        /// <summary>
        /// Creates friendly unique string. There may be duplicates of the identifier
        /// </summary>
        /// <returns></returns>
        public string CreateFriendlyId()
        {
            string rawValue = Convert.ToBase64String(Guid.NewGuid().ToByteArray()).ToUint32Hash().ToString("x");

            while (rawValue.Length < 12)
            {
                rawValue += Random.Next(0, 16).ToString("X");
            }

            string result = "";

            for (int i = 0; i < rawValue.Length; i++)
            {
                if (i % 4 == 0 && result.Length > 0)
                    result += "-";

                result += rawValue[i];
            }

            return result;
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

            while (result.Length < 24)
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

            while (result.Length < 24)
            {
                result += Random.Next(0, 10).ToString();
            }

            return result.ToLower();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public ushort CreateUInt16Hash(string value)
        {
            return value.ToUint16Hash();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public uint CreateUInt32Hash(string value)
        {
            return value.ToUint32Hash();
        }

        /// <summary>
        /// Retrieves current public IP
        /// </summary>
        /// <param name="callback"></param>
        public string GetPublicIp()
        {
            try
            {
                // Create a request for the URL. 		
                WebRequest request = WebRequest.Create("https://ifconfig.co/ip");
                // Get the response.
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Stream dataStream = response.GetResponseStream();
                    StreamReader reader = new StreamReader(dataStream);
                    string responseFromServer = reader.ReadToEnd();
                    reader.Close();
                    dataStream.Close();
                    response.Close();
                    return responseFromServer;
                }
                else
                {
                    Logs.Error($"The following error occurred : {response.StatusCode}, {response.StatusDescription}");
                    return string.Empty;
                }
            }
            catch (WebException e)
            {
                Logs.Error($"The following error occurred : {e.Status}");
                return string.Empty;
            }
            catch (Exception e)
            {
                Logs.Error($"The following Exception was raised : {e.Message}");
                return string.Empty;
            }
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
    }
}