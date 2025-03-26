# Master Server Toolkit - Profile System Documentation

## Overview

The Master Server Toolkit (MST) Profile System provides a robust framework for managing player profiles in multiplayer game architectures. It enables real-time synchronization of player data across different contexts, with automatic change tracking and persistence.

The system operates in three distinct contexts, each with specialized components:

1. **Client Context** - Manages the player's local profile representation and updates
2. **Game Server Context** - Handles multiple player profiles during gameplay sessions
3. **Master Server Context** - Coordinates profiles across the entire network and provides persistence

The Profile System excels at:
- Real-time synchronization of player data
- Automatic change tracking
- Efficient network serialization
- Seamless integration with Unity
- Database persistence
- Event-driven architecture

## Architecture

The Profile System architecture follows the observer pattern, creating a reactive data system where changes to profile properties automatically propagate through the network.

### Data Flow

1. **Property Changes**: When a property within a profile is modified, it marks itself as "dirty" and notifies its parent profile
2. **Profile Updates**: The profile collects changes and prepares them for transmission
3. **Network Synchronization**: Changes are sent to the appropriate servers/clients
4. **Database Persistence**: The master server periodically saves dirty profiles to the database

### Component Relationships

- **ObservableProperties** form the foundation, providing change tracking for individual data points
- **ObservableProfile** aggregates properties and manages serialization/updates
- **Context-specific Implementations** (Client/Server/Master) handle network communication
- **Database Accessor** provides persistence for profiles

## Core Components

### ObservableProfile

The `ObservableProfile` class serves as the foundation of the profile system. It acts as a container for observable properties, tracking changes and providing serialization capabilities.

Key features:

- **Property Management**: Stores and retrieves properties by key
- **Change Tracking**: Monitors which properties have been modified
- **Serialization**: Converts profiles to bytes for network transmission
- **Update Management**: Efficiently collects and applies property updates
- **Event System**: Notifies listeners when properties change

The profile maintains a collection of `IObservableProperty` instances, each with a unique key. When properties change, they trigger events that bubble up to the profile, which then marks those properties for update transmission.

### ObservableServerProfile

`ObservableServerProfile` extends the base profile with server-specific functionality:

- **User Identification**: Associates the profile with a specific user ID
- **Peer Reference**: Maintains a link to the user's network connection
- **Server Events**: Provides events for server-side processing of changes
- **Lifecycle Management**: Handles disposal and cleanup of resources

This server-side representation is crucial for game servers and the master server to track which profiles belong to which users and manage their lifecycle.

### MstProfilesClient

`MstProfilesClient` manages the client-side lifecycle of profiles, providing:

- **Profile Loading**: Requests and retrieves profile data from the server
- **Event Notification**: Informs client code when profile data is available
- **Connection Management**: Handles network connection status and reconnection
- **Update Reception**: Processes incoming profile updates from the server

This client component creates a seamless experience where profile data appears synchronized with the server without requiring explicit refresh calls from game code.

### MstProfilesServer

`MstProfilesServer` operates on game servers, providing:

- **Profile Collection**: Manages multiple player profiles simultaneously
- **Update Batching**: Collects changes and sends them efficiently to the master server
- **Update Scheduling**: Configurable update intervals for performance tuning
- **Player Identification**: Retrieves profiles by user ID

Game servers act as intermediaries in the profile system, collecting changes during gameplay and forwarding them to the master server while also applying updates from the master server to ensure consistency.

### ProfilesModule

The `ProfilesModule` is the master server component that coordinates the entire profile system:

- **Database Integration**: Persists profiles to long-term storage
- **Authentication Integration**: Links profiles to user accounts
- **Update Management**: Processes updates from game servers
- **Client Synchronization**: Sends updates to connected clients
- **Profile Creation**: Initializes new profiles with default values
- **Profile Unloading**: Manages memory by unloading inactive profiles
- **Security**: Validates profile modification permissions

This module serves as the central authority for profile data, ensuring consistency and persistence across the entire game network.

## Observable Properties

Observable properties are the fundamental building blocks of the profile system. They track changes to individual data points and emit events when modified.

### Property Types

The system supports various property types to accommodate different kinds of game data:

- **ObservableString**: For text data (usernames, titles, etc.)
- **ObservableInt**: For numeric values (level, score, currency, etc.)
- **ObservableBool**: For flags and toggles (achievements, settings, etc.)
- **ObservableList**: For collections (inventory, abilities, etc.)
- **ObservableDictionary**: For key-value pairs (statistics, custom data, etc.)
- **ObservableVector3**: For positions and directions
- **ObservableDateTime**: For timestamps and durations

