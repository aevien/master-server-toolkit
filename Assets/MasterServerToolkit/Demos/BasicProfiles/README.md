# Basic Profiles Demo

This demo shows how a client loads observable profile properties, edits simple player data, and
uses profile-backed currencies and inventory entries in a small store.

The sample is intentionally compact. It demonstrates MST profile synchronization and UI binding,
but its store request contract is not suitable for a production economy.

## Runtime Ownership

- The master scene owns the custom `ProfilesModule` and the authoritative observable profile.
- The client scene owns `DemoProfilesBehaviour` and the profile, settings, and inventory views.
- `StoreOffersDatabase` is client-side presentation data used to build the demo store.
- Currency and inventory changes are applied by the custom master module and synchronized through
  the standard profile system.

## Scene Setup

1. Open `Scenes/MasterServer/MasterServer.unity`.
2. Verify that the custom `BasicProfile.ProfilesModule` is present with the normal authentication,
   database, and profiles dependencies required by the scene.
3. Open `Scenes/Client/Client.unity`.
4. Verify that `DemoProfilesBehaviour` loads the profile after authentication.
5. Verify that `ProfileView`, `ProfileSettingsView`, and `InventoryView` reference the UI objects
   described below.
6. Assign `Databases/StoreOffersDatabase.asset` to `InventoryView.Store Offers`.
7. Run the master and client, authenticate, and wait for the profile to load.

## Demo Profiles Behaviour

### On Profile Saved Event

Invoked only after the master returns a successful response to the profile update request. Use it
to close a dialog, refresh presentation, or show a success notice. It is not invoked for rejected
or failed requests.

## Profile View

### Avatar

Required `ImageLoaderUI` reference. It downloads and displays the profile's `avatarUrl` string.
The sample does not validate the URL before loading it.

### Display Name UI Property

Required `UIProperty` reference. Its label is replaced with the profile's `displayName` value.

### Bronze, Silver, and Gold UI Properties

Required `UIProperty` references. Each component displays the matching observable integer currency
from the loaded profile.

## Profile Settings View

### Display Name Input Field

Required text input. The current `displayName` is copied into it after the profile loads, and its
text is sent back when `Submit()` is called.

### Avatar URL Input Field

Required text input. The current `avatarUrl` is copied into it and submitted as plain text. The demo
does not validate the URL or restrict its scheme.

## Inventory View

### Store Item UI Prefab

Required `ItemUI` prefab instantiated once per entry in the offers database.

### Store Items Container

Required `RectTransform` parent for store entries. Existing children are removed before the store
is redrawn.

### Backpack Item UI Prefab

Required `ItemUI` prefab instantiated for each item stack stored in the profile.

### Backpack Items Container

Required `RectTransform` parent for backpack entries. Existing children are removed when the
inventory is rebuilt after profile loading.

### Store Offers

Required `StoreOffersDatabase` asset. It supplies item IDs, labels, icons, prices, and currency
names for both store and backpack entries.

### Bronze, Silver, and Gold UI Properties

Required currency display components. They are refreshed when the matching profile property
changes.

## Item UI

### Button

Required button used for buying or selling an item. A child `TMP_Text` receives the generated price
label. The demo replaces all button listeners whenever an offer is bound.

## Store Offers Database

### Offers

The ordered list displayed by the store. An empty list produces an empty store.

Each offer contains:

- `Id`: unique, non-empty inventory key. Duplicate IDs make lookups ambiguous.
- `Name`: player-facing label. This demo does not localize it.
- `Icon Sprite`: optional store and backpack icon.
- `Price`: whole-number purchase price. Use a positive value. The demo sells for 70 percent of the
  configured price and truncates the result to an integer.
- `Currency`: profile currency property name, normally `bronze`, `silver`, or `gold`.

## Custom Profiles Module

The serialized header is informational only. The module extends the standard profiles module with
three demo message handlers and automatically adds currency while the server is running:

- `gold`: 1 per second;
- `silver`: 10 per second;
- `bronze`: 100 per second.

These rates are hard-coded demo behavior and are not Inspector settings.

## Validation

1. Confirm that authentication completes and the profile view shows a display name and currencies.
2. Edit the display name and avatar URL, submit, and confirm that `On Profile Saved Event` fires.
3. Buy an offer and confirm that its currency decreases and its item appears in the backpack.
4. Sell the item and confirm that the stack decreases and 70 percent of the configured price is
   returned.
5. Stop the client and reconnect to confirm that saved profile values are restored.

## Limitations

- Purchase requests include price and currency data supplied by the client. A production store must
  resolve trusted product data on the server instead.
- The demo does not validate offer IDs, negative prices, currency names, display names, or avatar
  URLs.
- The UI assumes all required references and profile properties are present.
- This sample is for profile synchronization and UI education, not production economy security.
