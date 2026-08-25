# Tools

`Tools` contains reusable utilities used by MST and demos. Keep this folder generic and free of
project gameplay policy.

## Main Areas

- `Json` - `MstJson` parser/writer and Unity type extensions.
- `UI` - reusable view stack, popups, validation components and simple UI widgets.
- `Utilities` - singleton base, object database helper, scene loader, web requests, generators,
  transliteration, byte/string/transform helpers and small registries.
- `DebounceThrottle` - async dispatchers used by profile save/update batching.
- `Terminal` - in-game command terminal.
- `Editor/Window` - `GameEditorWindow<T>` base for custom editor tools.
- `Attributes` and `WebGL` - small inspector/web helpers.

## Rules

- Utility code should stay dependency-light and reusable.
- `SingletonBehaviour<T>` contains static runtime state and must remain safe for disabled Domain Reload.
- Derived singleton APIs that only inspect or clean up state should use `TryGetExisting`; reserve
  `TryGetOrCreate` for operations that intentionally create the service object.
- `ObjectsDatabase<T>` is editor-assisted ScriptableObject indexing; do not use it for live server DB.
- `MstJson` is legacy-compatible; avoid broad rewrites unless tests/demos are updated.
- Debounce/throttle dispatchers own cancellation tokens and should be disposed by long-lived owners.

## UI Property Widget

`UIProperty` displays an identified numeric value as text, an optional icon, and an optional filled
progress image. `Min Value` and `Max Value` define the accepted range; `SetValue` clamps incoming
values to that range. `Invert Value` reverses only the normalized progress direction. `Format Value`
selects zero to five fractional digits. The `Use` switches control visibility of assigned child
components in the Inspector and at runtime.

The displayed number is always the clamped current value. Inversion affects only the normalized fill.
Value changes are applied immediately.

`UIViewPanel` controls the active state of its assigned `Panel`. `Hide On Start` hides that panel from
`Awake`; disable it when the scene-authored initial state must remain visible.

## View Helpers

`UIViewSync` mirrors owner-view events to an explicit list of other views:

- `Synced Views` contains the target views. Null entries are ignored; do not add the owner itself.
- `Listen To Show Event` calls `Show` on every target when the owner starts showing.
- `Listen To Hide Event` calls `Hide` on every target when the owner starts hiding.
- `Invoke Instantly` skips the target views' normal transitions for both synchronized operations.

`UIViewKeyInputHandler` checks `Input.GetKeyDown` for its configured `Key`. `None` disables keyboard
activation. `Toggle UIView` must be enabled for both the view toggle and `On Input Event`; when it is
disabled, a key press performs no action. `Toggle Instantly` skips the view transition.

`ValidationFormComponent.Validate()` finds child `IValidatableComponent` instances. It invokes
`On Form Valid Event` when every component is valid, including when the form contains no validators,
and invokes `On Form Invalid Event` when at least one validator fails.

The view sound helpers use one-shot 2D UI audio:

- `UIButtonSound` plays `Click Clip` from the assigned button's click event.
- `UIToggleSound` plays `On Clip` only when the assigned toggle changes to On.
- `UIViewSound` plays separate optional clips when its owner view starts showing or hiding.

The Button, Toggle, and AudioSource references are populated from the same GameObject by `OnValidate`
when left empty. Clips are optional; component references are required at runtime. An AudioSource must
be enabled and active in the hierarchy to play.

`DemoView.Loading Popup Time` is measured in seconds. A positive value keeps the loading popup visible
for that duration; `0` requests an immediate timer callback.

## Utility Inspector Settings

`ObjectsDatabase<T>` is an editor-built asset index:

- `Search Paths` contains project-relative folders passed to `AssetDatabase.FindAssets`, such as
  `Assets/GameData`. An empty list is reset to `Assets/`.
- `Sort Order` controls alphabetical asset-name ordering when a derived database rebuilds the index.
- `Objects` is the serialized generated index used by runtime lookups. Manual edits can be overwritten
  by the derived database refresh.

`SingletonBehaviour<T>.Is Global` controls scene ownership. Enabled instances use
`DontDestroyOnLoad`; disabled instances are destroyed with their scene. `Log Level` is the minimum
severity emitted through the singleton's MST logger.

`SceneObject` stores the selected scene name. Its Inspector drawer accepts only scenes present in
Build Settings; an empty value means no scene is assigned.

`WebGlTextMeshProInput.Title` is a localization key for the native browser prompt title. The component
uses the attached `TMP_InputField` value as the prompt default and writes the accepted value back to
that field.

## Screenshot Maker

`ScreenshotMaker` can capture the game view, UI, or both. `Width` and `Height` are pixels; `0` uses the
current screen dimension. `Apply Resolution Multiplier` scales both dimensions by the integer
`Resolution Multiplier`, where `1` keeps the base resolution. An empty `Main Camera` is resolved from
the scene. Transparent background applies to UI capture when the canvas and output format support it.
Keep `Debug Mode` disabled outside capture diagnostics because it emits detailed Console messages.
