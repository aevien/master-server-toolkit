# Shared Bridge UI

This folder contains reusable sample UI for exercising MST client features. The views demonstrate authentication, room discovery and creation, chat, notifications, localization, remote images, and simple dialog flows.

These components are presentation clients only. They do not own authentication, room, chat, or notification state. Their authoritative data comes from the corresponding `Mst.Client` module or from a bridge helper such as `MatchmakingBehaviour`.

## General Setup

1. Add the required MST client modules and bridge behaviours to the client scene.
2. Register every `UIView` or `PopupView` prefab with the project's `ViewsManager`.
3. Assign every reference marked as required in the Inspector tooltip.
4. Configure TMP input validation, password masking, navigation, and submit buttons on the UI prefabs.
5. Keep component templates under their target containers only when the owning script explicitly preserves the template. List views otherwise remove existing container children at runtime.
6. Test the view against a running master server with the related module enabled.

The scripts intentionally remain generic demo UI. Game-specific validation, visual policy, localization wording, and business rules belong in the game project.

## Authentication Views

### `SignInView`

- `Username Input Field` and `Password Input Field` provide the credentials sent to `Mst.Client.Auth`.
- `Default Username`, `Default Password`, and `Use Default Credentials` exist only in the Unity Editor. When enabled, the defaults are copied into the form during `Awake`.
- Editor defaults are for local testing only and are excluded from player builds.

Validation:

1. Open the view and submit valid credentials.
2. Confirm that the loading view closes and the main menu opens for a confirmed account.
3. Confirm that an unconfirmed account opens the email confirmation flow.
4. Submit invalid credentials and confirm that an error dialog is shown.

### `SignUpView`

- The username, email, password, and confirmation references are required for the normal form flow.
- Editor defaults populate all fields during `Awake` when enabled.
- The sample view sends the password field to the server but does not compare it with the confirmation field. A production UI must validate equality before calling `SignUp`.

### `EmailConfirmationView`

- `Confirmation Code Input Field` supplies the code passed to `ConfirmEmail`.
- The view can request a new code for the currently signed-in account.
- The authentication module and mail delivery configuration must both be available for the complete flow.

### Password Reset Views

- `PasswordResetCodeView.Email Input Field` contains the account email. The request methods in this sample are currently placeholders and do not send a reset-code request.
- `PasswordResetView.Reset Code Input Field` and `New Password Input Field` are sent to the authentication module.
- `New Password Confirm Input Field` is exposed but is not validated by the sample view.
- `PasswordInputDialogBoxView.Password Input Field` stores its text as the current room password before invoking the dialog callback.

Do not treat the sample password forms as complete production validation. Apply the password rules configured by `AuthModule` before sending a request.

## Client Status

### `ClientConnectionStatusComponent`

- Updates when the MST socket reports a new connection state.
- `Change Status Color` controls only the Image color; the text continues to update.
- Connected and connecting states include the current remote address.
- Both the status Image and text are optional, but omitting them removes that part of the visual output.

### `ClientAuthStatusComponent`

- Displays the signed-in username and distinguishes normal and guest accounts by color.
- `Change Status Color` controls only the Image color.
- An unsigned client or unavailable account uses the localized unauthorized text.

### `ClientInformationView`

- `Client Auth Status Panel` is required.
- The panel is shown only for a signed-in client and displays `AccountInfo.ToString()`.

## Rooms And Players

### `CreateNewRoomView`

- `Room Name Input Field` receives a generated friendly name when the view initializes.
- `Room Max Connections Input Field` is passed as text in the room spawn options. Configure TMP numeric validation on the input itself.
- `Room Region Name Input Dropdown` is populated from matchmaker regions every time the view finishes opening and is required for region selection.
- An empty `Room Password Input Field` omits the room-password option; a non-empty value creates a password-protected room request.

Validation:

1. Open the view while connected to a master with matchmaking and spawner support.
2. Confirm that regions are loaded and the dropdown becomes interactable.
3. Create public and password-protected rooms.
4. Verify the title, maximum connections, region, and password state in the room list.

### `GamesListView`

- `UI Lable Prefab` creates normal data cells.
- `UI Col Lable Prefab` creates column headers.
- `Button Prefab` creates player-list and join actions.
- `List Container` receives all generated objects and is cleared before each refresh.
- `Status Info Text` reports room discovery progress and the empty result outside the Editor.
- `On Start Game Event` is exposed for prefab integrations but is not invoked by the current component.

The label type uses the existing `UILable` spelling. Do not rename serialized fields merely to correct that historical name.

### `GameListItem`

`GameListItem` is an alternative row component. All of its references are guarded, so an omitted reference removes only that part of the row. Its connect button starts the selected room through `MatchmakingBehaviour`.

### `PlayersListView`

- The normal and column label prefabs are required to draw the table.
- `List Container` receives generated labels and is cleared on refresh.
- If no room ID is supplied in the view payload, the view uses the room access currently held by `Mst.Client.Rooms`.

