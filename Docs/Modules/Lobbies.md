# Master Server Toolkit - Lobbies

## Описание
Модуль для создания и управления игровыми лобби, включая создание групп игроков, управление готовностью и запуск игр.

## LobbiesModule

Основной класс для управления игровыми лобби.

### Настройка:
```csharp
[Header("Configuration")]
public int createLobbiesPermissionLevel = 0;
public bool dontAllowCreatingIfJoined = true;
public int joinedLobbiesLimit = 1;
```

## Создание лобби

### Регистрация фабрики лобби:
```csharp
// Создание фабрики
var factory = new LobbyFactoryAnonymous("Game_Lobby", lobbyModule);
factory.CreateNewLobbyHandler = (properties, user) =>
{
    var config = new LobbyConfig
    {
        Name = properties.AsString("name", "Game Lobby"),
        IsPublic = properties.AsBool("isPublic", true),
        MaxPlayers = properties.AsInt("maxPlayers", 10),
        MaxWaitTime = properties.AsInt("maxWaitTime", 60000),
        Teams = new List<LobbyTeam>
        {
            new LobbyTeam("Team A") { MaxPlayers = 5 },
            new LobbyTeam("Team B") { MaxPlayers = 5 }
        }
    };
    
    return new GameLobby(lobbyModule.NextLobbyId(), config, lobbyModule);
};

// Регистрация фабрики
lobbyModule.AddFactory(factory);
```

### Создание лобби (клиент):
```csharp
// Создание лобби
var properties = new MstProperties();
properties.Set(MstDictKeys.LOBBY_FACTORY_ID, "Game_Lobby");
properties.Set("name", "Epic Battle Room");
properties.Set("isPublic", true);
properties.Set("maxPlayers", 8);

Mst.Client.Connection.SendMessage(MstOpCodes.CreateLobby, properties, (status, response) =>
{
    if (status == ResponseStatus.Success)
    {
        int lobbyId = response.AsInt();
        Debug.Log($"Lobby created: {lobbyId}");
    }
});
```

## Присоединение к лобби

### Присоединение (клиент):
```csharp
// Присоединение к лобби
Mst.Client.Connection.SendMessage(MstOpCodes.JoinLobby, lobbyId, (status, response) =>
{
    if (status == ResponseStatus.Success)
    {
        var lobbyData = response.AsPacket<LobbyDataPacket>();
        Debug.Log($"Joined lobby: {lobbyData.Name}");
        
        // Подписка на события лобби
        SubscribeToLobbyEvents();
    }
});

// Покинуть лобби
Mst.Client.Connection.SendMessage(MstOpCodes.LeaveLobby, lobbyId);
```

## События лобби

### Подписка на события:
```csharp
// Подписка на изменения
Mst.Client.Connection.RegisterMessageHandler(MstOpCodes.LobbyMemberJoined, OnMemberJoined);
Mst.Client.Connection.RegisterMessageHandler(MstOpCodes.LobbyMemberLeft, OnMemberLeft);
Mst.Client.Connection.RegisterMessageHandler(MstOpCodes.LobbyStateChange, OnLobbyStateChange);
Mst.Client.Connection.RegisterMessageHandler(MstOpCodes.LobbyMemberReadyStatusChange, OnMemberReadyChange);
Mst.Client.Connection.RegisterMessageHandler(MstOpCodes.LobbyChatMessage, OnChatMessage);

// Обработчики
private void OnMemberJoined(IIncomingMessage message)
{
    var memberData = message.AsPacket<LobbyMemberData>();
    Debug.Log($"Player {memberData.Username} joined the lobby");
}

private void OnLobbyStateChange(IIncomingMessage message)
{
    var state = (LobbyState)message.AsInt();
    Debug.Log($"Lobby state changed to: {state}");
}
```

## Управление свойствами

### Свойства лобби:
```csharp
// Установка свойств лобби (только владелец)
var properties = new MstProperties();
properties.Set("map", "forest");
properties.Set("gameMode", "battle");
properties.Set("maxPlayers", 12);

var packet = new LobbyPropertiesSetPacket
{
    LobbyId = currentLobbyId,
    Properties = properties
};

Mst.Client.Connection.SendMessage(MstOpCodes.SetLobbyProperties, packet);
```

### Свойства игрока:
```csharp
// Установка своих свойств
var myProperties = new Dictionary<string, string>
{
    { "character", "warrior" },
    { "level", "25" },
    { "color", "blue" }
};

Mst.Client.Connection.SendMessage(MstOpCodes.SetMyProperties, myProperties.ToBytes());
```

## Команды

### Готовность игрока:
```csharp
// Установить себя готовым
Mst.Client.Connection.SendMessage(MstOpCodes.SetLobbyAsReady, 1);

// Отменить готовность
Mst.Client.Connection.SendMessage(MstOpCodes.SetLobbyAsReady, 0);
```

