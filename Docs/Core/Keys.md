# Master Server Toolkit - Keys

## Описание
Центральный реестр ключей и кодов для сетевых сообщений, событий и словарных данных. Обеспечивает типизированные константы для всех операций в фреймворке.

## MstOpCodes

Операционные коды для сетевых сообщений между клиентом и сервером.

### Базовые операции:
```csharp
// Ошибки и пинг
MstOpCodes.Error        // Сообщения об ошибках
MstOpCodes.Ping         // Проверка связи

// Аутентификация
MstOpCodes.SignIn       // Вход в аккаунт
MstOpCodes.SignUp       // Регистрация
MstOpCodes.SignOut      // Выход
```

### Пример использования:
```csharp
// Отправка сообщения с определенным OpCode
client.SendMessage(MstOpCodes.SignIn, loginData);

// Регистрация обработчика для OpCode
client.RegisterMessageHandler(MstOpCodes.LobbyInfo, HandleLobbyInfo);

// Создание кастомного OpCode
public static ushort MyCustomCode = "myCustomAction".ToUint16Hash();
```

### Категории OpCodes:

#### 1. Аутентификация и аккаунты:
```csharp
MstOpCodes.SignIn
MstOpCodes.SignUp
MstOpCodes.SignOut
MstOpCodes.GetPasswordResetCode
MstOpCodes.ConfirmEmail
MstOpCodes.ChangePassword
```

#### 2. Комнаты и спавны:
```csharp
MstOpCodes.RegisterRoomRequest
MstOpCodes.DestroyRoomRequest
MstOpCodes.GetRoomAccessRequest
MstOpCodes.SpawnProcessRequest
MstOpCodes.CompleteSpawnProcess
```

#### 3. Лобби:
```csharp
MstOpCodes.CreateLobby
MstOpCodes.JoinLobby
MstOpCodes.LeaveLobby
MstOpCodes.SetLobbyProperties
MstOpCodes.StartLobbyGame
```

#### 4. Чат:
```csharp
MstOpCodes.ChatMessage
MstOpCodes.JoinChannel
MstOpCodes.LeaveChannel
MstOpCodes.PickUsername
```

## MstEventKeys

Ключи для системы событий, используемые для UI и игровой логики.

### UI события:
```csharp
// Диалоги
MstEventKeys.showOkDialogBox
MstEventKeys.hideOkDialogBox
MstEventKeys.showYesNoDialogBox

// Экраны
MstEventKeys.showSignInView
MstEventKeys.hideSignInView
MstEventKeys.showLobbyListView
```

### Пример использования:
```csharp
// Показать диалог
Mst.Events.Invoke(MstEventKeys.showOkDialogBox, "Добро пожаловать!");

// Подписка на событие
Mst.Events.AddListener(MstEventKeys.gameStarted, OnGameStarted);

// Создание кастомных событий
public static string MyCustomEvent = "game.levelCompleted";
```

### Категории событий:

#### 1. Навигация:
```csharp
MstEventKeys.goToZone
MstEventKeys.leaveRoom
MstEventKeys.showLoadingInfo
```

#### 2. Игровые события:
```csharp
MstEventKeys.gameStarted
MstEventKeys.gameOver
MstEventKeys.playerStartedGame
MstEventKeys.playerFinishedGame
```

#### 3. Визуальные элементы:
```csharp
MstEventKeys.showLoadingInfo
MstEventKeys.hideLoadingInfo
MstEventKeys.showPickUsernameView
```

## MstDictKeys

Ключи для словарных данных, передаваемых в сообщениях.

### Пользовательские данные:
```csharp
MstDictKeys.USER_ID          // "-userId"
MstDictKeys.USER_NAME        // "-userName"
MstDictKeys.USER_EMAIL       // "-userEmail"
MstDictKeys.USER_AUTH_TOKEN  // "-userAuthToken"
```

### Пример использования:
```csharp
// Создание сообщения с данными
var userData = new MstProperties();
userData.Set(MstDictKeys.USER_NAME, "Player1");
userData.Set(MstDictKeys.USER_EMAIL, "player@game.com");

// Отправка данных
client.SendMessage(MstOpCodes.SignUp, userData);

// Получение данных
string userName = message.AsString(MstDictKeys.USER_NAME);
```

### Категории ключей:

