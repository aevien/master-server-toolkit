# ProfilesModule Documentation

## Introduction

`ProfilesModule` is a comprehensive server-side component designed to manage player profiles within a master server architecture. It handles the complete lifecycle of player profiles—from creation to updates and eventual unloading—while efficiently managing database interactions and network communications. This module synchronizes profile changes between clients and the server, and ensures data consistency across the gaming ecosystem.

## Contents

- Overview and Architecture
- Installation and Setup
- Configuration Options
- Core Functionality
  - Profile Lifecycle Management
  - Throttled Database Saving
  - Client Update Synchronization
  - Profile Unloading Strategy
- Message Handling System
- Usage Examples
  - Basic Setup and Configuration
  - Handling Profile Events
  - Custom Profile Initialization
  - Advanced Permission Control
- Best Practices and Optimization
- Troubleshooting Common Issues

## Overview and Architecture

The `ProfilesModule` operates as a central hub for player data management, sitting between clients, game servers, and the database layer. It integrates several key components:

1. **Profile Management System**: Creates, loads, updates, and unloads player profiles
2. **Database Integration**: Throttled read/write operations with the database
3. **Client Update System**: Efficient delivery of profile changes to connected clients
4. **Server Update Handling**: Processing and validating profile updates from game servers
5. **Authentication Integration**: Synchronization with the authentication system for user state management

The module uses throttling and debouncing techniques to optimize performance and reduce database load, while ensuring data integrity and responsiveness for players.

## Installation and Setup

Add the `ProfilesModule.cs` file to your project and make sure you're using the correct namespace:

```csharp
using MasterServerToolkit.MasterServer;
```

The module requires the following dependencies:
- `AuthModule`
- `IProfilesDatabaseAccessor` implementation
- `DebounceThrottle` namespace for throttling capabilities

## Configuration Options

The `ProfilesModule` offers several configuration options through Unity's Inspector:

### General Settings

- **Unload Profile After**: Time in seconds after logout until a profile is removed from memory. This should be long enough for game servers to submit final changes. Default: `20` seconds.

- **Save Profile Debounce Time**: Interval in seconds at which updated profiles are saved to the database. This reduces database write operations by grouping multiple updates. Default: `1` second.

- **Client Update Debounce Time**: Interval in seconds at which profile updates are sent to clients. This optimizes network traffic. Default: `1` second.

- **Edit Profile Permission Level**: Permission level required for game servers or admins to edit player profiles. Default: `0` (everyone can edit).

- **Max Update Size**: Maximum size in bytes for profile updates to prevent memory-based attacks. Default: `1048576` (1MB).

### Timeout Settings

- **Profile Load Timeout Seconds**: Maximum time in seconds to wait for a profile to be loaded from the database. Default: `10` seconds.

### Database Connection

- **Database Accessor Factory**: Factory component that creates the database accessor for profile operations.

- **Populators Database**: Collection of property populators that initialize default values in new profiles.

## Core Functionality

### Profile Lifecycle Management

#### 1. Profile Creation

When a user logs in, the module creates a new profile structure if one doesn't exist in memory:

```csharp
protected virtual ObservableServerProfile CreateProfile(string userId, IPeer clientPeer)
{
    var profile = new ObservableServerProfile(userId, clientPeer);

    // Initialize with default values from populators
    foreach (var populator in populatorsDatabase.Populators)
    {
        profile.Add(populator.Populate());
    }

    OnProfileCreated?.Invoke(profile);

    return profile;
}
```

The profile is first populated with default values through the `populatorsDatabase`. This system allows for modular initialization of different profile sections. After creation, subscribers to the `OnProfileCreated` event are notified, allowing for additional customization.

#### 2. Profile Loading

Once created, profiles are loaded from the database:

```csharp
// Restore profile data from database
await databaseAccessor.RestoreProfileAsync(profile);

// Listen to profile events
profile.OnModifiedInServerEvent += OnProfileChangedEventHandler;

profile.ClearUpdates();

// Save profile property
user.Peer.AddExtension(new ProfilePeerExtension(profile, user.Peer));
NotifyProfileLoaded(profile);
```

After loading, the module sets up event listeners to track changes and notifies systems about the loaded profile through the `OnProfileLoaded` event.

#### 3. Profile Updates

The module tracks changes to profiles and manages update propagation:

```csharp
protected virtual void OnProfileChangedEventHandler(ObservableServerProfile profile)
{
    SaveProfile(profile);
    SendUpdatesToClient(profile);
}
```

Each profile change triggers two operations:
- Scheduling a database save operation
- Collecting changes to send to the client

Both operations are throttled to optimize performance.

#### 4. Profile Unloading

When a user logs out, their profile is marked for unloading but not immediately removed:

```csharp
protected void UnloadProfile(string userId)
{
    if (profilesList.TryGetValue(userId, out ObservableServerProfile profile) && profile != null)
    {
        profile.UnloadDebounceDispatcher.Cancel();
        profile.UnloadDebounceDispatcher.Debounce(() =>
        {
            if (authModule.IsUserLoggedInById(userId))
                return;

            SaveProfile(profile);

            if (profilesList.TryRemove(userId, out _))
            {
                profile.OnModifiedInServerEvent -= OnProfileChangedEventHandler;
                logger.Debug($"Profile for user {userId} has been unloaded");
            }
        });
    }
}
```

