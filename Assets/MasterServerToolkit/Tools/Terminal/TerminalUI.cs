using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MasterServerToolkit.CommandTerminal
{
    [RequireComponent(typeof(Terminal))]
    public class TerminalUI : MonoBehaviour
    {
        private const string CommandControlName = "MSTTerminalCommand";
        private const string SearchControlName = "MSTTerminalSearch";
        private const int FormatCacheLimit = 512;

        [Header("Backend")]
        [SerializeField]
        [Tooltip("Terminal backend displayed by this IMGUI view. Leave empty to use the Terminal on the same GameObject, then the active Terminal singleton at runtime.")]
        private Terminal terminal;

        [Header("Input")]
        [SerializeField]
        [Tooltip("Keyboard key that opens or closes the terminal. Select None to disable keyboard toggling while keeping the public open and close methods available.")]
        private KeyCode toggleKey = KeyCode.BackQuote;
        [SerializeField]
        [Tooltip("Terminal visibility and size applied when this component starts: closed, compact, or full height.")]
        private TerminalState initialState = TerminalState.Close;

        [Header("Window")]
        [SerializeField, Min(0f)]
        [Tooltip("Preferred empty space around the terminal window, in screen pixels. On screens smaller than the configured minimum size, the window is clamped to the available screen area.")]
        private float margin = 16f;
        [SerializeField, Range(0.2f, 0.95f)]
        [Tooltip("Fraction of screen height used by the compact terminal before minimum height and screen margins are applied.")]
        private float smallHeightRatio = 0.38f;
        [SerializeField, Range(0.5f, 1f)]
        [Tooltip("Fraction of screen height used by the full terminal before minimum height and screen margins are applied.")]
        private float fullHeightRatio = 0.92f;
        [SerializeField, Min(320f)]
        [Tooltip("Preferred minimum terminal width, in screen pixels. The window shrinks to fit when the screen is narrower.")]
        private float minWidth = 520f;
        [SerializeField, Min(180f)]
        [Tooltip("Preferred minimum terminal height, in screen pixels. The window shrinks to fit when the screen is shorter.")]
        private float minHeight = 220f;

        [Header("Layout")]
        [SerializeField, Min(12f)]
        [Tooltip("Height of each displayed log row, in screen pixels. This value determines how many buffered entries fit in the log area.")]
        private float lineHeight = 18f;
        [SerializeField, Min(4f)]
        [Tooltip("Spacing inside the terminal window and between its main sections, in screen pixels.")]
        private float padding = 8f;
        [SerializeField, Min(18f)]
        [Tooltip("Height of the terminal header row, in screen pixels.")]
        private float headerHeight = 26f;
        [SerializeField, Min(18f)]
        [Tooltip("Height of the search and command input rows, in screen pixels.")]
        private float inputHeight = 24f;
        [SerializeField, Min(16f)]
        [Tooltip("Height of the status row at the bottom of the terminal, in screen pixels.")]
        private float statusHeight = 20f;

        [Header("Formatting")]
        [SerializeField]
        [Tooltip("When enabled, each displayed log line starts with its local HH:mm:ss capture time.")]
        private bool includeTimestamp = true;
        [SerializeField]
        [Tooltip("When enabled, each displayed log line includes its terminal log type, such as Warning or Error.")]
        private bool includeLogType;
        [SerializeField]
        [Tooltip("When enabled, opening the terminal moves keyboard focus to the command input field.")]
        private bool focusCommandOnOpen = true;
        [SerializeField]
        [Tooltip("When enabled, pressing Tab with multiple autocomplete matches writes all suggestions to the terminal log. A single match is completed directly regardless of this setting.")]
        private bool logAutocompleteSuggestions = true;

        private readonly Dictionary<long, string> formattedLineCache = new Dictionary<long, string>();
        private readonly StringBuilder formatBuilder = new StringBuilder(128);

        private TerminalState state;
        private TerminalSearchResult lastSearchResult = new TerminalSearchResult();
        private string commandText = string.Empty;
        private string searchText = string.Empty;
        private int visibleLineCapacity = 1;
        private int scrollOffsetFromBottom;
        private int lastKnownLogCount = -1;
        private bool focusCommandNextFrame;
        private bool stylesInitialized;
        private bool cachedIncludeTimestamp;
        private bool cachedIncludeLogType;

        private GUIStyle windowStyle;
        private GUIStyle headerStyle;
        private GUIStyle boxStyle;
        private GUIStyle lineStyle;
        private GUIStyle statusStyle;
        private GUIStyle textFieldStyle;
        private GUIStyle buttonStyle;

        public Terminal GetBackend() => ResolveTerminal();
        public TerminalState State => state;
        public bool IsVisible => state != TerminalState.Close;

        protected virtual void Awake()
        {
            state = initialState;
            ResolveTerminal();
        }

        protected virtual void OnValidate()
        {
            margin = Mathf.Max(0f, margin);
            minWidth = Mathf.Max(320f, minWidth);
            minHeight = Mathf.Max(180f, minHeight);
            lineHeight = Mathf.Max(12f, lineHeight);
            padding = Mathf.Max(4f, padding);
            headerHeight = Mathf.Max(18f, headerHeight);
            inputHeight = Mathf.Max(18f, inputHeight);
            statusHeight = Mathf.Max(16f, statusHeight);
        }

        protected virtual void Update()
        {
            if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
                Toggle();
        }

        protected virtual void OnGUI()
        {
            if (!IsVisible)
                return;

            Terminal backend = ResolveTerminal();

            if (backend == null)
                return;

            EnsureStyles();
            SyncExternalState(backend);

            Rect windowRect = CalculateWindowRect();
            GUI.Box(windowRect, GUIContent.none, windowStyle);

            GUI.BeginGroup(windowRect);
            DrawWindowContent(new Rect(0f, 0f, windowRect.width, windowRect.height), backend);
            GUI.EndGroup();
        }

        public void Show()
        {
            OpenSmall();
        }

        public void Hide()
        {
            Close();
        }

        public void Toggle()
        {
            if (state == TerminalState.Close)
                OpenSmall();
            else
                Close();
        }

        public void OpenSmall()
        {
            SetState(TerminalState.OpenSmall);
        }

        public void OpenFull()
        {
            SetState(TerminalState.OpenFull);
        }

        public void Close()
        {
            SetState(TerminalState.Close);
        }

        public void ToggleSize()
        {
            if (state == TerminalState.OpenFull)
                OpenSmall();
            else
                OpenFull();
        }

        public void SubmitCurrentCommand()
        {
            SubmitCommand(commandText);
        }

        public void SubmitCommand(string commandLine)
        {
            Terminal backend = ResolveTerminal();

            if (backend == null || string.IsNullOrWhiteSpace(commandLine))
                return;

            backend.SubmitCommand(commandLine);
            commandText = string.Empty;
            scrollOffsetFromBottom = 0;
            focusCommandNextFrame = focusCommandOnOpen;
        }

        public void ClearLog()
        {
            Terminal backend = ResolveTerminal();

            if (backend == null)
                return;

            backend.ClearLog();
            scrollOffsetFromBottom = 0;
            formattedLineCache.Clear();
        }

        public void ClearSearch()
        {
            Terminal backend = ResolveTerminal();

            if (backend == null)
                return;

            searchText = string.Empty;
            lastSearchResult = backend.ClearSearch();
            scrollOffsetFromBottom = 0;
            focusCommandNextFrame = focusCommandOnOpen;
        }

        public void PreviousSearchPage()
        {
            Terminal backend = ResolveTerminal();

            if (backend == null)
                return;

            lastSearchResult = backend.PreviousSearchPage(visibleLineCapacity);
        }

        public void NextSearchPage()
        {
            Terminal backend = ResolveTerminal();

            if (backend == null)
                return;

            lastSearchResult = backend.NextSearchPage(visibleLineCapacity);
        }

        private void SetState(TerminalState value)
        {
            if (state == value)
                return;

            state = value;

            if (state != TerminalState.Close)
            {
                scrollOffsetFromBottom = 0;
                focusCommandNextFrame = focusCommandOnOpen;
            }
            else
            {
                GUIUtility.keyboardControl = 0;
            }
        }

        private Terminal ResolveTerminal()
        {
            if (terminal != null)
                return terminal;

            terminal = GetComponent<Terminal>();

            if (terminal == null && Application.isPlaying)
                terminal = Terminal.Instance;

            return terminal;
        }

        private void DrawWindowContent(Rect rect, Terminal backend)
        {
            float y = padding;
            float width = rect.width - padding * 2f;

            Rect headerRect = new Rect(padding, y, width, headerHeight);
            DrawHeader(headerRect, backend);
            y += headerHeight + padding;

            Rect searchRect = new Rect(padding, y, width, inputHeight);
            DrawSearch(searchRect, backend);
            y += inputHeight + padding;

            float commandBlockHeight = inputHeight + padding + statusHeight;
            float logHeight = Mathf.Max(lineHeight, rect.height - y - commandBlockHeight - padding);
            Rect logRect = new Rect(padding, y, width, logHeight);
            visibleLineCapacity = Mathf.Max(1, Mathf.FloorToInt((logHeight - 4f) / lineHeight));

            HandleKeyboard(Event.current, backend, logRect);
            DrawLogArea(logRect, backend);
            y += logHeight + padding;

            Rect commandRect = new Rect(padding, y, width, inputHeight);
            DrawCommandInput(commandRect, backend);
            y += inputHeight + padding;

            Rect statusRect = new Rect(padding, y, width, statusHeight);
            DrawStatus(statusRect, backend);

            if (focusCommandNextFrame && Event.current.type == EventType.Repaint)
            {
                GUI.FocusControl(CommandControlName);
                focusCommandNextFrame = false;
            }
        }

        private void DrawHeader(Rect rect, Terminal backend)
        {
            GUI.Label(new Rect(rect.x, rect.y, rect.width - 240f, rect.height), "Terminal", headerStyle);

            float buttonWidth = 70f;
            float x = rect.xMax - buttonWidth;

            if (GUI.Button(new Rect(x, rect.y, buttonWidth, rect.height), "Close", buttonStyle))
                Close();

            x -= buttonWidth + 6f;
            string sizeLabel = state == TerminalState.OpenFull ? "Small" : "Full";

            if (GUI.Button(new Rect(x, rect.y, buttonWidth, rect.height), sizeLabel, buttonStyle))
                ToggleSize();

            x -= buttonWidth + 6f;

            if (GUI.Button(new Rect(x, rect.y, buttonWidth, rect.height), "Clear", buttonStyle))
                ClearLog();
        }

        private void DrawSearch(Rect rect, Terminal backend)
        {
            float labelWidth = 54f;
            float buttonWidth = 32f;
            float pageWidth = 34f;
            float gap = 6f;

            GUI.Label(new Rect(rect.x, rect.y, labelWidth, rect.height), "Search", statusStyle);

            float inputX = rect.x + labelWidth + gap;
            float buttonsWidth = buttonWidth + gap + pageWidth + gap + pageWidth;
            Rect inputRect = new Rect(inputX, rect.y, rect.width - labelWidth - gap - buttonsWidth - gap, rect.height);

            GUI.SetNextControlName(SearchControlName);
            string newSearchText = GUI.TextField(inputRect, searchText, textFieldStyle);

            if (newSearchText != searchText)
            {
                searchText = newSearchText;
                lastSearchResult = backend.SetSearch(searchText, visibleLineCapacity);
                scrollOffsetFromBottom = 0;
            }

            float x = inputRect.xMax + gap;

            using (new GuiEnabledScope(!string.IsNullOrWhiteSpace(searchText)))
            {
                if (GUI.Button(new Rect(x, rect.y, buttonWidth, rect.height), "X", buttonStyle))
                    ClearSearch();
            }

            x += buttonWidth + gap;

            bool canPageSearch = backend.HasActiveSearch && lastSearchResult.HasMatches;

            using (new GuiEnabledScope(canPageSearch && !lastSearchResult.IsFirstPage))
            {
                if (GUI.Button(new Rect(x, rect.y, pageWidth, rect.height), "<", buttonStyle))
                    PreviousSearchPage();
            }

            x += pageWidth + gap;

            using (new GuiEnabledScope(canPageSearch && !lastSearchResult.IsLastPage))
            {
                if (GUI.Button(new Rect(x, rect.y, pageWidth, rect.height), ">", buttonStyle))
                    NextSearchPage();
            }
        }

        private void DrawLogArea(Rect rect, Terminal backend)
        {
            GUI.Box(rect, GUIContent.none, boxStyle);

            IReadOnlyList<LogItem> entries;

            if (backend.HasActiveSearch)
            {
                lastSearchResult = backend.GetCurrentSearchResult(visibleLineCapacity);
                entries = lastSearchResult.Entries;
            }
            else
            {
                entries = backend.GetLatestEntries(visibleLineCapacity, scrollOffsetFromBottom);
            }

            Rect lineRect = new Rect(rect.x + 4f, rect.y + 2f, rect.width - 8f, lineHeight);

            for (int i = 0; i < entries.Count && i < visibleLineCapacity; i++)
            {
                LogItem entry = entries[i];
                lineStyle.normal.textColor = GetTextColor(entry.type);
                GUI.Label(lineRect, GetFormattedLine(entry), lineStyle);
                lineRect.y += lineHeight;
            }

            HandleLogMouseWheel(Event.current, rect, backend);
        }

        private void DrawCommandInput(Rect rect, Terminal backend)
        {
            float buttonWidth = 64f;
            float gap = 6f;
            Rect inputRect = new Rect(rect.x, rect.y, rect.width - buttonWidth - gap, rect.height);
            Rect buttonRect = new Rect(inputRect.xMax + gap, rect.y, buttonWidth, rect.height);

            GUI.SetNextControlName(CommandControlName);
            commandText = GUI.TextField(inputRect, commandText, textFieldStyle);

            if (GUI.Button(buttonRect, "Run", buttonStyle))
                SubmitCurrentCommand();
        }

        private void DrawStatus(Rect rect, Terminal backend)
        {
            string text;

            if (backend.HasActiveSearch)
            {
                if (lastSearchResult.HasMatches)
                {
                    text = string.Format(
                        "Found {0} | Page {1}/{2} | Lines {3}-{4} | Rendering {5}",
                        lastSearchResult.TotalMatches,
                        lastSearchResult.PageIndex + 1,
                        lastSearchResult.PageCount,
                        lastSearchResult.StartMatchIndex,
                        lastSearchResult.EndMatchIndex,
                        visibleLineCapacity);
                }
                else
                {
                    text = string.Format("Found 0 | Rendering {0}", visibleLineCapacity);
                }
            }
            else
            {
                text = string.Format(
                    "Lines {0}/{1} | Rendering {2} | Offset {3}",
                    backend.GetLogBuffer().Count,
                    backend.GetLogBuffer().Capacity,
                    visibleLineCapacity,
                    scrollOffsetFromBottom);
            }

            GUI.Label(rect, text, statusStyle);
        }

        private void HandleKeyboard(Event e, Terminal backend, Rect logRect)
        {
            if (e == null || e.type != EventType.KeyDown)
                return;

            string focusedControl = GUI.GetNameOfFocusedControl();
            bool commandFocused = focusedControl == CommandControlName;

            if (e.keyCode == KeyCode.Escape)
            {
                if (!string.IsNullOrWhiteSpace(searchText))
                    ClearSearch();
                else
                    Close();

                e.Use();
                return;
            }

            if (commandFocused)
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    SubmitCurrentCommand();
                    e.Use();
                    return;
                }

                if (e.keyCode == KeyCode.UpArrow)
                {
                    commandText = backend.GetCommandHistory().Previous();
                    focusCommandNextFrame = true;
                    e.Use();
                    return;
                }

                if (e.keyCode == KeyCode.DownArrow)
                {
                    commandText = backend.GetCommandHistory().Next();
                    focusCommandNextFrame = true;
                    e.Use();
                    return;
                }

                if (e.keyCode == KeyCode.Tab)
                {
                    CompleteCurrentCommand(backend);
                    e.Use();
                    return;
                }
            }

            if (e.keyCode == KeyCode.PageUp)
            {
                PageUp(backend);
                e.Use();
            }
            else if (e.keyCode == KeyCode.PageDown)
            {
                PageDown(backend);
                e.Use();
            }
            else if (e.keyCode == KeyCode.End)
            {
                scrollOffsetFromBottom = 0;
                e.Use();
            }
        }

        private void HandleLogMouseWheel(Event e, Rect logRect, Terminal backend)
        {
            if (e == null || e.type != EventType.ScrollWheel || !logRect.Contains(e.mousePosition))
                return;

            if (backend.HasActiveSearch)
            {
                if (e.delta.y < 0f)
                    PreviousSearchPage();
                else if (e.delta.y > 0f)
                    NextSearchPage();
            }
            else
            {
                int step = Mathf.Max(1, visibleLineCapacity / 2);

                if (e.delta.y < 0f)
                    scrollOffsetFromBottom += step;
                else if (e.delta.y > 0f)
                    scrollOffsetFromBottom -= step;

                ClampScrollOffset(backend);
            }

            e.Use();
        }

        private void PageUp(Terminal backend)
        {
            if (backend.HasActiveSearch)
            {
                PreviousSearchPage();
                return;
            }

            scrollOffsetFromBottom += visibleLineCapacity;
            ClampScrollOffset(backend);
        }

        private void PageDown(Terminal backend)
        {
            if (backend.HasActiveSearch)
            {
                NextSearchPage();
                return;
            }

            scrollOffsetFromBottom -= visibleLineCapacity;
            ClampScrollOffset(backend);
        }

        private void ClampScrollOffset(Terminal backend)
        {
            int maxOffset = Mathf.Max(0, backend.GetLogBuffer().Count - visibleLineCapacity);
            scrollOffsetFromBottom = Mathf.Clamp(scrollOffsetFromBottom, 0, maxOffset);
        }

        private void CompleteCurrentCommand(Terminal backend)
        {
            string headText;
            string[] completions = backend.CompleteCommand(commandText, out headText);

            if (completions == null || completions.Length == 0)
                return;

            if (completions.Length == 1)
            {
                commandText = headText + completions[0];
                focusCommandNextFrame = true;
                return;
            }

            if (logAutocompleteSuggestions)
                Terminal.Log(string.Join("    ", completions));
        }

        private void SyncExternalState(Terminal backend)
        {
            if (backend.SearchQuery != searchText && GUI.GetNameOfFocusedControl() != SearchControlName)
                searchText = backend.SearchQuery;

            if (backend.HasActiveSearch)
                lastSearchResult = backend.GetCurrentSearchResult(visibleLineCapacity);
            else
                lastSearchResult = new TerminalSearchResult();

            int logCount = backend.GetLogBuffer().Count;

            if (lastKnownLogCount >= 0 && logCount < lastKnownLogCount)
            {
                scrollOffsetFromBottom = 0;
                formattedLineCache.Clear();
            }

            lastKnownLogCount = logCount;
            ClampScrollOffset(backend);
            EnsureCacheMatchesFormatting();
        }

        private Rect CalculateWindowRect()
        {
            float screenWidth = Mathf.Max(1f, Screen.width);
            float screenHeight = Mathf.Max(1f, Screen.height);
            float maxWidth = Mathf.Max(1f, screenWidth - margin * 2f);
            float maxHeight = Mathf.Max(1f, screenHeight - margin * 2f);
            float width = Mathf.Min(screenWidth, Mathf.Max(minWidth, maxWidth));
            float heightRatio = state == TerminalState.OpenFull ? fullHeightRatio : smallHeightRatio;
            float height = Mathf.Clamp(screenHeight * heightRatio, Mathf.Min(minHeight, maxHeight), maxHeight);
            float x = Mathf.Max(0f, (screenWidth - width) * 0.5f);
            float y = Mathf.Min(margin, Mathf.Max(0f, screenHeight - height));
            return new Rect(x, y, width, height);
        }

        private void EnsureStyles()
        {
            if (stylesInitialized)
                return;

            windowStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(0, 0, 0, 0)
            };

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip
            };
            headerStyle.normal.textColor = Color.white;

            boxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(4, 4, 2, 2)
            };

            lineStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                wordWrap = false
            };

            statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                wordWrap = false
            };
            statusStyle.normal.textColor = new Color(0.72f, 0.72f, 0.72f, 1f);

            textFieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 13,
                clipping = TextClipping.Clip
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                clipping = TextClipping.Clip
            };

            stylesInitialized = true;
        }

        private string GetFormattedLine(LogItem entry)
        {
            EnsureCacheMatchesFormatting();

            if (formattedLineCache.TryGetValue(entry.sequenceId, out string cached))
                return cached;

            formatBuilder.Length = 0;

            if (includeTimestamp)
                formatBuilder.Append(entry.timestamp.ToString("HH:mm:ss")).Append(' ');

            if (includeLogType)
                formatBuilder.Append('[').Append(entry.type).Append("] ");

            formatBuilder.Append(entry.message);

            string value = formatBuilder.ToString();

            if (formattedLineCache.Count >= FormatCacheLimit)
                formattedLineCache.Clear();

            formattedLineCache[entry.sequenceId] = value;
            return value;
        }

        private void EnsureCacheMatchesFormatting()
        {
            if (cachedIncludeTimestamp == includeTimestamp && cachedIncludeLogType == includeLogType)
                return;

            formattedLineCache.Clear();
            cachedIncludeTimestamp = includeTimestamp;
            cachedIncludeLogType = includeLogType;
        }

        private Color GetTextColor(TerminalLogType type)
        {
            switch (type)
            {
                case TerminalLogType.Error:
                case TerminalLogType.Exception:
                case TerminalLogType.Assert:
                    return new Color(1f, 0.42f, 0.42f, 1f);
                case TerminalLogType.Warning:
                    return new Color(1f, 0.82f, 0.35f, 1f);
                case TerminalLogType.Input:
                    return new Color(0.45f, 0.85f, 1f, 1f);
                case TerminalLogType.ShellMessage:
                    return new Color(0.7f, 1f, 0.65f, 1f);
                default:
                    return Color.white;
            }
        }

        private struct GuiEnabledScope : IDisposable
        {
            private readonly bool previous;

            public GuiEnabledScope(bool enabled)
            {
                previous = GUI.enabled;
                GUI.enabled = enabled;
            }

            public void Dispose()
            {
                GUI.enabled = previous;
            }
        }
    }
}