#### 1. Комнаты:
```csharp
MstDictKeys.ROOM_ID
MstDictKeys.ROOM_CONNECTION_TYPE
MstDictKeys.WORLD_ZONE
```

#### 2. Лобби:
```csharp
MstDictKeys.LOBBY_FACTORY_ID
MstDictKeys.LOBBY_NAME
MstDictKeys.LOBBY_PASSWORD
MstDictKeys.LOBBY_TEAM
```

#### 3. Аутентификация:
```csharp
MstDictKeys.USER_ID
MstDictKeys.USER_PASSWORD
MstDictKeys.USER_AUTH_TOKEN
MstDictKeys.RESET_PASSWORD_CODE
```

## MstPeerPropertyCodes

Коды для свойств пиров (подключенных клиентов/серверов).

```csharp
// Базовые свойства
MstPeerPropertyCodes.Start

// Зарегистрированные сущности
MstPeerPropertyCodes.RegisteredRooms
MstPeerPropertyCodes.RegisteredSpawners

// Клиентские запросы
MstPeerPropertyCodes.ClientSpawnRequest
```

### Пример использования:
```csharp
// Установка свойства пира
peer.SetProperty(MstPeerPropertyCodes.RegisteredRooms, roomsList);

// Получение свойства
var rooms = peer.GetProperty(MstPeerPropertyCodes.RegisteredRooms);
```

## Создание собственных ключей

### Расширение OpCodes:
```csharp
public static class CustomOpCodes
{
    public static ushort GetPlayerStats = "getPlayerStats".ToUint16Hash();
    public static ushort UpdateInventory = "updateInventory".ToUint16Hash();
    public static ushort CraftItem = "craftItem".ToUint16Hash();
}
```

### Расширение EventKeys:
```csharp
public static class GameEventKeys
{
    public static string itemCrafted = "game.itemCrafted";
    public static string achievementUnlocked = "game.achievementUnlocked";
    public static string questCompleted = "game.questCompleted";
}
```

### Расширение DictKeys:
```csharp
public static class CustomDictKeys
{
    public const string PLAYER_LEVEL = "-playerLevel";
    public const string INVENTORY_DATA = "-inventoryData";
    public const string ACHIEVEMENT_ID = "-achievementId";
}
```

## Лучшие практики

1. **Используйте хеширование для OpCodes**:
```csharp
// Хорошо
public static ushort MyAction = "myAction".ToUint16Hash();

// Плохо - прямые числа
public static ushort MyAction = 1234;
```

2. **Делайте ключи описательными**:
```csharp
// Хорошо
MstDictKeys.USER_AUTH_TOKEN

// Плохо
"-token"
```

3. **Группируйте по функциональности**:
```csharp
// Группировка по модулям
public struct ChatOpCodes { }
public struct LobbyOpCodes { }
public struct AuthOpCodes { }
```

4. **Документируйте кастомные ключи**:
```csharp
/// <summary>
/// Получает статистику игрока за текущий сезон
/// </summary>
public static ushort GetSeasonStats = "getSeasonStats".ToUint16Hash();
```

## Интеграция с другими системами

```csharp
// Создание сообщения с несколькими ключами
var message = new MstProperties();
message.Set(MstDictKeys.USER_ID, userId);
message.Set(MstDictKeys.ROOM_ID, roomId);
message.Set(CustomDictKeys.PLAYER_LEVEL, level);

// Отправка через сеть
Mst.Server.SendMessage(peer, CustomOpCodes.UpdatePlayerData, message);

// Обработка с использованием событий
Mst.Events.AddListener(GameEventKeys.itemCrafted, (msg) => {
    var itemData = msg.As<CraftedItem>();
    // Обработка
});
```

## Отладка и мониторинг

```csharp
// Логирование всех входящих OpCodes
Connection.OnMessageReceived += (msg) => {
    Debug.Log($"Received OpCode: {msg.OpCode} ({GetOpCodeName(msg.OpCode)})");
};

// Метод для получения имени OpCode
private string GetOpCodeName(ushort opCode)
{
    var fields = typeof(MstOpCodes).GetFields(BindingFlags.Public | BindingFlags.Static);
    foreach (var field in fields)
    {
        if ((ushort)field.GetValue(null) == opCode)
            return field.Name;
    }
    return "Unknown";
}
```