The unloading process is debounced, allowing time for final game server updates. Before unloading, the module:
1. Checks if the user has logged back in (cancels unloading if true)
2. Saves any pending changes
3. Removes event listeners
4. Removes the profile from memory

### Throttled Database Saving

To prevent database overload, profile saving is throttled:

```csharp
try
{
    saveDebounceDispatcher.ThrottleAsync(async () =>
    {
        List<ObservableServerProfile> snapshot;

        lock (profilesListToSave)
        {
            snapshot = new List<ObservableServerProfile>(profilesListToSave.Values);
            profilesListToSave.Clear();
        }

        await databaseAccessor.UpdateProfilesAsync(snapshot);
    });
}
catch (Exception ex)
{
    logger.Error($"Error saving profiles: {ex}");
}
```

This system:
1. Collects all profiles marked for saving
2. Creates a snapshot under a lock to ensure thread safety
3. Clears the pending save queue
4. Performs a batch update to the database
5. Handles any exceptions that occur during the process

### Client Update Synchronization

Similarly, updates to clients are throttled to optimize network traffic:

```csharp
try
{
    sendDebounceDispatcher.Throttle(() =>
    {
        var snapshot = new Dictionary<IPeer, byte[]>();

        lock (profilesListToSend)
        {
            snapshot = profilesListToSend.ToDictionary(k => k.Value.ClientPeer, k => k.Value.GetUpdates());
            profilesListToSend.Clear();
        }

        foreach (var item in snapshot)
        {
            if (item.Key.IsConnected)
                item.Key.SendMessage(MessageHelper.Create(MstOpCodes.UpdateClientProfile, item.Value), DeliveryMethod.ReliableSequenced);
        }
    });
}
catch (Exception ex)
{
    logger.Error($"Error sending profile updates: {ex}");
}
```

This process:
1. Creates a snapshot of updates under a lock for thread safety
2. Clears the pending update queue
3. Sends updates only to connected clients
4. Uses reliable sequenced delivery to ensure update order

### Profile Unloading Strategy

The module implements a sophisticated unloading strategy:

1. When a user logs out, their profile isn't immediately unloaded
2. A debounced unload operation is scheduled after the configured timeout
3. If the user logs back in before the timeout, the unload is canceled
4. Final changes are saved before unloading
5. Event listeners are properly cleaned up

This approach ensures:
- Game servers have time to submit final updates
- No data is lost during quick reconnects
- Memory usage is optimized by removing inactive profiles

## Message Handling System

The module handles several types of network messages:

### 1. Client Profile Requests

When a client requests their profile:

```csharp
protected virtual async Task ClientFillInProfileValuesRequestHandler(IIncomingMessage message)
{
    // Authentication checks
    // ...
    
    // Get profile with timeout handling
    // ...
    
    // Send profile to client
    profileExt.Profile.ClientPeer = message.Peer;
    message.Respond(profileExt.Profile.ToBytes(), ResponseStatus.Success);
}
```

The handler includes:
- Authentication validation
- Timeout handling with cancellation tokens
- Error handling for various failure scenarios
- Profile serialization and delivery

### 2. Server Profile Updates

Game servers can submit profile updates:

```csharp
protected virtual Task ServerUpdateProfileValuesHandler(IIncomingMessage message)
{
    // Permission checks
    // ...
    
    // Process multiple profile updates
    for (var i = 0; i < count; i++)
    {
        // Read userId and updates from binary stream
        // ...
        
        // Apply updates if size is valid
        if (updatesLength > 0 && updatesLength < maxUpdateSize)
        {
            if (profilesList.TryGetValue(userId, out ObservableServerProfile profile))
            {
                profile.ApplyUpdates(updates);
            }
        }
    }
}
```

This handler:
- Verifies permissions for the server
- Processes binary data containing multiple profile updates
- Validates update size to prevent attacks
- Applies updates to the correct profiles

### 3. Server Profile Requests

Game servers can request full profiles:

```csharp
protected virtual async Task ServerFillInProfileValuesRequestHandler(IIncomingMessage message)
{
    // Permission checks
    // ...
    
    // Retrieve profile with timeout handling
    // ...
    
    // Serialize and send profile
    byte[] rawProfile = profile.ToBytes();
    message.Respond(rawProfile, ResponseStatus.Success);
}
```

This process includes:
- Server permission validation
- Timeout handling
- Profile serialization and delivery

## Usage Examples

### Basic Setup and Configuration

