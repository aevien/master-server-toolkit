# Master Server Toolkit - Rooms

## Описание
Модуль для регистрации, управления и предоставления доступа к игровым комнатам. Позволяет создавать публичные и приватные комнаты, управлять их параметрами и контролировать доступ игроков.

## RoomsModule

Основной класс для управления комнатами на master server.

### Настройка:
```csharp
[Header("Permissions")]
[SerializeField] protected int registerRoomPermissionLevel = 0;
```

## Регистрация комнаты

### С игрового сервера:
```csharp
// Создание опций комнаты
var options = new RoomOptions
{
    Name = "Epic Battle Arena",
    RoomIp = "192.168.1.100",
    RoomPort = 7777,
    MaxConnections = 16,
    IsPublic = true,
    Password = "", // пустой для публичной комнаты
    Region = "RU",
    CustomOptions = new MstProperties()
};

// Добавляем кастомные свойства
options.CustomOptions.Set("map", "forest_arena");
options.CustomOptions.Set("gameMode", "battle_royale");
options.CustomOptions.Set("difficulty", "hard");

// Регистрация на master server
Mst.Server.Rooms.RegisterRoom(options, (room, error) =>
{
    if (room != null)
    {
        Debug.Log($"Room registered with ID: {room.RoomId}");
    }
    else
    {
        Debug.LogError($"Failed to register room: {error}");
    }
});
```

### Автоматическая регистрация:
```csharp
public class GameServerManager : MonoBehaviour
{
    [Header("Room Settings")]
    public string roomName = "Game Room";
    public int maxPlayers = 10;
    public bool isPublic = true;
    
    void Start()
    {
        RegisterGameRoom();
    }
    
    private void RegisterGameRoom()
    {
        var options = new RoomOptions
        {
            Name = roomName,
            RoomIp = GetServerIp(),
            RoomPort = NetworkManager.singleton.networkPort,
            MaxConnections = maxPlayers,
            IsPublic = isPublic
        };
        
        Mst.Server.Rooms.RegisterRoom(options);
    }
}
```

## Управление комнатой

### Обновление параметров:
```csharp
// Изменение опций комнаты
var newOptions = room.Options;
newOptions.MaxConnections = 20;
newOptions.CustomOptions.Set("gameState", "playing");

Mst.Server.Rooms.SaveRoomOptions(room.RoomId, newOptions);

// Управление игроками
room.AddPlayer(peerId, peer);
room.RemovePlayer(peerId);

// Получение списка игроков
var players = room.Players.Values;
```

### Уничтожение комнаты:
```csharp
// При завершении игры
Mst.Server.Rooms.DestroyRoom(room.RoomId, (successful, error) =>
{
    if (successful)
    {
        Debug.Log("Room destroyed successfully");
    }
});

// Автоматическое уничтожение при отключении
void OnApplicationQuit()
{
    if (registeredRoom != null)
    {
        Mst.Server.Rooms.DestroyRoom(registeredRoom.RoomId);
    }
}
```

## Подключение к комнате

### Поиск и подключение:
```csharp
// Поиск публичных комнат
Mst.Client.Matchmaker.FindGames((games) =>
{
    foreach (var game in games)
    {
        Debug.Log($"Room: {game.Name}, Players: {game.OnlinePlayers}/{game.MaxPlayers}");
    }
});

// Подключение к конкретной комнате
var roomId = 12345;
Mst.Client.Rooms.GetAccess(roomId, "", (access, error) =>
{
    if (access != null)
    {
        ConnectToGameServer(access.RoomIp, access.RoomPort, access.Token);
    }
});
```

### Подключение с паролем:
```csharp
// Подключение к приватной комнате
Mst.Client.Rooms.GetAccess(roomId, "secret_password", (access, error) =>
{
    if (access != null)
    {
        Debug.Log("Access granted!");
        JoinGameServer(access.RoomIp, access.RoomPort, access.Token);
    }
    else
    {
        Debug.LogError("Invalid password");
    }
});
```

## Интеграция с игровым сервером

