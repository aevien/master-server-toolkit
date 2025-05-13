# Master Server Toolkit - Chat

## Описание
Модуль для создания чат-системы с поддержкой каналов, приватных сообщений и проверки нецензурных слов.

## ChatModule

Основной класс для управления чатом.

### Настройка:
```csharp
[Header("General Settings")]
[SerializeField] protected bool useAuthModule = true;
[SerializeField] protected bool useCensorModule = true;
[SerializeField] protected bool allowUsernamePicking = true;

[SerializeField] protected bool setFirstChannelAsLocal = true;
[SerializeField] protected bool setLastChannelAsLocal = true;

[SerializeField] protected int minChannelNameLength = 5;
[SerializeField] protected int maxChannelNameLength = 25;
```

## Работа с каналами

### Создание/получение канала:
```csharp
// Получить или создать канал
var channel = chatModule.GetOrCreateChannel("general");

// Проверка на запрещенные слова
var channel = chatModule.GetOrCreateChannel("badword", true); // игнорировать проверку
```

### Присоединение к каналу:
```csharp
// Отправка запроса на сервер
Mst.Client.Connection.SendMessage(MstOpCodes.JoinChannel, "general");

// Получение текущих каналов
Mst.Client.Connection.SendMessage(MstOpCodes.GetCurrentChannels);
```

### Управление каналами:
```csharp
// Покинуть канал
Mst.Client.Connection.SendMessage(MstOpCodes.LeaveChannel, "general");

// Установить канал по умолчанию
Mst.Client.Connection.SendMessage(MstOpCodes.SetDefaultChannel, "global");

// Получить список пользователей в канале
Mst.Client.Connection.SendMessage(MstOpCodes.GetUsersInChannel, "general");
```

## Отправка сообщений

### Типы сообщений:
```csharp
public enum ChatMessageType : byte
{
    ChannelMessage, // Сообщение в канал
    PrivateMessage  // Приватное сообщение
}
```

### Отправка сообщений:
```csharp
// Сообщение в канал
var channelMsg = new ChatMessagePacket
{
    MessageType = ChatMessageType.ChannelMessage,
    Receiver = "general",  // имя канала
    Message = "Hello everyone!"
};
Mst.Client.Connection.SendMessage(MstOpCodes.ChatMessage, channelMsg);

// Сообщение в личный канал (без указания получателя)
var localMsg = new ChatMessagePacket
{
    MessageType = ChatMessageType.ChannelMessage,
    Receiver = null, // отправится в DefaultChannel
    Message = "Hello local channel!"
};

// Приватное сообщение
var privateMsg = new ChatMessagePacket
{
    MessageType = ChatMessageType.PrivateMessage,
    Receiver = "username",  // имя пользователя
    Message = "Hello privately!"
};
```

## Управление пользователями

### Установка имени пользователя:
```csharp
// Если allowUsernamePicking = true
Mst.Client.Connection.SendMessage(MstOpCodes.PickUsername, "myUsername");
```

### Работа с ChatUserPeerExtension:
```csharp
// Получить расширение пользователя
var chatUser = peer.GetExtension<ChatUserPeerExtension>();

// Изменить имя пользователя
chatModule.ChangeUsername(peer, "newUsername", true); // сохранить каналы

// Доступ к каналам пользователя
var channels = chatUser.CurrentChannels;
var defaultChannel = chatUser.DefaultChannel;
```

## Интеграция с другими модулями

### Интеграция с AuthModule:
```csharp
// При useAuthModule = true
authModule.OnUserLoggedInEvent += OnUserLoggedInEventHandler;
authModule.OnUserLoggedOutEvent += OnUserLoggedOutEventHandler;

// Автоматическое создание ChatUser при входе
```

### Интеграция с CensorModule:
```csharp
// При useCensorModule = true
// Автоматическая проверка сообщений на нецензурные слова
// Замена запрещенного сообщения на предупреждение
```

## Кастомизация