Each property type handles its own serialization and change tracking, making the system extensible for custom data types.

### Change Detection

Observable properties implement a smart change detection system:

1. When a property's value is modified, it calls `MarkAsDirty()`
2. This triggers the `OnDirtyEvent`, notifying listeners (typically the parent profile)
3. The parent profile adds the property to a collection of "dirty" properties
4. During the next update cycle, only the changed properties are serialized and transmitted

This approach minimizes network traffic by sending only the data that has actually changed rather than the entire profile.

### Property Populators

Populators provide a convenient way to initialize properties with default values:

- **ScriptableObject-based**: Created in the Unity editor
- **Type-specific**: Each populator creates a specific property type
- **Default Value Configuration**: Allows setting initial values
- **Key Management**: Handles property key generation

The `ObservablePropertyPopulatorsDatabase` collects these populators, making it easy to define a complete profile structure that is consistently applied to new players.

## Serialization and Networking

The Profile System implements efficient serialization strategies for network transmission:

### Full Profile Serialization

When a client first connects or a game server requests a profile, the entire profile is serialized:

1. The profile counts its properties
2. Each property is identified by its key
3. Each property serializes its data
4. The resulting bytes are transmitted

### Delta Updates

For ongoing synchronization, only changes (deltas) are transmitted:

1. The profile collects properties that have been marked dirty
2. Only these properties are serialized
3. The resulting, much smaller, bytes are transmitted
4. The receiving end applies these targeted updates

This delta approach significantly reduces bandwidth usage, especially for large profiles with frequent, small changes.

### Binary Format

The system uses a custom binary format for serialization:

- **Endian-aware**: Works across different platforms
- **Compact**: Minimizes data size
- **Type-specific**: Each property type handles its own serialization
- **Hierarchical**: Supports nested data structures

The binary format ensures efficient network usage while maintaining compatibility across different platforms and contexts.

## Client-Side Implementation Details

The client-side implementation centers around retrieving and displaying profile data while handling property updates.

### Profile Initialization

When a client connects to the master server, it initializes its profile system:

1. Create a local profile instance
2. Register required property types
3. Request profile data from the server
4. Receive and apply server data
5. Set up event listeners for property changes

This process ensures the client has an up-to-date representation of the player's profile.

### Handling Property Changes

The client can both receive property changes from the server and make local changes:

- **Server Updates**: The server sends delta updates that are automatically applied to the local profile
- **Local Changes**: When the client modifies a property, the change is automatically queued for transmission to the server

This bidirectional synchronization creates a seamless experience where changes appear consistent across all contexts.

### UI Integration

Profile properties can easily integrate with UI elements:

1. Access properties through the profile
2. Display current values in UI elements
3. Register for change events
4. Update UI when properties change

This event-driven approach eliminates the need for polling or manual refresh calls, creating a reactive UI that automatically updates when profile data changes.

## Game Server Implementation Details

Game servers act as intermediaries, managing multiple player profiles during gameplay sessions.

### Profile Management

Game servers handle multiple player profiles simultaneously:

1. When a player connects, request their profile from the master server
2. Store the profile in a local collection
3. Associate the profile with the player's game representation
4. Make gameplay-driven updates to properties
5. Allow game systems to access profile data

This centralized approach gives the game server a complete view of all connected players' data.

### Gameplay Integration

Game servers typically modify profiles based on gameplay events:

- Award experience when players complete objectives
- Update statistics when game events occur
- Modify inventory when items are acquired or used
- Track achievements when conditions are met

These changes are automatically tracked and synchronized with the master server.

### Efficient Synchronization

Game servers employ several strategies for efficient profile synchronization:

- **Batching**: Collect multiple property changes before transmission
- **Scheduling**: Send updates at configurable intervals
- **Prioritization**: Ensure critical updates are sent promptly
- **Compression**: Minimize data size through delta updates

These optimizations balance responsiveness with network efficiency.

## Master Server Implementation Details

The master server coordinates the entire profile system, managing persistence and ensuring consistency.

### Profile Lifecycle

The master server handles the full lifecycle of profiles:

1. **Creation**: Initialize new profiles when players first login
2. **Loading**: Retrieve existing profiles from the database
3. **Updating**: Process changes from game servers and clients
4. **Persistence**: Save modified profiles to the database
5. **Unloading**: Remove inactive profiles from memory

This lifecycle management ensures efficient resource usage while maintaining data integrity.

### Database Integration

The master server integrates with databases through the `IProfilesDatabaseAccessor` interface:

- **Restore**: Load profile data when players connect
- **Update**: Save profile changes periodically
- **Batch Processing**: Handle multiple profiles efficiently

