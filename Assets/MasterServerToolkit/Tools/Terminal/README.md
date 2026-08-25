# MST Terminal

Client-side command terminal for Unity players and WebGL builds.

The terminal is an MST tool and lives under `Assets/MasterServerToolkit/Tools/Terminal`.
It has two layers:

- `Terminal` - singleton backend for command registration, command execution, logs,
  history, autocomplete and search.
- `TerminalUI` - lightweight IMGUI renderer that draws only the number of
  lines that fit inside the current terminal window height.

The prefab `--TERMINAL.prefab` intentionally contains only one GameObject with
`Terminal` and `TerminalUI`. It does not require Canvas, TMP, UGUI buttons,
scroll rects or font assets.

## Runtime Contract

- `Terminal` inherits MST `SingletonBehaviour<Terminal>`.
- The public command API follows the original MST terminal style:
  `Terminal.Shell`, `Terminal.Log`, `Terminal.Buffer`, `Terminal.History` and
  `Terminal.Autocomplete`.
- Instance-level lazy access is explicit: `GetLogBuffer()`, `GetCommandShell()`,
  `GetCommandHistory()` and `GetCommandAutocomplete()` initialize the runtime backend before returning
  it. `TerminalUI.GetBackend()` performs the configured component/singleton resolution.
- Commands execute only in the client Unity process where the terminal exists.
- Commands are registered explicitly through delegates.
- Runtime reflection command discovery is not used, so the terminal is safe for
  WebGL/AOT.
- Static runtime state is reset on `SubsystemRegistration`, and live terminal
  components reclaim singleton ownership when Scene Reload is disabled.
- The backend stores up to `bufferCapacity` log entries, default `5000`.
- `maxVisibleLines` remains the backend default page size for non-IMGUI callers,
  default `100`.
- `TerminalUI` calculates its active page size from the log area height:
  `floor(logHeight / lineHeight)`.
- Search scans the full log buffer and returns paged results using the active
  visible line capacity.

## Inspector Settings

`Terminal` backend:

- `Buffer Capacity` is the maximum number of log entries retained in memory.
  The oldest entries are removed after the limit is reached. Minimum is `1`;
  default is `5000`.
- `Max Visible Lines` is the default page size for backend read and search
  methods that do not receive an explicit page size. It is clamped between `1`
  and `Buffer Capacity`; default is `100`. `TerminalUI` calculates its own page
  size from the available log area.
- `Capture Unity Logs` copies messages received from Unity's application log
  callback into the terminal buffer.
- `Register Built In Commands` registers the standard terminal commands during
  runtime initialization.

`TerminalUI` input and backend:

- `Terminal` is the backend displayed by the view. Leave it empty to use the
  component on the same GameObject, then the active singleton at runtime.
- `Toggle Key` opens or closes the terminal. Use `None` to disable keyboard
  toggling without disabling the public open and close methods.
- `Initial State` starts the terminal closed, compact, or full height.

`TerminalUI` window and layout:

- `Margin` is the preferred screen-edge spacing in pixels. Minimum is `0`.
- `Small Height Ratio` and `Full Height Ratio` are fractions of screen height
  used by compact and full modes before minimum size and margins are applied.
- `Min Width` and `Min Height` are preferred minimum sizes in screen pixels.
  The window still shrinks when the screen is smaller.
- `Line Height` is the height of one visible log row in pixels and therefore
  controls the number of displayed entries.
- `Padding` is the spacing inside the window and between its main sections.
- `Header Height`, `Input Height`, and `Status Height` are section heights in
  screen pixels.

`TerminalUI` formatting:

- `Include Timestamp` prefixes each line with its local `HH:mm:ss` capture time.
- `Include Log Type` includes values such as `Warning` and `Error`.
- `Focus Command On Open` focuses the command field whenever the terminal opens.
- `Log Autocomplete Suggestions` writes multiple Tab-completion matches to the
  terminal log. A single match is completed directly regardless of this option.