```csharp
// In your Unity MonoBehaviour class
public class GameServer : MonoBehaviour
{
    [SerializeField] private ProfilesModule profilesModule;
    
    private void Start()
    {
        // Configure the module
        profilesModule.unloadProfileAfter = 30; // Keep profiles in memory longer
        profilesModule.saveProfileDebounceTime = 5; // Less frequent saves
        profilesModule.clientUpdateDebounceTime = 1; // Quick client updates
        
        // Assign database factory
        profilesModule.databaseAccessorFactory = GetComponent<DatabaseAccessorFactory>();
    }
}
```

### Handling Profile Events

```csharp
// In your initialization code
public void InitializeProfileHandlers()
{
    var profilesModule = Mst.Server.GetModule<ProfilesModule>();
    
    // Handle profile creation
    profilesModule.OnProfileCreated += OnProfileCreated;
    
    // Handle profile loading
    profilesModule.OnProfileLoaded += OnProfileLoaded;
}

private void OnProfileCreated(ObservableServerProfile profile)
{
    // Initialize additional profile sections or properties
    profile.Add(new ObservableInt("currency", 100)); // Starting currency
    profile.Add(new ObservableString("rank", "Novice")); // Initial rank
}

private void OnProfileLoaded(ObservableServerProfile profile)
{
    // Perform validation or migration of loaded data
    if (!profile.HasProperty("lastLoginDate"))
    {
        profile.Add(new ObservableDateTime("lastLoginDate", DateTime.UtcNow));
    }
    
    // Update last login date
    profile.GetProperty<ObservableDateTime>("lastLoginDate").Value = DateTime.UtcNow;
}
```

### Custom Profile Initialization

```csharp
// Create a custom profile creator by inheriting from ProfilesModule
public class CustomProfileModule : ProfilesModule
{
    // Override the CreateProfile method
    protected override ObservableServerProfile CreateProfile(string userId, IPeer clientPeer)
    {
        // Create the base profile
        var profile = base.CreateProfile(userId, clientPeer);
        
        // Add game-specific data
        profile.Add(new ObservableInt("level", 1));
        profile.Add(new ObservableBool("tutorialCompleted", false));
        
        // Add a collection
        var inventory = new ObservableDictionary<string, int>("inventory");
        inventory.Add("wood", 10);
        inventory.Add("stone", 5);
        profile.Add(inventory);
        
        return profile;
    }
}
```

### Advanced Permission Control

```csharp
// Override permission handling for more granular control
protected override bool HasPermissionToEditProfiles(IPeer messagePeer)
{
    var securityExtension = messagePeer.GetExtension<SecurityInfoPeerExtension>();
    
    // Get the property being modified (example implementation)
    string propertyBeingModified = GetPropertyBeingModified(messagePeer);
    
    // Special handling for certain properties
    if (propertyBeingModified == "currency" || propertyBeingModified == "premium_currency")
    {
        // Require higher permissions for currency modifications
        return securityExtension != null && securityExtension.PermissionLevel >= 5;
    }
    
    // Default permission check
    return securityExtension != null && securityExtension.PermissionLevel >= editProfilePermissionLevel;
}
```

## Best Practices and Optimization

1. **Optimize Throttle Intervals**: 
   - For small player counts (< 1000): 1 second intervals work well
   - For large player counts (> 1000): Consider 3-5 second intervals for database saves
   - For games with critical data: Keep client updates at 1 second or less

2. **Database Load Management**:
   - Implement retries with exponential backoff for database operations
   - Consider sharding profiles across multiple databases for very large games
   - Create indexes on userId in your database for faster profile retrieval

3. **Memory Management**:
   - Monitor active profile count to detect memory leaks
   - Adjust `unloadProfileAfter` based on your game's reconnection patterns
   - Consider implementing a maximum memory threshold that forces profile unloading

4. **Error Handling and Resilience**:
   - Implement more sophisticated retry logic for database operations
   - Add circuit breakers to prevent cascading failures
   - Create backup mechanisms for profile data in case of corruption

5. **Profile Size Optimization**:
   - Only store essential data in profiles
   - Consider splitting profiles into frequently and infrequently changed sections
   - Implement data compression for large profiles

## Troubleshooting Common Issues

### Profile Loading Timeouts

If profiles frequently time out during loading:

1. Check database connection performance and latency
2. Increase `profileLoadTimeoutSeconds` value
3. Implement caching mechanisms for frequently accessed profiles
4. Optimize database queries or add indexes

### High Database Load

If your database is experiencing high load:

1. Increase `saveProfileDebounceTime` to reduce write frequency
2. Implement change detection to avoid saving unchanged profiles
3. Consider read replicas for database scaling
4. Shard profiles across multiple database instances

### Client Synchronization Issues

If clients aren't receiving profile updates:

1. Verify network connectivity and message routing
2. Check that the client is properly registered with the correct userId
3. Validate serialization/deserialization of profile data
4. Implement logging of sent/received updates for debugging

### Memory Usage Growth

If server memory usage keeps growing:

1. Verify profiles are being properly unloaded
2. Check for reference leaks that prevent garbage collection
3. Implement a memory monitor that logs active profile count
4. Force garbage collection after batch profile unloading

This documentation covers the complete functionality of the `ProfilesModule` and should help you effectively implement and optimize player profile management in your game server architecture.