This abstraction allows the profile system to work with various database technologies (SQL, NoSQL, cloud services, etc.).

### Security and Validation

The master server implements security measures to protect profile data:

- **Permission Checking**: Verify that servers/clients have appropriate rights
- **Data Validation**: Ensure profile data meets expected formats and ranges
- **Timeout Handling**: Manage loading/saving timeouts gracefully
- **Error Recovery**: Implement retry mechanisms for failed operations

These measures protect against data corruption, unauthorized access, and system failures.

## Database Integration

The Profile System provides a flexible database integration architecture for persisting player profiles.

### IProfilesDatabaseAccessor Interface

This interface defines the core functionality required for database integration:

- **RestoreProfileAsync**: Load a profile from the database
- **UpdateProfileAsync**: Save a single profile to the database
- **UpdateProfilesAsync**: Save multiple profiles efficiently

By implementing this interface, developers can connect the profile system to their preferred database technology.

### Database Operation Flow

The typical flow for database operations includes:

1. **Player Login**: Trigger profile loading from the database
2. **Profile Modification**: Track changes in memory
3. **Periodic Saving**: Save modified profiles at configured intervals
4. **Batch Processing**: Group multiple profile saves for efficiency
5. **Player Logout**: Ensure final save of profile data

This approach balances the need for persistence with performance considerations.

### JSON Serialization

While the network uses binary serialization for efficiency, database integration typically uses JSON:

- **Human-readable**: Easier to debug and manually inspect
- **Compatibility**: Works with most database systems
- **Flexibility**: Accommodates schema evolution
- **Query Support**: Enables querying specific profile elements in supporting databases

Each observable property implements both binary and JSON serialization methods to support both network and database operations.

## Usage Examples

### Basic Client Profile Setup

```csharp
// Create a profiles client
var profilesClient = new MstProfilesClient(Mst.Client.Connection);

// Create a local profile instance
var profile = new ObservableProfile();

// Add required properties
profile.Add(new ObservableString("username".ToUint16Hash(), "Player"));
profile.Add(new ObservableInt("level".ToUint16Hash(), 1));
profile.Add(new ObservableInt("experience".ToUint16Hash(), 0));

// Register for profile loading event
profilesClient.OnProfileLoadedEvent += OnProfileLoaded;

// Request profile data from server
profilesClient.FillInProfileValues(profile, (success, error) => {
    if (!success) Debug.LogError($"Failed to load profile: {error}");
});

// Handle profile loaded event
void OnProfileLoaded(ObservableProfile profile)
{
    // Access profile properties
    var username = profile.Get<ObservableString>("username");
    var level = profile.Get<ObservableInt>("level");
    
    // Display values in UI
    usernameText.text = username.Value;
    levelText.text = $"Level: {level.Value}";
    
    // Register for property changes
    username.OnDirtyEvent += (prop) => usernameText.text = ((ObservableString)prop).Value;
    level.OnDirtyEvent += (prop) => levelText.text = $"Level: {((ObservableInt)prop).Value}";
}
```

### Game Server Profile Management

```csharp
// Create a profiles server
var profilesServer = new MstProfilesServer(Mst.Server.Connection);

// Set update interval
profilesServer.ProfileUpdatesInterval = 0.2f; // 200ms

// When a player connects
void OnPlayerConnected(NetworkPlayer player, string userId)
{
    // Create a server profile
    var profile = new ObservableServerProfile(userId);
    
    // Request profile data from master server
    profilesServer.FillProfileValues(profile, (success, error) => {
        if (success)
        {
            // Assign profile to player
            player.Profile = profile;
        }
    });
}

// Award experience to a player
void AwardExperience(string userId, int amount)
{
    if (profilesServer.TryGetById(userId, out var profile))
    {
        var experience = profile.Get<ObservableInt>("experience");
        var level = profile.Get<ObservableInt>("level");
        
        // Update experience (changes automatically tracked)
        experience.Value += amount;
        
        // Check for level up
        int requiredExp = level.Value * 100;
        if (experience.Value >= requiredExp)
        {
            level.Value += 1;
            experience.Value -= requiredExp;
        }
    }
}
```

### Master Server Setup

```csharp
void Start()
{
    // Create and add profiles module
    var profilesModule = gameObject.AddComponent<ProfilesModule>();
    
    // Configure profiles module
    profilesModule.unloadProfileAfter = 30; // Unload after 30 seconds
    profilesModule.saveProfileDebounceTime = 5; // Save every 5 seconds
    profilesModule.clientUpdateDebounceTime = 1; // Send updates every 1 second
    profilesModule.populatorsDatabase = CreatePopulatorsDatabase();
    
    // Set up database integration
    profilesModule.databaseAccessorFactory = gameObject.AddComponent<MyDatabaseAccessorFactory>();
    
    // Add module to server
    Mst.Server.AddModule(profilesModule);
}
```