## TerminalUI

`TerminalUI` uses Unity IMGUI and the default IMGUI font fallback. It requires a
`Terminal` component on the same GameObject and does not
assign or load a custom font.

Controls:

- backquote toggles the terminal by default;
- `Run` submits the command line;
- `Clear` clears the log buffer;
- `Full` / `Small` switches window size;
- `Close` hides the terminal;
- `Search` filters the whole buffered log;
- `X` clears search;
- `<` and `>` move through search result pages.

Keyboard shortcuts:

- `Esc` clears search first, then closes the terminal;
- `Up` / `Down` browse command history while the command field is focused;
- `Tab` autocompletes the current command while the command field is focused;
- `PageUp` / `PageDown` scroll log history when search is inactive;
- `PageUp` / `PageDown` move through search pages when search is active;
- `End` returns to the latest log lines.

The log view does not create UI objects per log line. It asks the backend for only the
visible range and draws those lines directly with `GUI.Label`.

## Registering Commands

Preferred registration:

```csharp
using MasterServerToolkit.CommandTerminal;

Terminal.Shell.AddCommand(
    "echo_id",
    args => Terminal.Log("id = {0}", args[0].String),
    minArgCount: 1,
    maxArgCount: 1,
    help: "Prints an id");
```

Unregister commands when the owner is disabled or destroyed:

```csharp
Terminal.Shell.RemoveCommand("echo_id");
```

## Reading Command Arguments

Use `TryGetInt`, `TryGetFloat`, and `TryGetBool` for typed command arguments.
Always check the returned value and stop the handler when conversion fails:

```csharp
Terminal.Shell.AddCommand(
    "set_limit",
    args =>
    {
        if (!args[0].TryGetInt(out int limit))
            return;

        Terminal.Log("Limit = {0}", limit);
    },
    minArgCount: 1,
    maxArgCount: 1,
    help: "Sets an integer limit");
```

Failed conversion returns `false` and reports the expected type through the
`CommandShell` that owns the argument. It does not continue with a substituted
`0` or `false`. `TryGetFloat` prefers invariant-culture input and retains the
current culture as a compatibility fallback. `TryGetBool` accepts `true` and
`false` case-insensitively.

## Submitting Commands From Code

```csharp
Terminal.Instance.SubmitCommand("help");
```

The terminal logs the submitted command as `TerminalLogType.Input`, runs the registered
handler and logs validation or execution errors.

## Reading Logs

```csharp
IReadOnlyList<LogItem> latest = Terminal.Instance.GetLatestEntries(32);
IReadOnlyList<LogItem> scrolled = Terminal.Instance.GetLatestEntries(32, offsetFromLatest: 64);
string text = Terminal.Instance.GetVisibleText();
```

`GetVisibleEntries()` returns either the latest visible log entries or the current
search page if search is active. `TerminalUI` uses the overloads that accept a
runtime page size calculated from the window height.

## Search And Pagination

```csharp
TerminalSearchResult result = Terminal.Instance.SetSearch("zombie", pageSize: 32);
Terminal.Instance.PreviousSearchPage(pageSize: 32);
Terminal.Instance.NextSearchPage(pageSize: 32);
Terminal.Instance.ClearSearch();
```

Search scans all buffered entries, but each page contains only the requested page size.
Search results are cached by query, page, page size and log-buffer version, so IMGUI
repaint/layout events do not rescan the buffer unless the query or log content changes.
A new search starts on the latest page. If new matching logs arrive while the UI is on
the latest search page, the latest page keeps following new results. If the user moves
to an older page, the page does not jump.

## Events

Code can subscribe to:

- `Changed`
- `LogEntryAdded`
- `CommandsChanged`
- `SearchChanged`

## Built-In Commands

- `help`
- `clear`
- `echo`
- `print`
- `logs`
- `time`
- `noop`
- `quit`
- `trace` in `DEBUG`