## Chat

### `UsernamePickView`

The input field is required. It receives a generated first name when the view opens, and the accepted value is stored in `Mst.Options` for the chat UI.

### `ChatsView`

- Channel and message containers receive generated rows and are cleared whenever the view opens.
- Separate incoming and outgoing prefabs allow distinct message presentation.
- `Status Info Text`, `Chat Title Text`, and `Message Input Field` are required by the active flow.
- `Default Channel Name` is both the channel joined on open and the receiver used for sent channel messages.
- Outgoing message bodies longer than 200 characters are truncated and receive an ellipsis.

The server must allow the configured channel to be joined. A failed join returns the user to username selection.

### Chat Row Components

- `ChatChannelItem` clears its icon and writes the channel name plus online count.
- `ChatChannelItemUI.Icon Image` is reserved by the current sample and is not changed by `Repaint`.
- `ChatMessageItemUI` treats sender and message labels as optional.

## Notifications

### `NoticeView`

- `Max Notices` creates a fixed reusable pool. Use at least `1`; `0` leaves no item to display and is not a valid operating configuration.
- `Messages Container` is cleared during initialization and receives the pool.
- `Notice Item Prefab` is instantiated once per pool entry.
- `Destroy After` is measured in seconds using scaled game time. A paused `Time.timeScale` also pauses notice expiration.

### `NoticeItem`

- `Message Output` is required.
- `On Message` is invoked only for non-empty notice text.

Validation:

1. Send more notifications than `Max Notices`.
2. Confirm that entries rotate through the fixed pool without creating additional items.
3. Pause scaled time and confirm that visible notices remain until scaled time resumes.

## Images And Localization

### `AvatarComponent`

- `Icon` displays the avatar and is hidden when no sprite is assigned.
- `Progress Image` is optional and rotates while a remote avatar request is active.
- `Default Sprite` is used for empty, invalid, or failed URLs. An empty fallback hides the icon.

### `ImageLoaderUI`

- `Icon` is required.
- `Progress Spinner` is optional.
- `Default Sprite` is assigned for empty URLs and request failures.
- `Max Cache Size` limits the shared in-memory image cache. Values `<= 0` disable retention; the sprite currently displayed by an active component remains alive until released.
- `On Image Loaded` also fires for default, cached, manually assigned, and null sprites.
- `On Image Load Error` fires after the fallback has been assigned.
- `Load(url, errorCallback)` provides a request-local failure callback for code-owned fallback chains.
  The callback runs only after the configured fallback sprite and Inspector error event have been
  processed. Starting another load from this callback is supported.
- `Load(url, stableCacheKey, errorCallback)` stores the image under a stable logical identity. Pass
  `null` as `errorCallback` when request-local error handling is not needed.
  Use this overload for mutable remote images such as `player-avatar:{accountId}`. Loading another
  URL with the same key replaces the previous cache entry instead of consuming another slot.
- Simultaneous requests with the same stable key and URL share one network download. Disabling or
  destroying one `ImageLoaderUI` removes only its subscription; the request continues for remaining
  active components and is cancelled when no subscribers remain.

Call `ClearCache` when an integration deliberately invalidates all cached remote images. Cache entries still displayed by active components are destroyed only after those components release them.

### `LocalizeText`

- `Lable Text` is updated on `Start` and after every MST language change.
- `Localization Key` must exist in the loaded MST localization data.

### `LocalizeImage`

- Each array entry maps an exact MST language code to a sprite.
- The first matching entry is used.
- No match clears the target Image sprite.
- Use an empty array for no mappings; do not leave the array reference null.

## Color Palette

- `Colors` generates one toggle per entry in array order.
- `Toggle Prefab` must contain a child named `Background` with an `Image`.
- `Container` receives the generated toggles.
- `On Color Change Event` fires when a generated toggle enters the on state.

Configure the template's `ToggleGroup` relationship if the palette must allow exactly one active color.

## Ad Banner

- `Show Time` is the required wait in seconds before the banner button invokes the success callback.
- Pressing the button while the timer is active invokes the failure callback.
- `Progress` is required and displays the remaining normalized wait.
- Use a positive `Show Time`; `0` does not provide a meaningful progress duration.

## Release Checklist

1. Open every shared UI prefab and confirm that the Inspector contains no missing required references.
2. Verify sign-in, guest sign-in, sign-up, email confirmation, and error dialogs.
3. Verify room discovery, region loading, room creation, player listing, and joining.
4. Verify chat username selection, channel join, send, receive, and online counts.
5. Verify notifications with scaled time running and paused.
6. Verify valid, invalid, empty, and repeated image URLs.
7. Switch through every supported localization language and confirm text and image mappings.
8. Check the Console for missing-reference exceptions. These demo components deliberately assume that references described as required are assigned.