## Feature Highlights

### Real-time Synchronization

The Profile System provides real-time synchronization of player data across different contexts:

- Changes made on the client are immediately reflected on the server
- Updates from the server are automatically applied to the client
- Game server modifications propagate to both clients and the master server

This synchronization happens with minimal developer intervention, creating a seamless experience where data appears consistent across all parts of the system.

### Automatic Change Tracking

The change tracking system automatically detects and propagates modifications:

- When a property is changed, it marks itself as "dirty"
- The parent profile collects these dirty properties
- Only the changed properties are serialized and transmitted
- The receiving end applies targeted updates

This approach eliminates the need for explicit "save" or "update" calls, streamlining development and reducing bugs.

### Event-Driven Architecture

The profile system uses an event-driven architecture for responsive and decoupled code:

- Properties emit events when their values change
- Profiles notify listeners when properties are updated
- Components can register for specific property changes
- UI elements can react directly to data modifications

This reactive approach creates clean, maintainable code where components respond to changes without tight coupling.

### Efficient Network Usage

The system employs several strategies to minimize network traffic:

- Delta updates send only changed properties
- Update batching collects multiple changes
- Configurable update intervals prevent network flooding
- Binary serialization minimizes data size

These optimizations make the profile system suitable for games with limited bandwidth or large player populations.

### Customizable Profile Schema

The profile schema can be customized to fit specific game requirements:

- Define custom properties for game-specific data
- Create property populators for initialization
- Organize properties in a logical structure
- Extend the system with custom property types

This flexibility allows the profile system to adapt to various game genres and requirements.

### Database Abstraction

The database integration provides a flexible abstraction:

- Interface-based design allows various implementations
- Async methods support different database technologies
- Batch operations enable efficient persistence
- Error handling and retry mechanisms ensure reliability

Developers can implement the `IProfilesDatabaseAccessor` interface for their preferred database technology, whether SQL, NoSQL, or cloud-based.

## Best Practices

### Profile Schema Design

Design your profile schema with consideration for:

- **Performance**: Group related properties to minimize update overhead
- **Scalability**: Use appropriate property types for expected data sizes
- **Clarity**: Use consistent naming conventions
- **Extensibility**: Plan for future additions to the profile

A well-designed schema lays the foundation for efficient profile management throughout your game's lifecycle.

### Property Naming Conventions

Establish clear naming conventions for profile properties:

- Use descriptive, consistent names
- Consider grouping related properties with prefixes
- Document the purpose and expectations for each property
- Be mindful of hash collisions with string-based keys

Example naming structure:
```
user.username      - Basic information
progression.level  - Player progression
progression.xp     - Experience points
inventory.items    - Player's items
stats.kills        - Game statistics
settings.audio     - User preferences
```

### Optimizing Update Frequency

Balance responsiveness and network efficiency:

- Critical data (health, position) may need immediate updates
- Less important data (statistics, achievements) can use longer intervals
- Consider the game type when setting update intervals
- Monitor network usage and adjust accordingly

Configurable settings:
- `profilesServer.ProfileUpdatesInterval`: How often game servers send updates
- `profilesModule.clientUpdateDebounceTime`: How often clients receive updates
- `profilesModule.saveProfileDebounceTime`: How often profiles are saved to the database

### Error Handling

Implement robust error handling:

- Validate profile data for integrity
- Implement retry mechanisms for network operations
- Provide fallbacks for missing or corrupted data
- Log errors for debugging
- Handle timeout cases gracefully

Proper error handling ensures a smooth player experience even when network conditions are less than ideal.

### Performance Considerations

Keep performance in mind:

- Unload inactive profiles to conserve memory
- Batch database operations for efficiency
- Use appropriate property types for data size
- Consider compression for large profile data
- Profile the system under typical load conditions

Regular performance monitoring helps identify and address bottlenecks before they impact players.

## Conclusion

The Master Server Toolkit's Profile System provides a robust foundation for managing player data in multiplayer games. With its real-time synchronization, automatic change tracking, and flexible architecture, it enables developers to focus on game features rather than the complexities of distributed data management.

By following the guidelines and examples in this documentation, developers can efficiently implement profile management that scales with their game's needs while providing a seamless experience for players.

For more information and advanced usage, refer to the official Master Server Toolkit documentation and example projects.