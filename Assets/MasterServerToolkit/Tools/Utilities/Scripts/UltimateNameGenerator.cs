// PostApocalypticNameGenerator.cs
// Unity 2022.3+
// Extensible post-apocalyptic single-word name generator with per-language caches,
// default language fallback, and per-language override for HarshOnset chance.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RedResistance.Names
{
    /// <summary>
    /// Style of phoneme pools: smoother or harsher consonant clusters.
    /// </summary>
    public enum PhonemeFlavor
    {
        Neutral,
        Harsher,
        Softer
    }

    /// <summary>
    /// Abstract language configuration for the generator (extensible via inheritance).
    /// </summary>
    public abstract class LangConfig
    {
        /// <summary>
        /// Backing flag indicating whether this language should be the default one.
        /// </summary>
        protected readonly bool _isDefault;

        /// <summary>
        /// Base constructor to optionally mark this language as default.
        /// </summary>
        protected LangConfig(bool defaultLang = false)
        {
            _isDefault = defaultLang;
        }

        /// <summary>
        /// Language code (e.g., "ru", "en", "tr"). Must be unique across registered languages.
        /// </summary>
        public abstract string Code { get; }

        /// <summary>
        /// True if this language is proposed as default during registration.
        /// </summary>
        public bool IsDefault => _isDefault;

        /// <summary>
        /// Common onsets (start consonants).
        /// </summary>
        public abstract string[] Onsets { get; }

        /// <summary>
        /// Harsher onsets used occasionally to add grit.
        /// </summary>
        public abstract string[] HarshOnsets { get; }

        /// <summary>
        /// Vowel nuclei.
        /// </summary>
        public abstract string[] Nuclei { get; }

        /// <summary>
        /// Optional codas (end consonants).
        /// </summary>
        public abstract string[] Codas { get; }

        /// <summary>
        /// Probability to use coda for a syllable.
        /// </summary>
        public abstract float CodaUseProbability { get; }

        /// <summary>
        /// Thematic starts like "rust/ash/dust" equivalents.
        /// </summary>
        public abstract string[] ThematicStart { get; }

        /// <summary>
        /// Thematic middles inserted between syllables.
        /// </summary>
        public abstract string[] ThematicMiddle { get; }

        /// <summary>
        /// Thematic ends appended at the end.
        /// </summary>
        public abstract string[] ThematicEnd { get; }

        /// <summary>
        /// Illegal or ugly clusters to soften.
        /// </summary>
        public abstract string[] IllegalClusters { get; }

        /// <summary>
        /// Per-language override for harsh onset probability [0..1].
        /// Return value &lt; 0 means "no override, use global Settings.HarshOnsetChance".
        /// </summary>
        public virtual float HarshOnsetChanceOverride => -1f;
    }

    /// <summary>
    /// Russian language config (Cyrillic).
    /// </summary>
    public sealed class RussianLangConfig : LangConfig
    {
        /// <summary>
        /// Create Russian language config. Set defaultLang=true to mark as default.
        /// </summary>
        public RussianLangConfig(bool defaultLang = false) : base(defaultLang) { }

        public override string Code => "ru";
        public override string[] Onsets => new[]
        {
            "бр","гр","др","кр","пр","тр","вр","зр","скр","стр",
            "ш","ж","з","с","к","г","д","т","п","в","ф","м","н","р","л",
            "ск","ст","см","сп","св","скл","пл","кл","гл","бл","фл",
            "ч","шр","жг","зн","сн","мн","рж","рк","рт",
        };
        public override string[] HarshOnsets => new[] { "скр", "стр", "жг", "шк", "жм", "здр", "взр", "рж", "хр", "грз", "дрск" };
        public override string[] Nuclei => new[]
        {
            "а","о","у","ы","э","и","я","ё","ю","аи","ау","оя","ио","ея","уа","юа","оу","иа"
        };
        public override string[] Codas => new[]
        {
            "к","г","д","т","п","ф","с","з","ш","щ","ч","м","н","р","л",
            "ск","ст","шт","рд","рк","рт","вш","зк","зд","нг","мр","лг","лд"
        };
        public override float CodaUseProbability => 0.65f;
        public override string[] ThematicStart => new[] { "пепл", "ржав", "прах", "угл", "копт", "серт", "тьм", "ядр", "жгл", "гар" };
        public override string[] ThematicMiddle => new[] { "пыл", "гар", "мхл", "рск", "шлак", "зол", "ртуть", "яд", "мор", "тлен" };
        public override string[] ThematicEnd => new[] { "ник", "мор", "тлен", "шрам", "пепел", "рвав", "пад", "прах", "мут", "жгут" };
        public override string[] IllegalClusters => new[] { "йй", "жьш", "шщ", "щш", "ьь", "ъъ", "ыы", "ууу", "ооо", "ссс", "ллл", "ррр", "жжж", "ччч", "шшш" };

        // Example: keep default behavior; no override -> -1f (inherited).
        // public override float HarshOnsetChanceOverride => 0f; // uncomment to force "never harsh" for RU
    }

    /// <summary>
    /// English language config (Latin).
    /// </summary>
    public sealed class EnglishLangConfig : LangConfig
    {
        /// <summary>
        /// Create English language config. Set defaultLang=true to mark as default.
        /// </summary>
        public EnglishLangConfig(bool defaultLang = false) : base(defaultLang) { }

        public override string Code => "en";
        public override string[] Onsets => new[]
        {
            "br","gr","dr","cr","pr","tr","wr","scr","str","sk","st","sp",
            "b","c","d","f","g","h","j","k","l","m","n","p","r","s","t","v","w","z",
            "cl","gl","pl","bl","fl","sl","sn","sm","sw","thr","shr","spr"
        };
        public override string[] HarshOnsets => new[] { "scr", "str", "spl", "spr", "sk", "st", "grit", "crust", "crack", "scorch", "slag" };
        public override string[] Nuclei => new[] { "a", "e", "i", "o", "u", "y", "ae", "ai", "au", "oa", "ou", "ei", "ie", "oo", "ea" };
        public override string[] Codas => new[] { "k", "g", "d", "t", "p", "f", "s", "z", "sh", "ch", "m", "n", "r", "l", "sk", "st", "rd", "rk", "rt", "ft", "ct", "x", "th", "mp", "nd", "ng" };
        public override float CodaUseProbability => 0.6f;
        public override string[] ThematicStart => new[] { "ash", "rust", "soot", "scar", "slag", "grim", "dust", "burn", "char", "waste" };
        public override string[] ThematicMiddle => new[] { "blight", "scorch", "grime", "scrap", "grit", "toxin", "rot", "mire", "smog", "scar" };
        public override string[] ThematicEnd => new[] { "scar", "blight", "waste", "bane", "gloom", "dusk", "mire", "slag", "scrap", "husk" };
        public override string[] IllegalClusters => new[] { "qq", "ww", "zzzz", "xxxx", "----", "'''", "    ", "yyyz", "jhx", "cktst" };

        // Example per-language override:
        // public override float HarshOnsetChanceOverride => 0.15f; // softer English
    }

    /// <summary>
    /// Turkish language config (Latin).
    /// </summary>
    public sealed class TurkishLangConfig : LangConfig
    {
        /// <summary>
        /// Create Turkish language config. Set defaultLang=true to mark as default.
        /// </summary>
        public TurkishLangConfig(bool defaultLang = false) : base(defaultLang) { }

        public override string Code => "tr";
        public override string[] Onsets => new[]
        {
            "br","gr","kr","pr","tr","dr","vr","fr","sk","st","sp",
            "b","c","ç","d","f","g","ğ","h","j","k","l","m","n","p","r","s","ş","t","v","y","z",
            "pl","kl","gl","bl","fl","sl","sr","şr"
        };
        public override string[] HarshOnsets => new[] { "sk", "st", "spr", "str", "kr", "gr", "çr", "şr", "scr", "str" };
        public override string[] Nuclei => new[] { "a", "e", "ı", "i", "o", "ö", "u", "ü", "ai", "au", "ia", "io", "uo", "öa", "üa", "ea" };
        public override string[] Codas => new[] { "k", "g", "d", "t", "p", "f", "s", "z", "ş", "ç", "m", "n", "r", "l", "sk", "st", "rt", "rk", "nd", "ng" };
        public override float CodaUseProbability => 0.55f;
        public override string[] ThematicStart => new[] { "kül", "pas", "toz", "iz", "yan", "kar", "sis", "çor", "kıran", "zehir" };
        public override string[] ThematicMiddle => new[] { "çürü", "is", "kir", "pas", "sis", "köz", "kül", "toz", "yan", "kabuk" };
        public override string[] ThematicEnd => new[] { "toz", "kül", "pas", "sis", "huzun", "karan", "yıkım", "iz", "duman", "enkaz" };
        public override string[] IllegalClusters => new[] { "ğğ", "ııı", "üüü", "ööö", "sssş", "şşш", "ççç", "yyyz", "qqq" };

        // Example: disable harshness entirely for TR
        // public override float HarshOnsetChanceOverride => 0f;
    }

    /// <summary>
    /// Post-apocalyptic single-word name generator with pluggable languages and internal caches.
    /// Includes default language fallback and per-language harsh-onset override.
    /// </summary>
    public static class UltimateNameGenerator
    {
        /// <summary>
        /// Registered language configs (keyed by Code).
        /// </summary>
        private static readonly Dictionary<string, LangConfig> _langs = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Per-language cached names (acts like a simple pool; LIFO in GetNext).
        /// </summary>
        private static readonly Dictionary<string, List<string>> _cache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Per-language registry of already-seen names to avoid duplicates across refills.
        /// </summary>
        private static readonly Dictionary<string, HashSet<string>> _used = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Code of the current default language (fallback target). Empty if none set yet.
        /// </summary>
        public static string DefaultLangCode { get; private set; } = string.Empty;

        /// <summary>
        /// Default generation settings used by WarmUp/Ensure/Refill.
        /// </summary>
        private static Settings _defaults = new Settings
        {
            MinLength = 4,
            MaxLength = 11,
            PreferredSyllables = 3.0f,
            ThemeBias = 2,
            Flavor = PhonemeFlavor.Neutral,
            ForceSingleWord = true,
            NormalizeCasing = true,
            EnsureUniqueInBatch = true,
            Seed = -1,
            HarshOnsetChance = 0.35f
        };

        /// <summary>
        /// Static ctor: register built-in languages (ru, en, tr) with RU as default.
        /// </summary>
        static UltimateNameGenerator()
        {
            RegisterLanguages(new LangConfig[]
            {
                new RussianLangConfig(defaultLang: true),
                new EnglishLangConfig(),
                new TurkishLangConfig()
            });
        }

        /// <summary>
        /// Default settings for cache-driven generation.
        /// </summary>
        public struct Settings
        {
            /// <summary>Minimum characters per name after cleanup.</summary>
            public int MinLength;
            /// <summary>Maximum characters per name after cleanup.</summary>
            public int MaxLength;
            /// <summary>Typical syllables (2–4 recommended).</summary>
            public float PreferredSyllables;
            /// <summary>Bias for thematic morphemes (0 = never).</summary>
            public int ThemeBias;
            /// <summary>Phoneme flavor shaping harshness.</summary>
            public PhonemeFlavor Flavor;
            /// <summary>Remove spaces/hyphens/apostrophes.</summary>
            public bool ForceSingleWord;
            /// <summary>Capitalize first char, lower the rest.</summary>
            public bool NormalizeCasing;
            /// <summary>Ensure duplicates are avoided within each batch.</summary>
            public bool EnsureUniqueInBatch;
            /// <summary>Seed for deterministic generation (-1 = random).</summary>
            public int Seed;
            /// <summary>Probability [0..1] to use HarshOnsets when Flavor = Harsher (unless the language overrides it).</summary>
            public float HarshOnsetChance;
        }

        /// <summary>
        /// Registers a single language config. If the code already exists, it will be replaced.
        /// Honors config.IsDefault and first-registered-language as default logic.
        /// </summary>
        public static void RegisterLanguage(LangConfig config)
        {
            if (config == null || string.IsNullOrWhiteSpace(config.Code))
                throw new ArgumentException("Invalid language config");

            _langs[config.Code] = config;
            if (!_cache.ContainsKey(config.Code)) _cache[config.Code] = new List<string>(512);
            if (!_used.ContainsKey(config.Code)) _used[config.Code] = new HashSet<string>();

            if (string.IsNullOrWhiteSpace(DefaultLangCode))
                DefaultLangCode = config.Code;

            if (config.IsDefault)
                DefaultLangCode = config.Code;
        }

        /// <summary>
        /// Registers multiple language configs at once.
        /// </summary>
        public static void RegisterLanguages(IEnumerable<LangConfig> configs)
        {
            foreach (var c in configs) RegisterLanguage(c);
        }

        /// <summary>
        /// Returns true if language code is known (registered).
        /// </summary>
        public static bool HasLanguage(string code) =>
            !string.IsNullOrWhiteSpace(code) && _langs.ContainsKey(code);

        /// <summary>
        /// Returns array of registered language codes.
        /// </summary>
        public static string[] GetLanguageCodes() => _langs.Keys.ToArray();

        /// <summary>
        /// Sets default language by code if registered. Throws if code is unknown.
        /// </summary>
        public static void SetDefaultLanguage(string code)
        {
            if (!HasLanguage(code))
                throw new InvalidOperationException($"Cannot set default. Language '{code}' is not registered.");
            DefaultLangCode = code;
        }

        /// <summary>
        /// Updates default settings used by WarmUp/Ensure/Refill for the internal cache.
        /// </summary>
        public static void ConfigureDefaults(in Settings settings) => _defaults = settings;

        /// <summary>
        /// Clears cache and used registry for a specific language. Unknown code falls back to default.
        /// </summary>
        public static void Clear(string langCode)
        {
            var code = ResolveCodeOrDefault(langCode);
            _cache[code].Clear();
            _used[code].Clear();
        }

        /// <summary>
        /// Clears all languages caches and used registries.
        /// </summary>
        public static void ClearAll()
        {
            foreach (var list in _cache.Values) list.Clear();
            foreach (var set in _used.Values) set.Clear();
        }

        /// <summary>
        /// Returns current cached count for a language. Unknown code falls back to default.
        /// </summary>
        public static int Count(string langCode)
        {
            var code = ResolveCodeOrDefault(langCode);
            return _cache[code].Count;
        }

        /// <summary>
        /// Pre-generates and stores 'count' names for a language into the internal cache. Unknown code → default.
        /// </summary>
        public static void WarmUp(string langCode, int count)
        {
            if (count <= 0) return;
            var code = ResolveCodeOrDefault(langCode);
            Refill_Internal(code, count);
        }

        /// <summary>
        /// Pre-generates and stores 'countEach' names for all registered languages.
        /// </summary>
        public static void WarmUpAll(int countEach)
        {
            if (countEach <= 0) return;
            foreach (var code in _langs.Keys)
                Refill_Internal(code, countEach);
        }

        /// <summary>
        /// Ensures that the cache for the language has at least 'minCount' items; refills if needed. Unknown code → default.
        /// </summary>
        public static void Ensure(string langCode, int minCount, int refillBatch = 200)
        {
            if (minCount <= 0) return;
            var code = ResolveCodeOrDefault(langCode);
            int need = minCount - _cache[code].Count;
            if (need > 0)
                Refill_Internal(code, Math.Max(refillBatch, need));
        }

        /// <summary>
        /// Appends unique names into the cache using current defaults for the given language code.
        /// Over-generates to compensate for duplicate filtering against _used.
        /// </summary>
        private static void Refill_Internal(string langCode, int count)
        {
            var cfg = _langs[langCode];
            var list = _cache[langCode];
            var used = _used[langCode];

            int over = Math.Max(count + count / 2, count + 32);

            var batch = GenerateBatch(
                cfg: cfg,
                count: over,
                minLength: _defaults.MinLength,
                maxLength: _defaults.MaxLength,
                preferredSyllables: _defaults.PreferredSyllables,
                themeBias: _defaults.ThemeBias,
                flavor: _defaults.Flavor,
                forceSingleWord: _defaults.ForceSingleWord,
                normalizeCasing: _defaults.NormalizeCasing,
                ensureUniqueInBatch: _defaults.EnsureUniqueInBatch,
                seed: _defaults.Seed
            );

            for (int i = 0; i < batch.Count && count > 0; i++)
            {
                string n = batch[i];
                if (used.Contains(n)) continue;
                list.Add(n);
                used.Add(n);
                count--;
            }
        }

        /// <summary>
        /// Takes the next name (LIFO) from the cached pool; if empty, auto-refills by 'refillBatch'. Unknown code → default.
        /// </summary>
        public static string GetNext(string langCode, int refillBatch = 200)
        {
            var code = ResolveCodeOrDefault(langCode);
            var list = _cache[code];
            if (list.Count == 0)
                Refill_Internal(code, Math.Max(16, refillBatch));

            int last = list.Count - 1;
            string pick = list[last];
            list.RemoveAt(last);
            return pick;
        }

        /// <summary>
        /// Returns a random name from the cached pool. If 'consume' is true, removes it (swap-remove).
        /// If empty, auto-refills by 'refillBatch'. Optional 'seed' for deterministic pick. Unknown code → default.
        /// </summary>
        public static string GetRandom(string langCode, bool consume = true, int refillBatch = 200, int seed = -1)
        {
            var code = ResolveCodeOrDefault(langCode);
            var list = _cache[code];
            if (list.Count == 0)
                Refill_Internal(code, Math.Max(16, refillBatch));

            var rng = (seed >= 0) ? new Random(seed) : new Random();
            int idx = rng.Next(0, list.Count);

            if (!consume)
                return list[idx];

            string picked = list[idx];
            int last = list.Count - 1;
            list[idx] = list[last];
            list.RemoveAt(last);
            return picked;
        }

        /// <summary>
        /// Returns a snapshot copy of the cached names for the language. Unknown code → default.
        /// </summary>
        public static List<string> GetAllSnapshot(string langCode)
        {
            var code = ResolveCodeOrDefault(langCode);
            return new List<string>(_cache[code]);
        }

        /// <summary>
        /// Generate a batch of names for a (possibly unknown) language code; on unknown, uses default.
        /// </summary>
        public static List<string> GenerateBatch(
            string langCode,
            int count,
            int minLength = 4,
            int maxLength = 11,
            float preferredSyllables = 3f,
            int themeBias = 1,
            PhonemeFlavor flavor = PhonemeFlavor.Neutral,
            bool forceSingleWord = true,
            bool normalizeCasing = true,
            bool ensureUniqueInBatch = true,
            int seed = -1,
            string optionalPrefix = "",
            string optionalSuffix = "")
        {
            var code = ResolveCodeOrDefault(langCode);
            return GenerateBatch(
                cfg: _langs[code],
                count: count,
                minLength: minLength,
                maxLength: maxLength,
                preferredSyllables: preferredSyllables,
                themeBias: themeBias,
                flavor: flavor,
                forceSingleWord: forceSingleWord,
                normalizeCasing: normalizeCasing,
                ensureUniqueInBatch: ensureUniqueInBatch,
                seed: seed,
                optionalPrefix: optionalPrefix,
                optionalSuffix: optionalSuffix
            );
        }

        /// <summary>
        /// Generate a batch of names directly from a LangConfig (useful for ad-hoc/testing).
        /// </summary>
        public static List<string> GenerateBatch(
            LangConfig cfg,
            int count,
            int minLength = 4,
            int maxLength = 11,
            float preferredSyllables = 3f,
            int themeBias = 1,
            PhonemeFlavor flavor = PhonemeFlavor.Neutral,
            bool forceSingleWord = true,
            bool normalizeCasing = true,
            bool ensureUniqueInBatch = true,
            int seed = -1,
            string optionalPrefix = "",
            string optionalSuffix = "")
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));

            var rng = (seed >= 0) ? new Random(seed) : new Random();

            int target = Math.Max(1, count);
            var results = new List<string>(target);
            var used = ensureUniqueInBatch ? new HashSet<string>() : null;

            for (int i = 0; i < target; i++)
            {
                string name = GenerateOneInternal(rng, cfg, preferredSyllables, themeBias, flavor);

                if (forceSingleWord) name = ToSingleWord(name);
                if (normalizeCasing) name = NormalizeCase(name);

                name = optionalPrefix + name + optionalSuffix;

                if (name.Length < minLength || name.Length > maxLength)
                {
                    int retries = 0;
                    while ((name.Length < minLength || name.Length > maxLength) && retries < 8)
                    {
                        name = GenerateOneInternal(rng, cfg, preferredSyllables, themeBias, flavor);
                        if (forceSingleWord) name = ToSingleWord(name);
                        if (normalizeCasing) name = NormalizeCase(name);
                        name = optionalPrefix + name + optionalSuffix;
                        retries++;
                    }
                }

                if (ensureUniqueInBatch)
                {
                    int guard = 0;
                    while (used!.Contains(name) && guard < 12)
                    {
                        name = GenerateOneInternal(rng, cfg, preferredSyllables, themeBias, flavor);
                        if (forceSingleWord) name = ToSingleWord(name);
                        if (normalizeCasing) name = NormalizeCase(name);
                        name = optionalPrefix + name + optionalSuffix;
                        guard++;
                    }
                    used!.Add(name);
                }

                results.Add(name);
            }

            return results;
        }

        /// <summary>
        /// Generates a single name using a (possibly unknown) language code; on unknown, uses default.
        /// </summary>
        public static string GenerateOne(
            string langCode,
            int minLength = 4,
            int maxLength = 11,
            float preferredSyllables = 3f,
            int themeBias = 1,
            PhonemeFlavor flavor = PhonemeFlavor.Neutral,
            bool forceSingleWord = true,
            bool normalizeCasing = true,
            int seed = -1)
        {
            var code = ResolveCodeOrDefault(langCode);
            return GenerateOne(
                _langs[code],
                minLength, maxLength, preferredSyllables, themeBias, flavor,
                forceSingleWord, normalizeCasing, seed
            );
        }

        /// <summary>
        /// Generates a single name from a LangConfig (convenience).
        /// </summary>
        public static string GenerateOne(
            LangConfig cfg,
            int minLength = 4,
            int maxLength = 11,
            float preferredSyllables = 3f,
            int themeBias = 1,
            PhonemeFlavor flavor = PhonemeFlavor.Neutral,
            bool forceSingleWord = true,
            bool normalizeCasing = true,
            int seed = -1)
        {
            var rng = (seed >= 0) ? new Random(seed) : new Random();

            string name = GenerateOneInternal(rng, cfg, preferredSyllables, themeBias, flavor);
            if (forceSingleWord) name = ToSingleWord(name);
            if (normalizeCasing) name = NormalizeCase(name);

            int retries = 0;
            while ((name.Length < minLength || name.Length > maxLength) && retries < 8)
            {
                name = GenerateOneInternal(rng, cfg, preferredSyllables, themeBias, flavor);
                if (forceSingleWord) name = ToSingleWord(name);
                if (normalizeCasing) name = NormalizeCase(name);
                retries++;
            }

            return name;
        }

        /// <summary>
        /// Picks a random name from an existing generated list (optionally consume).
        /// </summary>
        public static string PickRandomFrom(IList<string> names, bool consume = false, int seed = -1)
        {
            if (names == null || names.Count == 0) return string.Empty;

            var rng = (seed >= 0) ? new Random(seed) : new Random();
            int idx = rng.Next(0, names.Count);

            if (!consume) return names[idx];

            if (names is List<string> list)
            {
                int last = list.Count - 1;
                string picked = list[idx];
                list[idx] = list[last];
                list.RemoveAt(last);
                return picked;
            }
            else
            {
                string picked = names[idx];
                names.RemoveAt(idx);
                return picked;
            }
        }

        /// <summary>
        /// Generate a single raw name string using RNG and language config.
        /// Applies per-language override for harsh-onset chance if provided.
        /// </summary>
        private static string GenerateOneInternal(
            Random rng,
            LangConfig cfg,
            float preferredSyllables,
            int themeBias,
            PhonemeFlavor flavor)
        {
            int syllables = SampleSyllableCount(rng, preferredSyllables);
            var sb = new StringBuilder(24);

            bool useTheme = themeBias > 0 && rng.Next(0, 6) < ClampInt(themeBias, 0, 5);

            // Resolve harsh chance (language override > global > 0)
            double harshChance = _defaults.HarshOnsetChance;
            if (cfg.HarshOnsetChanceOverride >= 0f)
                harshChance = cfg.HarshOnsetChanceOverride;

            for (int i = 0; i < syllables; i++)
            {
                string onset = Pick(rng, cfg.Onsets);
                string nucleus = Pick(rng, cfg.Nuclei);
                string coda = rng.NextDouble() < cfg.CodaUseProbability ? Pick(rng, cfg.Codas) : string.Empty;

                if (flavor == PhonemeFlavor.Harsher && rng.NextDouble() < harshChance)
                    onset = Pick(rng, cfg.HarshOnsets);
                else if (flavor == PhonemeFlavor.Softer && rng.NextDouble() < 0.35) // softening remains constant
                    coda = string.Empty;

                sb.Append(onset);
                sb.Append(nucleus);
                sb.Append(coda);

                if (useTheme && i == 0 && rng.NextDouble() < 0.25)
                    sb.Append(Pick(rng, cfg.ThematicMiddle));
            }

            if (useTheme && rng.NextDouble() < 0.4)
                sb.Insert(0, Pick(rng, cfg.ThematicStart));

            if (useTheme && rng.NextDouble() < 0.4)
                sb.Append(Pick(rng, cfg.ThematicEnd));

            string raw = sb.ToString();
            raw = CleanUp(raw, cfg);
            return raw;
        }

        /// <summary>
        /// Resolves provided code to a registered one or falls back to DefaultLangCode.
        /// Throws if no languages are registered at all.
        /// </summary>
        private static string ResolveCodeOrDefault(string langCode)
        {
            if (_langs.Count == 0)
                throw new InvalidOperationException("No languages registered in the generator.");

            if (!string.IsNullOrWhiteSpace(langCode) && _langs.ContainsKey(langCode))
                return langCode;

            if (!string.IsNullOrWhiteSpace(DefaultLangCode) && _langs.ContainsKey(DefaultLangCode))
                return DefaultLangCode;

            return _langs.Keys.First();
        }

        /// <summary>
        /// Pick a random element from the pool (safe for empty).
        /// </summary>
        private static string Pick(Random rng, string[] pool)
        {
            if (pool == null || pool.Length == 0) return string.Empty;
            return pool[rng.Next(0, pool.Length)];
        }

        /// <summary>
        /// Sample syllable count around preferred value (2–4) with soft randomness.
        /// </summary>
        private static int SampleSyllableCount(Random rng, float preferred)
        {
            float clamp = ClampFloat(preferred, 2f, 4f);
            double roll = rng.NextDouble();

            if (clamp < 2.5f)
                return roll < 0.70 ? 2 : 3;
            if (clamp < 3.5f)
                return roll < 0.60 ? 3 : (roll < 0.85 ? 2 : 4);
            return roll < 0.65 ? 4 : 3;
        }

        /// <summary>
        /// Normalize casing: upper first, lower rest (works for basic Cyrillic/Latin).
        /// </summary>
        private static string NormalizeCase(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return s;
            s = s.Trim();
            if (s.Length == 1) return char.ToUpper(s[0]).ToString();
            char first = char.ToUpper(s[0]);
            string rest = s.Substring(1).ToLower();
            return first + rest;
        }

        /// <summary>
        /// Convert string to a single word by removing spaces/hyphens/apostrophes.
        /// </summary>
        private static string ToSingleWord(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var forbid = new[] { ' ', '-', '—', '\t', '\n', '\r', '\'', '’', '‘', 'ˈ' };
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (Array.IndexOf(forbid, c) >= 0) continue;
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Light phonotactic cleanup and repeat collapsing.
        /// </summary>
        private static string CleanUp(string s, LangConfig cfg)
        {
            if (string.IsNullOrEmpty(s)) return s;

            s = CollapseRepeats(s, 2);

            foreach (var bad in cfg.IllegalClusters)
            {
                if (string.IsNullOrEmpty(bad) || bad.Length <= 1) continue;
                s = s.Replace(bad, bad.Substring(0, bad.Length - 1));
            }

            s = new string(s.Where(char.IsLetter).ToArray());
            s = CollapseRepeats(s, 2);
            return s;
        }

        /// <summary>
        /// Collapse runs of the same char beyond maxRun.
        /// </summary>
        private static string CollapseRepeats(string s, int maxRun)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new StringBuilder(s.Length);
            char prev = '\0';
            int run = 0;

            foreach (char c in s)
            {
                if (c == prev) run++;
                else { prev = c; run = 1; }

                if (run <= maxRun) sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Clamp integer to [min,max].
        /// </summary>
        private static int ClampInt(int v, int min, int max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        /// <summary>
        /// Clamp float to [min,max].
        /// </summary>
        private static float ClampFloat(float v, float min, float max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }
    }
}
