using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class CensorModule : BaseServerModule
    {
        protected HashSet<string> censoredWords;

        [Header("Settings"), SerializeField]
        private TextAsset[] wordsLists;
        [SerializeField, TextArea(5, 10)]
        private string matchPattern = @"\b{0}\b";

        public override void Initialize(IServer server)
        {
            ParseTextFiles();
        }

        protected virtual void ParseTextFiles()
        {
            censoredWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (TextAsset words in wordsLists)
            {
                var wordsArray = words.text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var word in wordsArray)
                {
                    censoredWords.Add(word.Trim());
                }
            }
        }

        /// <summary>
        /// Determines whether the text is dirty (has a bad word in it).
        /// </summary>
        /// <returns><c>true</c> if text is dirty; otherwise, <c>false</c>.</returns>
        /// <param name="text">Text to check for dirty words.</param>
        public virtual bool HasCensoredWord(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            foreach (var word in censoredWords)
            {
                string pattern = string.Format(matchPattern, Regex.Escape(word));
                Regex regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

                if (regex.IsMatch(text))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
