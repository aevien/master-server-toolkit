using MasterServerToolkit.Utils;
using System;
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
            // Generate a new GUID and convert it to a 16-character hex string
            string rawValue = CreateGuidStringN().Substring(0, 12).ToUpper();

            // If the length of rawValue is less than 12, append random hex digits to make it 12 characters long
            while (rawValue.Length < 12)
            {
                rawValue += Random.Next(0, 16).ToString("X");
            }

            // Use StringBuilder for efficient concatenation
            StringBuilder result = new StringBuilder();

            for (int i = 0; i < rawValue.Length; i++)
            {
                if (i > 0 && i % 4 == 0)
                {
                    result.Append("-");
                }

                result.Append(rawValue[i]);
            }

            return result.ToString();
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
        /// Retrieves current public IP
        /// </summary>
        /// <param name="callback"></param>
        public string GetPublicIp()
        {
            return NetWebRequests.Get("https://ifconfig.co/ip").StringValue;
        }
    }
}