### Переопределение обработки сообщений:
```csharp
protected override bool TryHandleChatMessage(ChatMessagePacket chatMessage, ChatUserPeerExtension sender, IIncomingMessage message)
{
    // Кастомная логика обработки
    if (chatMessage.Message.StartsWith("/"))
    {
        HandleCommand(chatMessage);
        return true;
    }
    
    return base.TryHandleChatMessage(chatMessage, sender, message);
}
```

### Создание кастомных пользователей:
```csharp
protected override ChatUserPeerExtension CreateChatUser(IPeer peer, string username)
{
    // Создание расширенного пользователя
    return new MyChatUserExtension(peer, username);
}
```

## Клиентский код (MstChatClient)

### Подписка на события:
```csharp
// Подключение к чат-модулю
Mst.Client.Chat.Connection = connection;

// События
Mst.Client.Chat.OnMessageReceivedEvent += (message) => {
    Debug.Log($"[{message.Sender}]: {message.Message}");
};

Mst.Client.Chat.OnLeftChannelEvent += (channel) => {
    Debug.Log($"Left channel: {channel}");
};

Mst.Client.Chat.OnJoinedChannelEvent += (channel) => {
    Debug.Log($"Joined channel: {channel}");
};
```

### Отправка сообщений с клиента:
```csharp
// В канал
Mst.Client.Chat.SendToChannel("general", "Hello world!");

// Приватное сообщение
Mst.Client.Chat.SendToUser("username", "Secret message");

// В локальный канал
Mst.Client.Chat.SendToLocalChannel("Hello local!");
```

## Примеры использования

### Создание игрового чата:
```csharp
public class GameChatUI : MonoBehaviour
{
    [Header("UI")]
    public InputField messageInput;
    public Text chatLog;
    public Dropdown channelDropdown;
    
    void Start()
    {
        // Присоединиться к общему каналу
        Mst.Client.Chat.JoinChannel("general");
        Mst.Client.Chat.SetDefaultChannel("general");
        
        // Событие получения сообщения
        Mst.Client.Chat.OnMessageReceivedEvent += OnMessageReceived;
        
        // События каналов
        Mst.Client.Chat.OnChannelUsersChanged += OnChannelUsersChanged;
    }
    
    private void OnMessageReceived(ChatMessagePacket message)
    {
        string formattedMsg = $"{message.Sender}: {message.Message}\n";
        chatLog.text += formattedMsg;
    }
    
    public void SendMessage()
    {
        if (!string.IsNullOrEmpty(messageInput.text))
        {
            Mst.Client.Chat.SendToDefaultChannel(messageInput.text);
            messageInput.text = "";
        }
    }
}
```

### Система команд в чате:
```csharp
protected override bool TryHandleChatMessage(ChatMessagePacket chatMessage, ChatUserPeerExtension sender, IIncomingMessage message)
{
    if (chatMessage.Message.StartsWith("/"))
    {
        var command = chatMessage.Message.Split(' ')[0].Substring(1);
        var args = chatMessage.Message.Split(' ').Skip(1).ToArray();
        
        switch (command)
        {
            case "whisper":
                if (args.Length >= 2)
                {
                    var targetUser = args[0];
                    var privateMsg = string.Join(" ", args.Skip(1));
                    // Отправить приватное сообщение
                }
                break;
                
            case "join":
                if (args.Length > 0)
                {
                    // Присоединиться к каналу
                }
                break;
        }
        
        message.Respond(ResponseStatus.Success);
        return true;
    }
    
    return base.TryHandleChatMessage(chatMessage, sender, message);
}
```

## Лучшие практики

1. **Всегда используйте AuthModule** для автоматической настройки пользователей
2. **Настройте CensorModule** для фильтрации нецензурных слов
3. **Ограничивайте длину имени канала** для предотвращения злоупотреблений
4. **Используйте приватные сообщения** для чувствительной информации
5. **Создавайте различные каналы** для разных целей (общий, торговля, гильдия)
6. **Очищайте пустые каналы** для оптимизации ресурсов
7. **Логируйте все сообщения** для модерации и анализа