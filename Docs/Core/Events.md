# Master Server Toolkit - Events

## Описание
Система событий для коммуникации между компонентами без жестких связей. Включает каналы событий и типизированные сообщения.

## MstEventsChannel

Канал для отправки и получения событий.

### Создание канала:
```csharp
// Создание канала с обработкой исключений
var defaultChannel = new MstEventsChannel("default", true);

// Создание простого канала
var gameChannel = new MstEventsChannel("game");

// Использование глобального канала
var globalChannel = Mst.Events;
```

### Основные методы:
```csharp
// Отправка события без данных
channel.Invoke("playerDied");

// Отправка события с данными
channel.Invoke("scoreUpdated", 100);
channel.Invoke("playerJoined", new Player("John"));

// Подписка на события
channel.AddListener("gameStarted", OnGameStarted);
channel.AddListener("levelCompleted", OnLevelCompleted, true);

// Отписка от событий
channel.RemoveListener("gameStarted", OnGameStarted);
channel.RemoveAllListeners("playerDied");
```

## EventMessage

Контейнер для данных событий с типизированным доступом.

### Создание и использование:
```csharp
// Создание пустого сообщения
var emptyMsg = EventMessage.Empty;

// Создание с данными
var scoreMsg = new EventMessage(150);
var playerMsg = new EventMessage(new Player { Name = "Alex", Level = 10 });

// Получение данных
int score = scoreMsg.As<int>();
float damage = damageMsg.AsFloat();
string text = textMsg.AsString();
bool isWinner = resultMsg.AsBool();

// Проверка наличия данных
if (message.HasData())
{
    var data = message.As<MyData>();
}
```

## Примеры использования

### 1. Игровые события:
```csharp
public class GameManager : MonoBehaviour
{
    private MstEventsChannel gameEvents;
    
    void Start()
    {
        gameEvents = new MstEventsChannel("game");
        
        // Подписка на события
        gameEvents.AddListener("playerJoined", OnPlayerJoined);
        gameEvents.AddListener("scoreChanged", OnScoreChanged);
        gameEvents.AddListener("gameOver", OnGameOver);
    }
    
    private void OnPlayerJoined(EventMessage msg)
    {
        var player = msg.As<Player>();
        Debug.Log($"Player {player.Name} joined the game");
        
        // Уведомляем других игроков
        gameEvents.Invoke("playerListUpdated", GetPlayerList());
    }
    
    private void OnScoreChanged(EventMessage msg)
    {
        int newScore = msg.AsInt();
        UpdateScoreUI(newScore);
        
        // Проверка рекорда
        if (newScore > highScore)
        {
            gameEvents.Invoke("newRecord", newScore);
        }
    }
}
```

### 2. UI события:
```csharp
public class UIManager : MonoBehaviour
{
    void Start()
    {
        // Подписка на глобальные события
        Mst.Events.AddListener("connectionLost", OnConnectionLost);
        Mst.Events.AddListener("dataLoaded", OnDataLoaded);
        Mst.Events.AddListener("errorOccurred", OnError);
    }
    
    private void OnConnectionLost(EventMessage msg)
    {
        ShowReconnectDialog();
    }
    
    private void OnDataLoaded(EventMessage msg)
    {
        var data = msg.As<GameData>();
        UpdateUI(data);
        HideLoadingScreen();
    }
    
    private void OnError(EventMessage msg)
    {
        string errorText = msg.AsString();
        ShowErrorDialog(errorText);
    }
}
```

### 3. Межкомпонентная коммуникация:
```csharp
public class InventorySystem : MonoBehaviour
{
    private MstEventsChannel inventoryEvents;
    
    void Start()
    {
        inventoryEvents = new MstEventsChannel("inventory");
        
        // Подписка на игровые события
        Mst.Events.AddListener("itemDropped", OnItemDropped);
        Mst.Events.AddListener("playerDied", OnPlayerDied);
    }
    
    public void AddItem(Item item)
    {
        if (CanAddItem(item))
        {
            // Добавляем предмет
            items.Add(item);
            
            // Уведомляем о изменении инвентаря
            inventoryEvents.Invoke("itemAdded", item);
            
            // Проверяем квесты
            if (IsQuestItem(item))
            {
                Mst.Events.Invoke("questItemObtained", item);
            }
        }
        else
        {
            inventoryEvents.Invoke("inventoryFull", item);
        }
    }
}
```

## Именованные каналы

### Создание специализированных каналов:
```csharp
// Канал для боевой системы
var combatChannel = new MstEventsChannel("combat");
combatChannel.AddListener("enemySpotted", OnEnemySpotted);
combatChannel.AddListener("damageDealt", OnDamageDealt);

// Канал для социальной системы
var socialChannel = new MstEventsChannel("social");
socialChannel.AddListener("friendRequestReceived", OnFriendRequest);
socialChannel.AddListener("messageReceived", OnChatMessage);

// Канал для экономики
var economyChannel = new MstEventsChannel("economy");
economyChannel.AddListener("purchaseCompleted", OnPurchase);
economyChannel.AddListener("currencyChanged", OnCurrencyUpdate);
```

## Лучшие практики

1. **Используйте осмысленные имена событий**:
```csharp
// Хорошо
"playerJoinedLobby"
"itemCraftingCompleted"
"achievementUnlocked"

// Плохо
"event1"
"update"
"changed"
```

2. **Типизируйте данные событий**:
```csharp
// Определите структуры для сложных данных
public struct ScoreChangedData
{
    public int oldScore;
    public int newScore;
    public string reason;
}

// Используйте их в событиях
channel.Invoke("scoreChanged", new ScoreChangedData 
{ 
    oldScore = 100, 
    newScore = 150, 
    reason = "enemyKilled" 
});
```

3. **Отписывайтесь от событий**:
```csharp
void OnDestroy()
{
    // Отписка от всех событий
    Mst.Events.RemoveListener("playerJoined", OnPlayerJoined);
    channel.RemoveAllListeners();
}
```

4. **Используйте каналы для логической группировки**:
- Игровые события → "game"
- UI события → "ui"
- Сетевые события → "network"
- Системные события → "system"

5. **Обрабатывайте исключения**:
```csharp
// При создании канала включайте обработку исключений
var channel = new MstEventsChannel("game", true);
```

## Интеграция с другими системами

```csharp
// Интеграция с аналитикой
Mst.Events.AddListener("levelCompleted", (msg) => {
    var data = msg.As<LevelCompletionData>();
    Analytics.TrackLevelCompletion(data);
});

// Интеграция с сохранениями
Mst.Events.AddListener("gameStateChanged", (msg) => {
    SaveSystem.SaveGameState(msg.As<GameState>());
});

// Интеграция с сетью
Mst.Events.AddListener("playerAction", (msg) => {
    NetworkManager.SendPlayerAction(msg.As<PlayerAction>());
});
```
