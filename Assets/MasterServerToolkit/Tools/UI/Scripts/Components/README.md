# MST UI Components

Reusable UGUI and TextMeshPro helpers used by MST views.

These components only manage presentation and input validation. They do not own
network requests, profiles, rooms, or other MST domain state. Assign references
from the same prefab or view whenever possible so the component remains
self-contained.

## DataTableLayoutGroup

Arranges direct child `RectTransform` elements into rows and columns. Children
are filled from left to right, then continue on the next row.

Settings:

- `Colls Info` defines the columns in display order. A column width greater than
  `0` is fixed in canvas pixels. A width of `0` or less shares the remaining
  width with the other flexible columns.
- `Cell Spacing` is the horizontal gap between cells, in canvas pixels.
- `Row Spacing` is the vertical gap between rows, in canvas pixels.
- `Min Row Height` is the height assigned to every cell, in canvas pixels.
- The inherited `Padding` setting reserves space around the complete table.

Example: for a 600-pixel content area, columns `160`, `0`, and `0` create one
fixed 160-pixel column and split the remaining width between the other two
columns after padding and spacing are deducted.

Keep the number and order of column definitions consistent with the visual
meaning of each child. The layout does not inspect child content or infer
column roles.

## UILable

Displays one text label.

- `Lable Text` is the `TextMeshProUGUI` component controlled by the widget.
- `Lable` is the displayed text. When it is empty, Inspector validation uses
  the GameObject name as the label.
- Runtime code can update the same value through `Text`.

## UIMultiLable

Updates an ordered group of `TextMeshProUGUI` labels through `Text(...)`.

- `Lables Text` defines the output order.
- Values are assigned by array index.
- Extra values are ignored.
- Labels without a corresponding supplied value keep their previous text.

## UIProgressBar

Displays a normalized `currentValue / maxValue` through a `Slider`.

- `Value Lable` optionally displays the percentage.
- `Fill` optionally receives `Progress Bar Color` during Inspector validation.
- `Slider` is required before `Set(...)` is called.
- `Progress Bar Color` controls the assigned fill image color.

Pass a non-zero `maxValue`. The component does not define a fallback for
division by zero.

## UIProgressProperty

Extends `UIProperty` with an animated filled image.

- `Progress Image` is the visual fill. At runtime the component changes a
  non-filled image to a horizontal Filled image.
- `Progress Max Value` is the upper bound for values supplied to
  `SetProgressValue`. Input is clamped from `0` to this value. Inspector values
  of `0` or less are corrected to `1`.

The label displays `current/max`, rounded to whole numbers. The fill moves
toward the target value over time.

## UIProperty

Displays a labelled numeric value with an optional icon and normalized fill.

- `Min Value` and `Max Value` define the accepted range. `SetValue` clamps
  incoming values to this range.
- `Current Value` is the numeric value displayed by `Value Text`.
- `Format Value` selects the number of fractional digits from `F0` to `F5`.
- `Invert Value` reverses only the fill direction; it does not alter the
  displayed numeric value.
- `Min Color` and `Max Color` define the fill color gradient. `Use Colors`
  additionally applies that gradient to the icon.
- `Use Value`, `Use Lable`, `Use Icon`, and `Use Progress` control which
  assigned presentation objects are active after Inspector validation.

`Min Value` must be lower than `Max Value` for the fill and value text to
update. Value changes are applied immediately.

## Validation Components

Call `IsValid()` before submitting the owning form. A non-interactable input or
dropdown is treated as valid.

`UIViewForm` is the base class for views whose primary action requires validation.
It requires `ValidationFormComponent` on the same GameObject and exposes a parameterless
`Submit()` method for button and `TMP_InputField.onSubmit` Inspector bindings. `Submit()`
validates the form and invokes `On Submit Event` only when every validator succeeds.
Connect the concrete request/apply method to `UIViewForm.On Submit Event`. Do not also
connect that method to `ValidationFormComponent.On Form Valid Event`, or the action can
be invoked twice. Navigation between intermediate input fields remains an explicit
Inspector responsibility and should not submit the whole form.

Shared `ValidatableBaseComponent` settings:

- `Invalid Color` is applied after failed validation.
- `Change Validation Color` enables color feedback and the gradual return to
  each graphic's startup color.
- `Validation Target Graphic` contains the graphics that receive feedback.
  Leave it empty when only the validation result and log message are needed.
- `Is Required` enables the derived component's required-value rule.
- `Required Error Message` is written through MST logging on failure. Leave it
  empty to use the default message.

`ValidatableDropdownComponent` settings:

- `Current Dropdown` is the validated `TMP_Dropdown`. Leave it empty to use the
  dropdown on the same GameObject.
- `Min Required Value` is the lowest accepted option index while the field is
  required. Set it to `1` when option `0` is a placeholder such as
  "Select a region".

`ValidatableInputFieldComponent` settings:

- `Current Input Field` is the validated `TMP_InputField`. Leave it empty to
  use the input field on the same GameObject.
- `Compare To Input Field` optionally requires an exact text match, commonly
  for password confirmation.
- `Compare Error Message` overrides the default mismatch message.
- `Reg Exp Pattern` optionally applies a .NET regular expression through
  `Regex.IsMatch`. Leave it empty to disable pattern validation.
- `Reg Exp Error Message` overrides the default pattern failure message.

Required validation runs before the regular expression and comparison checks.
The first failed check returns `false`, reports its message, and applies the
configured visual feedback.
