using System;
using System.Collections.Generic;

namespace MasterServerToolkit.CommandTerminal
{
    public class CommandAutocomplete
    {
        private readonly List<string> knownWords = new List<string>();
        private readonly List<string> buffer = new List<string>();

        public IReadOnlyList<string> KnownWords => knownWords;

        public void SetWords(IEnumerable<string> words)
        {
            knownWords.Clear();

            if (words == null)
                return;

            foreach (string word in words)
                Register(word);

            knownWords.Sort(StringComparer.OrdinalIgnoreCase);
        }

        public void Register(string word)
        {
            if (string.IsNullOrWhiteSpace(word))
                return;

            string normalized = word.Trim();

            if (knownWords.Exists(x => string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase)))
                return;

            knownWords.Add(normalized);
        }

        public void Unregister(string word)
        {
            if (string.IsNullOrWhiteSpace(word))
                return;

            knownWords.RemoveAll(x => string.Equals(x, word.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public string[] Complete(ref string text)
        {
            text ??= string.Empty;
            string partialWord = EatLastWord(ref text);
            buffer.Clear();

            for (int i = 0; i < knownWords.Count; i++)
            {
                string known = knownWords[i];

                if (known.StartsWith(partialWord, StringComparison.OrdinalIgnoreCase))
                    buffer.Add(known);
            }

            return buffer.ToArray();
        }

        public void Clear()
        {
            knownWords.Clear();
            buffer.Clear();
        }

        private string EatLastWord(ref string text)
        {
            int lastSpace = text.LastIndexOf(' ');

            if (lastSpace < 0)
            {
                string result = text;
                text = string.Empty;
                return result;
            }

            string word = text.Substring(lastSpace + 1);
            text = text.Substring(0, lastSpace + 1);
            return word;
        }
    }
}
