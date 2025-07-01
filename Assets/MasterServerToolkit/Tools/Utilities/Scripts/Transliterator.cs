using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MasterServerToolkit.MasterServer
{
    public class Transliterator
    {
        private static readonly Dictionary<string, string> CyrToLat = new Dictionary<string, string>
        {
            {"а", "a"}, {"б", "b"}, {"в", "v"}, {"г", "g"}, {"д", "d"},
            {"е", "ye"}, {"ё", "yo"}, {"ж", "zh"}, {"з", "z"}, {"и", "i"},
            {"й", "j"}, {"к", "k"}, {"л", "l"}, {"м", "m"}, {"н", "n"},
            {"о", "o"}, {"п", "p"}, {"р", "r"}, {"с", "s"}, {"т", "t"},
            {"у", "u"}, {"ф", "f"}, {"х", "kh"}, {"ц", "ts"}, {"ч", "ch"},
            {"ш", "sh"}, {"щ", "shch"}, {"ъ", "'"}, {"ы", "y"}, {"ь", "`"},
            {"э", "e"}, {"ю", "yu"}, {"я", "ya"},
            // Заглавные буквы
            {"А", "A"}, {"Б", "B"}, {"В", "V"}, {"Г", "G"}, {"Д", "D"},
            {"Е", "Ye"}, {"Ё", "Yo"}, {"Ж", "Zh"}, {"З", "Z"}, {"И", "I"},
            {"Й", "J"}, {"К", "K"}, {"Л", "L"}, {"М", "M"}, {"Н", "N"},
            {"О", "O"}, {"П", "P"}, {"Р", "R"}, {"С", "S"}, {"Т", "T"},
            {"У", "U"}, {"Ф", "F"}, {"Х", "Kh"}, {"Ц", "Ts"}, {"Ч", "Ch"},
            {"Ш", "Sh"}, {"Щ", "Shch"}, {"Ъ", "'"}, {"Ы", "Y"}, {"Ь", "`"},
            {"Э", "E"}, {"Ю", "Yu"}, {"Я", "Ya"}
        };

        private static readonly Dictionary<string, string> LatToCyr = new Dictionary<string, string>
        {
            {"zh", "ж"}, {"kh", "х"}, {"ts", "ц"}, {"ch", "ч"}, {"sh", "ш"},
            {"shch", "щ"}, {"yu", "ю"}, {"ya", "я"}, {"yo", "ё"}, {"j", "й"},
            {"`", "ь"}, {"'", "ъ"}, {"y", "ы"}, {"a", "а"}, {"b", "б"}, {"v", "в"},
            {"g", "г"}, {"d", "д"}, {"z", "з"}, {"i", "и"}, {"k", "к"},
            {"l", "л"}, {"m", "м"}, {"n", "н"}, {"o", "о"}, {"p", "п"}, {"r", "р"},
            {"s", "с"}, {"t", "т"}, {"u", "у"}, {"f", "ф"},
            // Для "е" и "э"
            {"ye", "е"}, {"Ye", "Е"}, {"YE", "Е"}, {"e", "э"}, {"E", "Э"},
            // Заглавные комбинации
            {"Zh", "Ж"}, {"Kh", "Х"}, {"Ts", "Ц"}, {"Ch", "Ч"}, {"Sh", "Ш"},
            {"Shch", "Щ"}, {"Yu", "Ю"}, {"Ya", "Я"}, {"Yo", "Ё"}, {"J", "Й"},
            {"Y", "Ы"}, {"A", "А"}, {"B", "Б"}, {"V", "В"}, {"G", "Г"}, {"D", "Д"},
            {"Z", "З"}, {"I", "И"}, {"K", "К"}, {"L", "Л"}, {"M", "М"},
            {"N", "Н"}, {"O", "О"}, {"P", "П"}, {"R", "Р"}, {"S", "С"}, {"T", "Т"},
            {"U", "У"}, {"F", "Ф"}
        };

        public static string CyrillicToLatin(string input)
        {
            if (string.IsNullOrEmpty(input)) 
                return input;

            var sb = new StringBuilder();

            foreach (char c in input)
            {
                string key = c.ToString();
                sb.Append(CyrToLat.TryGetValue(key, out var value) ? value : c);
            }

            return sb.ToString();
        }

        public static string LatinToCyrillic(string input)
        {
            if (string.IsNullOrEmpty(input)) 
                return input;

            var sb = new StringBuilder();
            int pos = 0;

            while (pos < input.Length)
            {
                bool replaced = false;

                foreach (var key in LatToCyr.Keys.OrderByDescending(k => k.Length))
                {
                    if (pos + key.Length <= input.Length)
                    {
                        string substr = input.Substring(pos, key.Length);

                        if (LatToCyr.TryGetValue(substr, out var value))
                        {
                            sb.Append(value);
                            pos += key.Length;
                            replaced = true;
                            break;
                        }
                    }
                }

                if (!replaced)
                {
                    sb.Append(input[pos++]);
                }
            }

            return sb.ToString();
        }
    }
}