using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MasterServerToolkit.Json;
using MasterServerToolkit.Logging;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class MstArgs
    {
        private const string ConfigFileArgName = "-mstConfigFile";
        private const string ConfigImportDirective = "@import";
        private string[] _args = Array.Empty<string>();

        private struct ConfigArgumentSource
        {
            public string FilePath;
            public int LineNumber;

            public ConfigArgumentSource(string filePath, int lineNumber)
            {
                FilePath = filePath;
                LineNumber = lineNumber;
            }

            public string Location => $"{FilePath}:{LineNumber}";
        }

        private struct ConfigImport
        {
            public string Path;
            public ConfigArgumentSource Source;

            public ConfigImport(string path, ConfigArgumentSource source)
            {
                Path = path;
                Source = source;
            }
        }

        /// <inheritdoc cref="MstArgNames.StartMaster" />
        public bool StartMaster { get; private set; }

        /// <inheritdoc cref="MstArgNames.SpawnerStart" />
        public bool StartSpawner { get; private set; }

        /// <inheritdoc cref="MstArgNames.StartClientConnection" />
        public bool StartClientConnection { get; private set; }

        /// <inheritdoc cref="MstArgNames.MasterIp" />
        public string MasterIp { get; private set; }

        /// <inheritdoc cref="MstArgNames.MasterPort" />
        public int MasterPort { get; private set; }

        /// <inheritdoc cref="MstArgNames.RoomName" />
        public string RoomName { get; private set; }

        /// <inheritdoc cref="MstArgNames.RoomTitle" />
        public string RoomTitle { get; private set; }

        /// <inheritdoc cref="MstArgNames.RoomIsPrivate" />
        public bool RoomIsPrivate { get; private set; }

        /// <inheritdoc cref="MstArgNames.RoomIp" />
        public string RoomIp { get; private set; }

        /// <inheritdoc cref="MstArgNames.RoomRedirectIp" />
        public string RoomRedirectIp { get; private set; }

        /// <inheritdoc cref="MstArgNames.RoomPort" />
        public ushort RoomPort { get; private set; }

        /// <inheritdoc cref="MstArgNames.RoomRedirectPort" />
        public ushort RoomRedirectPort { get; private set; }

        /// <inheritdoc cref="MstArgNames.RoomClientUseSecure" />
        public bool RoomClientUseSecure { get; private set; }

        /// <inheritdoc cref="MstArgNames.SpawnerRoomDefaultPort" />
        public int SpawnerRoomDefaultPort { get; private set; }

        /// <inheritdoc cref="MstArgNames.SpawnerRoomDefaultRedirectPort" />
        public int SpawnerRoomDefaultRedirectPort { get; private set; }

        /// <inheritdoc cref="MstArgNames.RoomMaxConnections" />
        public ushort RoomMaxConnections { get; private set; }

        /// <inheritdoc cref="MstArgNames.RoomExecutablePath" />
        public string RoomExecutablePath { get; private set; }

        /// <inheritdoc cref="MstArgNames.RoomRegion" />
        public string RoomRegion { get; private set; }

        /// <inheritdoc cref="MstArgNames.RoomPassword" />
        public string RoomPassword { get; private set; }

        /// <inheritdoc cref="MstArgNames.SpawnerTaskId" />
        public int SpawnTaskId { get; private set; }

        /// <inheritdoc cref="MstArgNames.SpawnerTaskUniqueCode" />
        public string SpawnTaskUniqueCode { get; private set; }

        /// <inheritdoc cref="MstArgNames.SpawnerMaxProcesses" />
        public int SpawnerMaxProcesses { get; private set; }

        /// <inheritdoc cref="MstArgNames.RoomOnlineScene" />
        public string LoadScene { get; private set; }

        /// <inheritdoc cref="MstArgNames.DatabaseConnectionString" />
        public string DatabaseConnectionString { get; private set; }

        /// <inheritdoc cref="MstArgNames.DatabaseConfiguration" />
        public string DatabaseConfiguration { get; private set; }

        /// <inheritdoc cref="MstArgNames.DatabaseProvider" />
        public string DatabaseProvider { get; private set; }

        /// <inheritdoc cref="MstArgNames.DatabaseAutoCloseConnection" />
        public string DatabaseAutoCloseConnection { get; private set; }

        /// <inheritdoc cref="MstArgNames.DatabaseLanguageType" />
        public string DatabaseLanguageType { get; private set; }

        /// <inheritdoc cref="MstArgNames.LobbyId" />
        public int LobbyId { get; private set; }

        /// <inheritdoc cref="MstArgNames.UseSecure" />
        public bool UseSecure { get; private set; }

        /// <inheritdoc cref="MstArgNames.CertificatePath" />
        public string CertificatePath { get; private set; }

        /// <inheritdoc cref="MstArgNames.CertificatePassword" />
        public string CertificatePassword { get; private set; }

        /// <inheritdoc cref="MstArgNames.SecurityKeyRingFile" />
        public string SecurityKeyRingFile { get; private set; }

        /// <inheritdoc cref="MstArgNames.PermissionCredentials" />
        public string PermissionCredentials { get; private set; }

        /// <inheritdoc cref="MstArgNames.UseDevMode" />
        public bool UseDevMode { get; private set; }

        /// <inheritdoc cref="MstArgNames.TargetFrameRate" />
        public int TargetFrameRate { get; private set; }

        /// <inheritdoc cref="MstArgNames.WebAddress" />
        public string WebAddress { get; private set; }

        /// <inheritdoc cref="MstArgNames.WebPort" />
        public int WebPort { get; private set; }

        /// <inheritdoc cref="MstArgNames.AdminId" />
        public string AdminId { get; private set; }

        /// <inheritdoc cref="MstArgNames.AdminUsername" />
        public string AdminUsername { get; private set; }

        /// <inheritdoc cref="MstArgNames.LogFilePath" />
        public string LogFileDir { get; private set; }

        /// <inheritdoc cref="MstArgNames.LogFilePath" />
        public string LogFilePath { get; private set; }

        /// <inheritdoc cref="MstArgNames.LogFileMinLevel" />
        public LogLevel LogFileMinLevel { get; private set; }

        /// <inheritdoc cref="MstArgNames.LogFileAppend" />
        public bool LogFileAppend { get; private set; }

        /// <inheritdoc cref="MstArgNames.LogUnityMessages" />
        public bool LogUnityMessages { get; private set; }

        /// <inheritdoc cref="MstArgNames.LogChannels" />
        public string LogChannels { get; private set; }

        /// <inheritdoc cref="MstArgNames.ConsoleLogChannels" />
        public string ConsoleLogChannels { get; private set; }

        /// <inheritdoc cref="MstArgNames.DefaultLanguage" />
        public string DefaultLanguage { get; private set; }

        /// <summary>
        /// Gets or sets the command-line argument names used by this parsed argument set.
        /// </summary>
        public MstArgNames Names { get; private set; }


        public MstArgs()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            ParseArguments();
#endif
            Names = new MstArgNames();

            StartMaster = AsBool(Names.StartMaster, false);
            StartClientConnection = AsBool(Names.StartClientConnection, false);

            MasterPort = AsInt(Names.MasterPort, 5000);
            MasterIp = AsString(Names.MasterIp, "127.0.0.1");

            WebAddress = AsString(Names.WebAddress, "127.0.0.1");
            WebPort = AsInt(Names.WebPort, 8080);
            AdminId = AsString(Names.AdminId, "");
            AdminUsername = AsString(Names.AdminUsername, "admin");

            RoomName = AsString(Names.RoomName);
            RoomTitle = AsString(Names.RoomTitle);
            RoomIp = AsString(Names.RoomIp, "127.0.0.1");
            RoomPort = (ushort)AsInt(Names.RoomPort, 7777);
            RoomRedirectIp = AsString(Names.RoomRedirectIp, "192.168.0.0");
            RoomRedirectPort = (ushort)AsInt(Names.RoomRedirectPort, 7777);
            RoomClientUseSecure = AsBool(Names.RoomClientUseSecure, false);
            RoomExecutablePath = AsString(Names.RoomExecutablePath);
            RoomRegion = AsString(Names.RoomRegion);
            RoomMaxConnections = (ushort)AsInt(Names.RoomMaxConnections, 10);
            RoomIsPrivate = AsBool(Names.RoomIsPrivate, false);
            RoomPassword = AsString(Names.RoomPassword);

            StartSpawner = AsBool(Names.SpawnerStart, false);
            SpawnTaskId = AsInt(Names.SpawnerTaskId, -1);
            SpawnTaskUniqueCode = AsString(Names.SpawnerTaskUniqueCode);
            SpawnerRoomDefaultPort = AsInt(Names.SpawnerRoomDefaultPort, 1500);
            SpawnerRoomDefaultRedirectPort = AsInt(Names.SpawnerRoomDefaultRedirectPort, -1);
            SpawnerMaxProcesses = AsInt(Names.SpawnerMaxProcesses, 0);

            LoadScene = AsString(Names.RoomOnlineScene);

            DatabaseConnectionString = AsString(Names.DatabaseConnectionString);
            DatabaseConfiguration = AsString(Names.DatabaseConfiguration);
            DatabaseProvider = AsString(Names.DatabaseProvider);

            LobbyId = AsInt(Names.LobbyId);

            UseSecure = AsBool(Names.UseSecure, false);
            CertificatePath = AsString(Names.CertificatePath);
            CertificatePassword = AsString(Names.CertificatePassword);
            SecurityKeyRingFile = AsString(
                Names.SecurityKeyRingFile,
                "Configs/mst-security.keys.json");
            PermissionCredentials = AsString(Names.PermissionCredentials);

            UseDevMode = AsBool(Names.UseDevMode, false);
            TargetFrameRate = AsInt(Names.TargetFrameRate, 60);
            DefaultLanguage = AsString(Names.DefaultLanguage);
            LogFilePath = AsString(Names.LogFilePath);
            LogFileDir = LogFilePath;
            LogFileMinLevel = AsEnum(Names.LogFileMinLevel, LogLevel.Info);
            LogFileAppend = AsBool(Names.LogFileAppend, false);
            LogUnityMessages = AsBool(Names.LogUnityMessages, false);
            LogChannels = AsString(Names.LogChannels, MasterServerToolkit.Logging.LogChannels.System);
            ConsoleLogChannels = AsString(Names.ConsoleLogChannels, MasterServerToolkit.Logging.LogChannels.System);
        }

        private void ParseArguments()
        {
#if !UNITY_EDITOR
            if (UnityEngine.Application.isMobilePlatform)
            {
                return;
            }
#endif
            if (_args.Length > 0)
                return;

            _args = Environment.GetCommandLineArgs();

            // Android fix
            if (_args == null)
            {
                _args = Array.Empty<string>();
            }

            List<string> newArgs = new List<string>();
            newArgs.AddRange(_args);

            //Load from .env
            foreach (DictionaryEntry env in Environment.GetEnvironmentVariables())
            {
                newArgs.Add("-" + (string)env.Key);
                newArgs.Add((string)env.Value);
            }

            _args = newArgs.ToArray();

            string configFileArg = AsString(ConfigFileArgName);
            string path = ResolveAppConfigFilePath(configFileArg);

            if (!string.IsNullOrWhiteSpace(configFileArg) && !File.Exists(path))
            {
                throw new FileNotFoundException($"MST config file was explicitly specified but was not found: {path}", path);
            }

            var loadedConfigFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var importArgumentSources = new Dictionary<string, ConfigArgumentSource>(StringComparer.Ordinal);

            LoadConfigFile(path, newArgs, loadedConfigFiles, importArgumentSources, true);

            _args = newArgs.ToArray();
        }

        private void LoadConfigFile(string path, List<string> args, HashSet<string> loadedConfigFiles,
            Dictionary<string, ConfigArgumentSource> importArgumentSources, bool isMainConfig)
        {
            path = NormalizeConfigFilePath(path);

            if (!loadedConfigFiles.Add(path))
                return;

            string[] lines = File.ReadAllLines(path);

            if (lines == null || lines.Length == 0)
                return;

            var fileArgumentSources = new Dictionary<string, ConfigArgumentSource>(StringComparer.Ordinal);
            var imports = new List<ConfigImport>();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int lineNumber = i + 1;

                if (IsIgnoredConfigLine(line))
                    continue;

                var source = new ConfigArgumentSource(path, lineNumber);

                if (IsImportDirective(line))
                {
                    if (TryParseImportDirective(line, out string importPath))
                    {
                        imports.Add(new ConfigImport(importPath, source));
                    }
                    else
                    {
                        Logs.Warn($"Invalid MST config import directive ignored in {source.Location}: {line.Trim()}");
                    }

                    continue;
                }

                var kvp = Parse(line, "=");

                if (string.IsNullOrEmpty(kvp.Key))
                    continue;

                AddConfigArgument(args, kvp, source, fileArgumentSources, importArgumentSources, isMainConfig);
            }

            foreach (ConfigImport import in imports)
            {
                string importPath = ResolveImportedConfigFilePath(path, import.Path);

                if (!File.Exists(importPath))
                {
                    Logs.Warn($"MST config import \"{import.Path}\" was not found and will be skipped. Source: {import.Source.Location}. Resolved path: {importPath}");
                    continue;
                }

                LoadConfigFile(importPath, args, loadedConfigFiles, importArgumentSources, false);
            }
        }

        private void AddConfigArgument(List<string> args, KeyValuePair<string, string> kvp, ConfigArgumentSource source,
            Dictionary<string, ConfigArgumentSource> fileArgumentSources,
            Dictionary<string, ConfigArgumentSource> importArgumentSources, bool isMainConfig)
        {
            if (fileArgumentSources.TryGetValue(kvp.Key, out ConfigArgumentSource existingFileSource))
            {
                WarnDuplicateConfigArgument(kvp.Key, source, existingFileSource);
                return;
            }

            fileArgumentSources[kvp.Key] = source;

            if (!isMainConfig)
            {
                if (importArgumentSources.TryGetValue(kvp.Key, out ConfigArgumentSource existingImportSource))
                {
                    WarnDuplicateConfigArgument(kvp.Key, source, existingImportSource);
                    return;
                }

                importArgumentSources[kvp.Key] = source;
            }

            if (!ContainsKey(args, kvp.Key))
            {
                args.Add(kvp.Key);
                args.Add(kvp.Value);
            }
        }

        private static void WarnDuplicateConfigArgument(string key, ConfigArgumentSource ignoredSource, ConfigArgumentSource existingSource)
        {
            Logs.Warn($"Duplicate MST config parameter \"{key}\" ignored in {ignoredSource.Location}. First value was defined in {existingSource.Location}.");
        }

        private KeyValuePair<string, string> Parse(string input, string splitter)
        {
            int splitterIndex = input.IndexOf(splitter);

            if (splitterIndex >= 0)
            {
                string key = input.Substring(0, splitterIndex);
                string value = input.Substring(splitterIndex + 1);
                return new KeyValuePair<string, string>(key, value);
            }

            return default;
        }

        private static bool IsIgnoredConfigLine(string line)
        {
            return string.IsNullOrWhiteSpace(line) || IsComment(line);
        }

        private static bool IsComment(string line)
        {
            return line.TrimStart().StartsWith("#");
        }

        private static bool IsImportDirective(string line)
        {
            string trimmed = line.TrimStart();

            if (!trimmed.StartsWith(ConfigImportDirective, StringComparison.Ordinal))
                return false;

            return trimmed.Length == ConfigImportDirective.Length ||
                   char.IsWhiteSpace(trimmed[ConfigImportDirective.Length]);
        }

        private static bool TryParseImportDirective(string line, out string importPath)
        {
            importPath = string.Empty;

            string trimmed = line.Trim();
            string remainder = trimmed.Substring(ConfigImportDirective.Length).TrimStart();

            if (string.IsNullOrEmpty(remainder))
                return false;

            char quote = remainder[0];

            if (quote != '"' && quote != '\'')
                return false;

            int closingQuoteIndex = remainder.IndexOf(quote, 1);

            if (closingQuoteIndex <= 1)
                return false;

            string tail = remainder.Substring(closingQuoteIndex + 1).TrimStart();

            if (!string.IsNullOrEmpty(tail) && !tail.StartsWith("#"))
                return false;

            importPath = remainder.Substring(1, closingQuoteIndex - 1);
            return !string.IsNullOrWhiteSpace(importPath);
        }

        /// <summary>
        /// Searches for keys by filter
        /// </summary>
        /// <param name="keysFilter"></param>
        /// <returns></returns>
        public string[] FindKeys(string keysFilter)
        {
            return _args.Where(i => i.StartsWith(keysFilter)).ToArray();
        }

        /// <summary>
        /// Returns the default MST application.cfg path for the project or a supplied build root, creating the file when it is missing.
        /// </summary>
        /// <param name="rootPath">Optional build root. When empty, the current Unity project root is used.</param>
        /// <returns>Absolute path to the default application.cfg file.</returns>
        public string AppConfigFile(string rootPath = "")
        {
            string path;
            string gameDirectory = Path.GetDirectoryName(Application.dataPath);

            if (string.IsNullOrEmpty(rootPath))
            {
                path = Path.Combine(gameDirectory, "application.cfg");
            }
            else
            {
                path = Path.Combine(rootPath, "application.cfg");
            }

            if (!File.Exists(path))
            {
                using (var file = File.Create(path))
                    file.Close();
            }

            return path;
        }

        private string ResolveAppConfigFilePath(string configFilePath)
        {
            if (string.IsNullOrWhiteSpace(configFilePath))
                return AppConfigFile();

            if (Path.IsPathRooted(configFilePath))
                return NormalizeConfigFilePath(configFilePath);

            string gameDirectory = Path.GetDirectoryName(Application.dataPath);
            return NormalizeConfigFilePath(Path.Combine(gameDirectory, configFilePath));
        }

        private static string ResolveImportedConfigFilePath(string parentConfigFilePath, string importPath)
        {
            if (Path.IsPathRooted(importPath))
                return NormalizeConfigFilePath(importPath);

            string parentDirectory = Path.GetDirectoryName(parentConfigFilePath) ?? string.Empty;
            return NormalizeConfigFilePath(Path.Combine(parentDirectory, importPath));
        }

        private static string NormalizeConfigFilePath(string path)
        {
            return Path.GetFullPath(path);
        }

        /// <summary>
        /// Returns all parsed MST arguments as a single command-line-style string.
        /// </summary>
        /// <returns>Parsed arguments joined with spaces.</returns>
        public override string ToString()
        {
            var redactedArguments = new List<string>(_args.Length);

            for (int i = 0; i < _args.Length; i++)
            {
                string argument = _args[i] ?? string.Empty;
                int separatorIndex = argument.IndexOf('=');
                string argumentName = separatorIndex >= 0 ? argument.Substring(0, separatorIndex) : argument;

                if (!IsSensitiveArgumentName(argumentName))
                {
                    redactedArguments.Add(argument);
                    continue;
                }

                if (separatorIndex >= 0)
                {
                    redactedArguments.Add($"{argumentName}=[REDACTED]");
                    continue;
                }

                redactedArguments.Add(argumentName);

                if (i + 1 < _args.Length)
                {
                    redactedArguments.Add("[REDACTED]");
                    i++;
                }
            }

            return string.Join(" ", redactedArguments);
        }

        /// <summary>
        /// Formats process arguments while masking values of known sensitive keys.
        /// </summary>
        public string ToRedactedString(MstProperties arguments, string itemsSeparator = " ", string kvpSeparator = " ")
        {
            if (arguments == null)
                return string.Empty;

            var redacted = arguments.ToDictionary();

            foreach (string key in redacted.Keys.ToArray())
            {
                if (IsSensitiveArgumentName(key))
                    redacted[key] = "[REDACTED]";
            }

            return new MstProperties(redacted).ToReadableString(itemsSeparator, kvpSeparator);
        }

        private static bool IsSensitiveArgumentName(string argumentName)
        {
            if (string.IsNullOrWhiteSpace(argumentName))
                return false;

            string normalized = argumentName
                .TrimStart('-')
                .Replace("-", string.Empty)
                .Replace("_", string.Empty)
                .Replace(".", string.Empty)
                .ToLowerInvariant();

            return normalized.Contains("password") ||
                   normalized.Contains("secret") ||
                   normalized.Contains("token") ||
                   normalized.Contains("credential") ||
                   normalized.Contains("connectionstring") ||
                   normalized.Contains("apikey") ||
                   normalized.Contains("privatekey");
        }

        /// <summary>
        /// Extracts a string value for command line arguments provided
        /// </summary>
        /// <param name="argName"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public string AsString(string argName, string defaultValue = "")
        {
            return TryGetValue(argName, out string value) ? value : defaultValue;
        }

        /// <summary>
        /// Tries to parse a command-line or config argument value as JSON.
        /// </summary>
        /// <param name="argName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool TryGetJson(string argName, out MstJson value)
        {
            value = null;

            if (!TryGetValue(argName, out string rawValue) || string.IsNullOrWhiteSpace(rawValue))
                return false;

            if (TryParseJson(rawValue, out value))
                return true;

            try
            {
                string unescapedValue = Uri.UnescapeDataString(rawValue.Trim());

                return !string.Equals(rawValue.Trim(), unescapedValue, StringComparison.Ordinal) &&
                       TryParseJson(unescapedValue, out value);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Extracts a JSON value for command line arguments provided.
        /// </summary>
        /// <param name="argName"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public MstJson AsJson(string argName, MstJson defaultValue = null)
        {
            return TryGetJson(argName, out MstJson value) ? value : defaultValue;
        }

        private static bool TryParseJson(string rawValue, out MstJson value)
        {
            value = null;

            if (string.IsNullOrWhiteSpace(rawValue))
                return false;

            try
            {
                string normalizedValue = rawValue.Trim();

                if (!MstJson.IsJson(normalizedValue))
                    return false;

                var json = new MstJson(normalizedValue);

                if (json.IsNull)
                    return false;

                value = json;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Extracts an int value for command line arguments provided.
        /// </summary>
        /// <param name="argName"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public int AsInt(string argName, int defaultValue = -1)
        {
            if (!TryGetValue(argName, out string value))
                return defaultValue;

            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result)
                ? result
                : defaultValue;
        }

        /// <summary>
        /// Extracts a float value for command line arguments provided.
        /// </summary>
        /// <param name="argName"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public float AsFloat(string argName, float defaultValue = -1)
        {
            if (!TryGetValue(argName, out string value))
                return defaultValue;

            string normalizedValue = value.Trim().Replace(',', '.');

            return float.TryParse(normalizedValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float result)
                ? result
                : defaultValue;
        }

        /// <summary>
        /// Extracts a bool value for command line arguments provided
        /// </summary>
        /// <param name="argName"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public bool AsBool(string argName, bool defaultValue = false)
        {
            if (!TryGetValue(argName, out string value))
                return defaultValue;

            if (string.IsNullOrWhiteSpace(value))
                return true;

            if (bool.TryParse(value, out bool result))
                return result;

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
                return intValue != 0;

            return defaultValue;
        }

        /// <summary>
        /// Extracts an enum value for command line arguments provided.
        /// </summary>
        /// <param name="argName">Argument name, such as -mstDatabaseProvider.</param>
        /// <param name="defaultValue">Value returned when the argument is missing or cannot be parsed.</param>
        /// <typeparam name="T">Enum type to parse.</typeparam>
        /// <returns>Parsed enum value or the provided default.</returns>
        public T AsEnum<T>(string argName, T defaultValue = default) where T : struct, Enum
        {
            if (IsProvided(argName) && Enum.TryParse(AsString(argName), true, out T value))
            {
                return value;
            }
            else
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Checks whether the given command-line/config argument or derived environment variable is provided.
        /// </summary>
        /// <param name="argName"></param>
        /// <returns></returns>
        public bool IsProvided(string argName)
        {
            return TryGetEnvironmentValue(argName, out _) || ContainsKey(_args, argName);
        }

        private bool TryGetValue(string argName, out string value)
        {
            value = string.Empty;

            if (string.IsNullOrWhiteSpace(argName))
                return false;

            if (TryGetEnvironmentValue(argName, out value))
                return true;

            string inlinePrefix = $"{argName}=";

            for (int i = 0; i < _args.Length; i++)
            {
                string arg = _args[i];

                if (arg.Equals(argName, StringComparison.Ordinal))
                {
                    if (i + 1 >= _args.Length || LooksLikeKey(_args[i + 1]))
                        return true;

                    value = _args[i + 1];
                    return true;
                }

                if (arg.StartsWith(inlinePrefix, StringComparison.Ordinal))
                {
                    value = arg.Substring(inlinePrefix.Length);
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetEnvironmentValue(string argName, out string value)
        {
            value = string.Empty;

            string environmentKey = ToEnvironmentKey(argName);

            if (string.IsNullOrWhiteSpace(environmentKey))
                return false;

            string environmentValue = Environment.GetEnvironmentVariable(environmentKey);

            if (environmentValue == null)
                return false;

            value = environmentValue;
            return true;
        }

        private static bool ContainsKey(IList<string> args, string argName)
        {
            if (string.IsNullOrWhiteSpace(argName))
                return false;

            string inlinePrefix = $"{argName}=";
            return args.Any(arg => arg.Equals(argName, StringComparison.Ordinal) ||
                                   arg.StartsWith(inlinePrefix, StringComparison.Ordinal));
        }

        private static bool LooksLikeKey(string value)
        {
            return value.StartsWith("-") &&
                   !float.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out _);
        }

        private static string ToEnvironmentKey(string argName)
        {
            if (string.IsNullOrWhiteSpace(argName))
                return string.Empty;

            string normalizedArgName = argName.TrimStart('-');
            var builder = new StringBuilder(normalizedArgName.Length * 2);

            for (int i = 0; i < normalizedArgName.Length; i++)
            {
                char current = normalizedArgName[i];

                if (current == '.' || current == '-' || char.IsWhiteSpace(current))
                {
                    AppendSeparator(builder);
                    continue;
                }

                if (char.IsUpper(current) && ShouldAddCamelCaseSeparator(normalizedArgName, i))
                    AppendSeparator(builder);

                builder.Append(char.ToUpperInvariant(current));
            }

            return builder.ToString().Trim('_');
        }

        private static bool ShouldAddCamelCaseSeparator(string value, int index)
        {
            if (index == 0)
                return false;

            char previous = value[index - 1];

            if (previous == '.' || previous == '-' || char.IsWhiteSpace(previous) || previous == '_')
                return false;

            if (char.IsLower(previous) || char.IsDigit(previous))
                return true;

            return index + 1 < value.Length && char.IsLower(value[index + 1]);
        }

        private static void AppendSeparator(StringBuilder builder)
        {
            if (builder.Length > 0 && builder[builder.Length - 1] != '_')
                builder.Append('_');
        }
    }
}
