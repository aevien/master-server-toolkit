using MasterServerToolkit.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MasterServerToolkit.CommandTerminal
{
    public enum TerminalState
    {
        Close,
        OpenSmall,
        OpenFull
    }

    public class Terminal : SingletonBehaviour<Terminal>
    {
        [Header("Terminal Backend")]
        [SerializeField, Min(1)]
        [Tooltip("Maximum number of log entries retained in memory. When the limit is exceeded, the oldest entries are removed. Minimum is 1.")]
        private int bufferCapacity = 5000;

        [SerializeField, Min(1)]
        [Tooltip("Default number of log entries returned by backend read and search methods that do not specify a page size. It is clamped from 1 to Buffer Capacity; TerminalUI calculates its own visible page size.")]
        private int maxVisibleLines = 100;

        [SerializeField]
        [Tooltip("When enabled, messages emitted through Unity's application log callback are copied into the terminal buffer.")]
        private bool captureUnityLogs = true;

        [SerializeField]
        [Tooltip("When enabled, the terminal registers its standard commands such as help, clear, echo, logs, time, and quit during runtime initialization.")]
        private bool registerBuiltInCommands = true;

        private bool initialized;
        private int initializedGeneration = -1;
        private bool unityLogSubscribed;
        private bool builtInsRegistered;
        private string searchQuery = string.Empty;
        private int searchPageIndex = -1;
        private bool searchFollowsLatest = true;
        private CommandLog subscribedBuffer;
        private CommandShell subscribedShell;
        private static int runtimeGeneration;

        public event Action<Terminal> Changed;
        public event Action<LogItem> LogEntryAdded;
        public event Action CommandsChanged;
        public event Action<TerminalSearchResult> SearchChanged;

        public static CommandLog Buffer { get; private set; }
        public static CommandShell Shell { get; private set; } = new CommandShell();
        public static CommandHistory History { get; private set; } = new CommandHistory();
        public static CommandAutocomplete Autocomplete { get; private set; } = new CommandAutocomplete();
        public static bool IssuedError => Shell != null && !string.IsNullOrWhiteSpace(Shell.IssuedErrorMessage);

        public CommandLog GetLogBuffer()
        {
            InitializeRuntime();
            return Buffer;
        }

        public CommandShell GetCommandShell()
        {
            InitializeRuntime();
            return Shell;
        }

        public CommandHistory GetCommandHistory()
        {
            InitializeRuntime();
            return History;
        }

        public CommandAutocomplete GetCommandAutocomplete()
        {
            InitializeRuntime();
            return Autocomplete;
        }

        public int BufferCapacity => bufferCapacity;
        public int MaxVisibleLines => maxVisibleLines;
        public string SearchQuery => searchQuery;
        public int SearchPageIndex => searchPageIndex;
        public bool HasActiveSearch => !string.IsNullOrWhiteSpace(searchQuery);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticRuntimeState()
        {
            runtimeGeneration++;
            Buffer = null;
            Shell = new CommandShell();
            History = new CommandHistory();
            Autocomplete = new CommandAutocomplete();

            _instance = null;
            _wasCreated = false;
            _creationHasPendingConfig = false;
            _creationIsGlobal = true;
        }

        protected override void Awake()
        {
            base.Awake();

            if (isNowDestroying)
                return;

            InitializeRuntime();
        }

        protected virtual void OnEnable()
        {
            InitializeRuntime();

            if (isNowDestroying)
                return;

            SubscribeUnityLog();
        }

        protected override void OnDestroy()
        {
            UnsubscribeUnityLog();
            UnsubscribeRuntimeEvents();
            base.OnDestroy();
        }

        protected virtual void OnValidate()
        {
            bufferCapacity = Mathf.Max(1, bufferCapacity);
            maxVisibleLines = Mathf.Clamp(maxVisibleLines, 1, bufferCapacity);

            if (Buffer != null)
                Buffer.SetCapacity(bufferCapacity);
        }

        public bool AddCommand(string name,
                               Action<CommandArg[]> proc,
                               int minArgCount = 0,
                               int maxArgCount = -1,
                               string help = "",
                               bool replaceExisting = false)
        {
            InitializeRuntime();
            return Shell.AddCommand(name, proc, minArgCount, maxArgCount, help, replaceExisting);
        }

        public bool RemoveCommand(string name)
        {
            InitializeRuntime();
            return Shell.RemoveCommand(name);
        }

        public bool SubmitCommand(string commandLine)
        {
            InitializeRuntime();

            if (string.IsNullOrWhiteSpace(commandLine))
                return false;

            Log(TerminalLogType.Input, commandLine);
            Shell.RunCommand(commandLine);
            History.Push(commandLine);

            if (IssuedError)
            {
                Log(TerminalLogType.Error, "Error: {0}", Shell.IssuedErrorMessage);
                return false;
            }

            return true;
        }

        public void ClearLog()
        {
            InitializeRuntime();
            Buffer.Clear();
        }

        public IReadOnlyList<LogItem> GetVisibleEntries()
        {
            InitializeRuntime();

            if (HasActiveSearch)
                return GetCurrentSearchResult().Entries;

            return Buffer.Latest(maxVisibleLines);
        }

        public IReadOnlyList<LogItem> GetVisibleEntries(int maxCount)
        {
            InitializeRuntime();

            if (HasActiveSearch)
                return GetCurrentSearchResult(maxCount).Entries;

            return Buffer.Latest(maxCount);
        }

        public IReadOnlyList<LogItem> GetLatestEntries(int maxCount, int offsetFromLatest = 0)
        {
            InitializeRuntime();
            return Buffer.Latest(maxCount, offsetFromLatest);
        }

        public string GetVisibleText(bool includeTimestamp = false, bool includeType = false)
        {
            return FormatEntries(GetVisibleEntries(), includeTimestamp, includeType);
        }

        public string GetVisibleText(int maxCount, bool includeTimestamp = false, bool includeType = false)
        {
            return FormatEntries(GetVisibleEntries(maxCount), includeTimestamp, includeType);
        }

        public TerminalSearchResult SetSearch(string query)
        {
            return SetSearch(query, maxVisibleLines);
        }

        public TerminalSearchResult SetSearch(string query, int pageSize)
        {
            InitializeRuntime();
            searchQuery = query ?? string.Empty;
            searchPageIndex = -1;
            searchFollowsLatest = true;
            TerminalSearchResult result = GetCurrentSearchResult(pageSize);
            SearchChanged?.Invoke(result);
            Changed?.Invoke(this);
            return result;
        }

        public TerminalSearchResult ClearSearch()
        {
            return SetSearch(string.Empty);
        }

        public TerminalSearchResult SetSearchPage(int pageIndex)
        {
            return SetSearchPage(pageIndex, maxVisibleLines);
        }

        public TerminalSearchResult SetSearchPage(int pageIndex, int pageSize)
        {
            InitializeRuntime();

            if (!HasActiveSearch)
                return new TerminalSearchResult();

            TerminalSearchResult result = Buffer.Search(searchQuery, pageIndex, pageSize);
            searchPageIndex = result.PageIndex;
            searchFollowsLatest = result.IsLastPage;
            SearchChanged?.Invoke(result);
            Changed?.Invoke(this);
            return result;
        }

        public TerminalSearchResult NextSearchPage()
        {
            return NextSearchPage(maxVisibleLines);
        }

        public TerminalSearchResult NextSearchPage(int pageSize)
        {
            TerminalSearchResult current = GetCurrentSearchResult(pageSize);

            if (!current.HasQuery || current.IsLastPage)
                return current;

            return SetSearchPage(current.PageIndex + 1, pageSize);
        }

        public TerminalSearchResult PreviousSearchPage()
        {
            return PreviousSearchPage(maxVisibleLines);
        }

        public TerminalSearchResult PreviousSearchPage(int pageSize)
        {
            TerminalSearchResult current = GetCurrentSearchResult(pageSize);

            if (!current.HasQuery || current.IsFirstPage)
                return current;

            return SetSearchPage(current.PageIndex - 1, pageSize);
        }

        public TerminalSearchResult GetCurrentSearchResult()
        {
            return GetCurrentSearchResult(maxVisibleLines);
        }

        public TerminalSearchResult GetCurrentSearchResult(int pageSize)
        {
            InitializeRuntime();

            if (!HasActiveSearch)
                return new TerminalSearchResult();

            TerminalSearchResult result = Buffer.Search(searchQuery, searchPageIndex, pageSize);
            searchPageIndex = result.PageIndex;
            return result;
        }

        public string[] CompleteCommand(string commandLine, out string completedText)
        {
            InitializeRuntime();
            completedText = commandLine ?? string.Empty;
            return Autocomplete.Complete(ref completedText);
        }

        public void SetBufferCapacity(int capacity)
        {
            InitializeRuntime();
            bufferCapacity = Mathf.Max(1, capacity);
            maxVisibleLines = Mathf.Clamp(maxVisibleLines, 1, bufferCapacity);
            Buffer.SetCapacity(bufferCapacity);
            Changed?.Invoke(this);
            SearchChanged?.Invoke(GetCurrentSearchResult());
        }

        public void SetMaxVisibleLines(int value)
        {
            InitializeRuntime();
            maxVisibleLines = Mathf.Clamp(value, 1, bufferCapacity);
            Changed?.Invoke(this);
            SearchChanged?.Invoke(GetCurrentSearchResult());
        }

        public static void Log(string format, params object[] message)
        {
            Log(TerminalLogType.ShellMessage, format, message);
        }

        public static void Log(TerminalLogType type, string format, params object[] message)
        {
            if (Buffer == null)
                return;

            Buffer.HandleLog(FormatMessage(format, message), type);
        }

        public static string FormatEntries(IEnumerable<LogItem> entries, bool includeTimestamp = false, bool includeType = false)
        {
            if (entries == null)
                return string.Empty;

            var builder = new StringBuilder();

            foreach (LogItem entry in entries)
            {
                if (builder.Length > 0)
                    builder.AppendLine();

                if (includeTimestamp)
                    builder.Append(entry.timestamp.ToString("HH:mm:ss")).Append(' ');

                if (includeType)
                    builder.Append('[').Append(entry.type).Append("] ");

                builder.Append(entry.message);
            }

            return builder.ToString();
        }

        private void InitializeRuntime()
        {
            if (isNowDestroying)
                return;

            if (_instance == null)
            {
                _instance = this;
                _wasCreated = true;

                if (isGlobal && Application.isPlaying)
                    DontDestroyOnLoad(this);
            }
            else if (_instance != this)
            {
                isNowDestroying = true;
                Destroy(gameObject);
                return;
            }

            if (initialized && initializedGeneration == runtimeGeneration)
                return;

            if (initialized)
            {
                UnsubscribeUnityLog();
                UnsubscribeRuntimeEvents();
                builtInsRegistered = false;
                searchQuery = string.Empty;
                searchPageIndex = -1;
                searchFollowsLatest = true;
            }

            bufferCapacity = Mathf.Max(1, bufferCapacity);
            maxVisibleLines = Mathf.Clamp(maxVisibleLines, 1, bufferCapacity);

            Buffer = new CommandLog(bufferCapacity);
            Buffer.EntryAdded += HandleLogEntryAdded;
            Buffer.Cleared += HandleLogCleared;
            subscribedBuffer = Buffer;

            Shell ??= new CommandShell();
            Shell.CommandsChanged += HandleCommandsChanged;
            subscribedShell = Shell;
            History ??= new CommandHistory();
            Autocomplete ??= new CommandAutocomplete();

            initialized = true;
            initializedGeneration = runtimeGeneration;

            if (registerBuiltInCommands && !builtInsRegistered)
            {
                BuiltinCommands.Register(Shell);
                builtInsRegistered = true;
            }

            RefreshAutocomplete();
        }

        private void SubscribeUnityLog()
        {
            if (!captureUnityLogs || unityLogSubscribed)
                return;

            Application.logMessageReceived += HandleUnityLog;
            unityLogSubscribed = true;
        }

        private void UnsubscribeUnityLog()
        {
            if (!unityLogSubscribed)
                return;

            Application.logMessageReceived -= HandleUnityLog;
            unityLogSubscribed = false;
        }

        private void UnsubscribeRuntimeEvents()
        {
            if (subscribedBuffer != null)
            {
                subscribedBuffer.EntryAdded -= HandleLogEntryAdded;
                subscribedBuffer.Cleared -= HandleLogCleared;
                subscribedBuffer = null;
            }

            if (subscribedShell != null)
            {
                subscribedShell.CommandsChanged -= HandleCommandsChanged;
                subscribedShell = null;
            }
        }

        private void HandleUnityLog(string message, string stackTrace, LogType type)
        {
            if (Buffer == null)
                return;

            Buffer.HandleLog(message, stackTrace, (TerminalLogType)type);
        }

        private void HandleLogEntryAdded(LogItem entry)
        {
            if (HasActiveSearch && searchFollowsLatest)
                searchPageIndex = -1;

            LogEntryAdded?.Invoke(entry);
            Changed?.Invoke(this);

            if (HasActiveSearch)
                SearchChanged?.Invoke(GetCurrentSearchResult());
        }

        private void HandleLogCleared()
        {
            if (HasActiveSearch)
                searchPageIndex = -1;

            SearchChanged?.Invoke(GetCurrentSearchResult());
            Changed?.Invoke(this);
        }

        private void HandleCommandsChanged()
        {
            RefreshAutocomplete();
            CommandsChanged?.Invoke();
            Changed?.Invoke(this);
        }

        private void RefreshAutocomplete()
        {
            if (Autocomplete == null || Shell == null)
                return;

            Autocomplete.SetWords(Shell.Commands.Keys);
        }

        private static string FormatMessage(string format, object[] message)
        {
            if (format == null)
                return string.Empty;

            if (message == null || message.Length == 0)
                return format;

            try
            {
                return string.Format(format, message);
            }
            catch (FormatException)
            {
                var builder = new StringBuilder(format);

                for (int i = 0; i < message.Length; i++)
                    builder.Append(' ').Append(message[i]);

                return builder.ToString();
            }
        }
    }
}
