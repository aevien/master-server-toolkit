using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class CensorModule : BaseServerModule
    {
        protected List<string> censoredWords;

        [Header("Settings"), SerializeField]
        private TextAsset[] wordsLists;

        public override void Initialize(IServer server)
        {
            ParseTextFiles();
        }

        protected virtual void ParseTextFiles()
        {
            censoredWords = new List<string>();

            char[] splitter = new char[] { ',', ' ' };

            foreach (TextAsset words in wordsLists)
            {
                censoredWords.AddRange(words.text.Split(splitter, System.StringSplitOptions.RemoveEmptyEntries).Select(word => word.Trim()));
            }
        }

        /// <summary>
        /// Determines whether the text is dirty (has a bad word in it).
        /// </summary>
        /// <returns><c>true</c> if text is dirty; otherwise, <c>false</c>.</returns>
        /// <param name="text">Text to check for dirty words.</param>
        public virtual bool HasCensoredWord(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            foreach (var pattern in censoredWords)
            {
                if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                {
                    return true;
                }
            }

            return false;
        }
    }
}