### RoomServerManager:
```csharp
public class RoomServerManager : MonoBehaviour
{
    [Header("Refs")]
    public NetworkManager networkManager;
    
    private RegisteredRoom currentRoom;
    
    void Start()
    {
        // Запуск сервера
        networkManager.StartServer();
        
        // Регистрация комнаты
        RegisterRoom();
    }
    
    public override void OnServerConnect(NetworkConnectionToClient conn)
    {
        // Проверка токена доступа
        ValidatePlayerAccess(conn);
    }
    
    private void ValidatePlayerAccess(NetworkConnectionToClient conn)
    {
        // Получение токена от клиента
        string accessToken = GetTokenFromClient(conn);
        
        // Проверка на master server
        Mst.Server.Rooms.ValidateAccess(currentRoom.RoomId, accessToken, (userData, error) =>
        {
            if (userData != null)
            {
                // Доступ разрешен
                acceptedPlayers[conn.connectionId] = userData;
                Debug.Log($"Player {userData.Username} validated");
            }
            else
            {
                // Отклонить подключение
                conn.Disconnect();
            }
        });
    }
}
```

## Фильтрация комнат

### Поиск по критериям:
```csharp
// Создание фильтров
var filters = new MstProperties();
filters.Set("map", "forest");
filters.Set("gameMode", "pvp");
filters.Set("maxPlayers", 10);

// Поиск комнат с фильтрами
Mst.Client.Matchmaker.FindGames(filters, (games) =>
{
    var filteredRooms = games.Where(g => 
        g.Type == GameInfoType.Room &&
        g.OnlinePlayers < g.MaxPlayers &&
        !g.IsPasswordProtected
    );
});
```

### Кастомная фильтрация:
```csharp
// На стороне сервера - переопределение GetPublicRoomOptions
public override MstProperties GetPublicRoomOptions(IPeer player, RegisteredRoom room, MstProperties playerFilters)
{
    var roomData = base.GetPublicRoomOptions(player, room, playerFilters);
    
    // Добавляем дополнительную информацию
    roomData.Set("serverVersion", "1.2.3");
    roomData.Set("ping", CalculatePing(player, room));
    
    // Скрываем некоторые данные
    if (!IsAdminPlayer(player))
    {
        roomData.Remove("adminPort");
    }
    
    return roomData;
}
```

## События комнат

### Подписка на события:
```csharp
// На мастер сервере
roomsModule.OnRoomRegisteredEvent += (room) =>
{
    Debug.Log($"New room registered: {room.Options.Name}");
    NotifyMatchmakingSystem(room);
};

roomsModule.OnRoomDestroyedEvent += (room) =>
{
    Debug.Log($"Room destroyed: {room.RoomId}");
    CleanupResources(room);
};

// В игровой комнате
room.OnPlayerJoinedEvent += (player) =>
{
    SendWelcomeMessage(player);
    UpdatePlayerCount();
};

room.OnPlayerLeftEvent += (player) =>
{
    SavePlayerProgress(player);
    CheckIfRoomShouldClose();
};
```

## UI для списка комнат

```csharp
public class RoomListUI : MonoBehaviour
{
    [Header("UI")]
    public GameObject roomItemPrefab;
    public Transform roomList;
    public Button refreshButton;
    
    void Start()
    {
        refreshButton.onClick.AddListener(RefreshRoomList);
        RefreshRoomList();
    }
    
    void RefreshRoomList()
    {
        // Очистка списка
        foreach (Transform child in roomList)
        {
            Destroy(child.gameObject);
        }
        
        // Получение комнат
        Mst.Client.Matchmaker.FindGames((games) =>
        {
            foreach (var game in games)
            {
                if (game.Type == GameInfoType.Room)
                {
                    CreateRoomItem(game);
                }
            }
        });
    }
    
    void CreateRoomItem(GameInfoPacket room)
    {
        var item = Instantiate(roomItemPrefab, roomList);
        var roomUI = item.GetComponent<RoomItem>();
        
        roomUI.Setup(room);
        roomUI.OnJoinClicked = () => JoinRoom(room.Id);
    }
    
    void JoinRoom(int roomId)
    {
        Mst.Client.Rooms.GetAccess(roomId, (access, error) =>
        {
            if (access != null)
            {
                NetworkManager.singleton.networkAddress = access.RoomIp;
                NetworkManager.singleton.StartClient();
            }
        });
    }
}
```

## Лучшие практики

1. **Всегда проверяйте токены доступа** на игровом сервере
2. **Используйте кастомные свойства** для гибкой настройки комнат
3. **Регистрируйте комнаты после старта сервера**
4. **Обновляйте количество игроков** в реальном времени
5. **Очищайте ресурсы** при уничтожении комнаты
6. **Используйте пароли** для приватных комнат
7. **Мониторьте статус комнат** для оптимизации ресурсов
8. **Применяйте фильтры** для лучшего UX поиска комнат