### Присоединение к команде:
```csharp
// Присоединиться к команде
var joinTeamPacket = new LobbyJoinTeamPacket
{
    LobbyId = currentLobbyId,
    TeamName = "Team A"
};

Mst.Client.Connection.SendMessage(MstOpCodes.JoinLobbyTeam, joinTeamPacket);
```

## Чат в лобби

### Отправка сообщений:
```csharp
// Отправка сообщения в чат лобби
string chatMessage = "Hello everyone!";
Mst.Client.Connection.SendMessage(MstOpCodes.SendMessageToLobbyChat, chatMessage);

// Получение сообщений
private void OnChatMessage(IIncomingMessage message)
{
    var chatData = message.AsPacket<LobbyChatPacket>();
    Debug.Log($"{chatData.Sender}: {chatData.Message}");
}
```

## Запуск игры

### Ручной запуск:
```csharp
// Начать игру (только владелец лобби)
Mst.Client.Connection.SendMessage(MstOpCodes.StartLobbyGame, (status, response) =>
{
    if (status == ResponseStatus.Success)
    {
        Debug.Log("Game started!");
    }
});
```

### Автоматический запуск:
```csharp
// Настройка автозапуска
var config = new LobbyConfig
{
    EnableReadySystem = true,
    MinPlayersToStart = 2,
    MaxWaitTime = 30000 // 30 секунд
};

// Игра начнется автоматически когда:
// 1. Минимум игроков готовы
// 2. Истекло время ожидания
```

## Реализация пользовательского лобби

```csharp
public class RankedGameLobby : BaseLobby
{
    public RankedGameLobby(int lobbyId, ILobbyFactory factory, 
        LobbiesModule module) : base(lobbyId, factory, module)
    {
        // Кастомные настройки
        EnableTeamSwitching = false;
        EnableReadySystem = true;
        MaxPlayers = 10;
    }
    
    public override bool AddPlayer(LobbyUserPeerExtension playerExt, out string error)
    {
        // Проверка рейтинга игрока
        var playerProfile = GetPlayerProfile(playerExt);
        if (playerProfile.Rating < RequiredRating)
        {
            error = "Rating too low for this lobby";
            return false;
        }
        
        // Автоматическое распределение по командам
        AutoBalanceTeams(playerExt);
        
        return base.AddPlayer(playerExt, out error);
    }
    
    protected override void OnGameStart()
    {
        // Кастомная логика старта
        CalculateTeamBalance();
        ApplyRatingModifiers();
        
        base.OnGameStart();
    }
}
```

## Интеграция с другими модулями

### С Spawner:
```csharp
// Автоматическое создание сервера при старте игры
protected override void OnGameStart()
{
    var spawnOptions = new MstProperties();
    spawnOptions.Set("map", Properties["map"]);
    spawnOptions.Set("gameMode", Properties["gameMode"]);
    
    SpawnersModule.Spawn(spawnOptions, (spawner, data) =>
    {
        GamePort = data.RoomPort;
        GameIp = data.RoomIp;
        // Игроки получат доступ к серверу
    });
}
```

### С Matchmaker:
```csharp
// Интеграция с матчмейкингом
var factory = new LobbyFactoryAnonymous("Matchmaking_Lobby", lobbyModule);
factory.CreateNewLobbyHandler = (properties, user) =>
{
    var matchmakingData = properties.AsPacket<MatchmakingRequestPacket>();
    var lobby = new MatchmakingLobby();
    
    // Настройка на основе данных матчмейкинга
    lobby.SetupMatchmaking(matchmakingData);
    
    return lobby;
};
```

## Лучшие практики

1. **Используйте фабрики** для создания разных типов лобби
2. **Валидируйте свойства** перед добавлением игроков
3. **Настройте автобаланс** для справедливой игры
4. **Обрабатывайте события** для создания отзывчивого UI
5. **Очищайте пустые лобби** для освобождения ресурсов
6. **Логируйте действия** для отладки и аналитики
7. **Защищайте от злоупотреблений** с помощью лимитов

## Примеры UI

### Простой список лобби:
```csharp
public class LobbyListUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject lobbyItemPrefab;
    public Transform lobbyList;
    
    void Start()
    {
        RefreshLobbyList();
    }
    
    void RefreshLobbyList()
    {
        // Получение списка публичных лобби
        Mst.Client.Lobbies.GetPublicLobbies((lobbies) =>
        {
            foreach (var lobby in lobbies)
            {
                var item = Instantiate(lobbyItemPrefab, lobbyList);
                var itemComponent = item.GetComponent<LobbyListItem>();
                itemComponent.Setup(lobby);
            }
        });
    }
}
```