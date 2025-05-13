# Master Server Toolkit - Notification

## Описание
Модуль уведомлений для отправки сообщений от сервера клиентам, с возможностью уведомления отдельных пользователей, групп в комнатах или всех подключенных пользователей.

## NotificationModule

Основной серверный класс модуля уведомлений.

### Настройка:
```csharp
[Header("General Settings")]
[SerializeField, Tooltip("If true, notification module will subscribe to auth module, and automatically setup recipients when they log in")]
protected bool useAuthModule = true;
[SerializeField, Tooltip("If true, notification module will subscribe to rooms module to be able to send notifications to room players")]
protected bool useRoomsModule = true;
[SerializeField, Tooltip("Permission level to be able to send notifications")]
protected int notifyPermissionLevel = 1;
[SerializeField]
private int maxPromisedMessages = 10;
```

### Зависимости:
- AuthModule (опционально) - для автоматического добавления пользователей в список получателей при входе в систему
- RoomsModule (опционально) - для отправки уведомлений игрокам в комнатах

## Основные методы

### Отправка уведомлений:
```csharp
// Получить модуль
var notificationModule = Mst.Server.Modules.GetModule<NotificationModule>();

// Отправить всем пользователям
notificationModule.NoticeToAll("Сервер будет перезагружен через 5 минут");

// Отправить всем и добавить в обещанные сообщения (новые пользователи также получат это уведомление при входе)
notificationModule.NoticeToAll("Добро пожаловать в наш мир!", true);

// Отправить конкретному пользователю (по ID пира)
notificationModule.NoticeToRecipient(peerId, "Вы получили новое достижение");

// Отправить группе пользователей
List<int> peerIds = new List<int> { 123, 456, 789 };
notificationModule.NoticeToRecipients(peerIds, "Новое групповое задание доступно");

// Отправить всем пользователям в комнате
notificationModule.NoticeToRoom(roomId, new List<int>(), "Комната будет закрыта через 2 минуты");

// Отправить всем в комнате, кроме указанных пользователей
List<int> ignorePeerIds = new List<int> { 123 };
notificationModule.NoticeToRoom(roomId, ignorePeerIds, "Игрок присоединился к комнате");
```

### Управление получателями:
```csharp
// Проверить наличие получателя
bool hasUser = notificationModule.HasRecipient(userId);

// Получить получателя
NotificationRecipient recipient = notificationModule.GetRecipient(userId);

// Получить получателя безопасно
if (notificationModule.TryGetRecipient(userId, out NotificationRecipient recipient))
{
    // Отправить уведомление
    recipient.Notify("Персональное сообщение");
}

// Добавить получателя вручную
NotificationRecipient newRecipient = notificationModule.AddRecipient(userExtension);

// Удалить получателя
notificationModule.RemoveRecipient(userId);
```

## Клиентская часть - MstNotificationClient

```csharp
// Получить клиент
var notificationClient = Mst.Client.Notifications;

// Подписаться на уведомления
notificationClient.Subscribe((isSuccess, error) =>
{
    if (isSuccess)
    {
        Debug.Log("Успешно подписались на уведомления");
    }
    else
    {
        Debug.LogError($"Ошибка подписки на уведомления: {error}");
    }
});

// Подписаться на событие получения уведомления
notificationClient.OnNotificationReceivedEvent += OnNotificationReceived;

// Обработчик уведомлений
private void OnNotificationReceived(string message)
{
    // Показать уведомление пользователю
    uiNotificationManager.ShowNotification(message);
}

// Отписаться от уведомлений
notificationClient.Unsubscribe((isSuccess, error) =>
{
    if (isSuccess)
    {
        Debug.Log("Успешно отписались от уведомлений");
    }
    else
    {
        Debug.LogError($"Ошибка отписки от уведомлений: {error}");
    }
});

// Отписаться от события
notificationClient.OnNotificationReceivedEvent -= OnNotificationReceived;
```

## Пакеты и структуры

### NotificationPacket:
```csharp
public class NotificationPacket : SerializablePacket
{
    public int RoomId { get; set; } = -1;               // ID комнаты (если отправляется в комнату)
    public string Message { get; set; } = string.Empty; // Текст уведомления
    public List<int> Recipients { get; set; } = new List<int>();        // Список получателей
    public List<int> IgnoreRecipients { get; set; } = new List<int>();  // Исключения
}
```

### NotificationRecipient:
```csharp
public class NotificationRecipient
{
    public string UserId { get; set; }
    public IPeer Peer { get; set; }
    
    // Отправка уведомления конкретному получателю
    public void Notify(string message)
    {
        Peer.SendMessage(MstOpCodes.Notification, message);
    }
}
```

## Серверная реализация - Пользовательский модуль уведомлений

Пример создания расширенного модуля уведомлений:

```csharp
public class GameNotificationModule : NotificationModule
{
    // Форматированные уведомления
    public void SendSystemNotification(string message)
    {
        string formattedMessage = $"[СИСТЕМА]: {message}";
        NoticeToAll(formattedMessage);
    }
    
    public void SendAdminNotification(string message, string adminName)
    {
        string formattedMessage = $"[АДМИН - {adminName}]: {message}";
        NoticeToAll(formattedMessage, true); // Сохраняем как обещанное сообщение
    }
    
    public void SendAchievementNotification(int peerId, string achievementTitle)
    {
        string formattedMessage = $"[ДОСТИЖЕНИЕ]: Вы получили '{achievementTitle}'!";
        NoticeToRecipient(peerId, formattedMessage);
    }
    
    // Уведомления с json-данными
    public void SendJSONNotification(int peerId, string type, object data)
    {
        var notification = new JSONNotification
        {
            Type = type,
            Data = JsonUtility.ToJson(data)
        };
        
        string jsonMessage = JsonUtility.ToJson(notification);
        NoticeToRecipient(peerId, jsonMessage);
    }
    
    // Класс для json-уведомлений
    [Serializable]
    private class JSONNotification
    {
        public string Type;
        public string Data;
    }
}
```

## Интеграция с UI

Пример обработки уведомлений в пользовательском интерфейсе:

```csharp
public class NotificationUIManager : MonoBehaviour
{
    [SerializeField] private GameObject notificationPrefab;
    [SerializeField] private Transform notificationsContainer;
    [SerializeField] private float displayTime = 5f;
    
    private void Start()
    {
        // Получить клиент уведомлений
        var notificationClient = Mst.Client.Notifications;
        
        // Подписаться на события
        notificationClient.OnNotificationReceivedEvent += ShowNotification;
        
        // Подписаться на получение уведомлений от сервера
        notificationClient.Subscribe((isSuccess, error) =>
        {
            if (!isSuccess)
            {
                Debug.LogError($"Failed to subscribe to notifications: {error}");
            }
        });
    }
    
    // Обработка простого текстового уведомления
    public void ShowNotification(string message)
    {
        // Проверка на JSON
        if (message.StartsWith("{") && message.EndsWith("}"))
        {
            try
            {
                // Пытаемся распарсить как JSON
                JsonNotification notification = JsonUtility.FromJson<JsonNotification>(message);
                
                // Отобразить в зависимости от типа
                switch (notification.Type)
                {
                    case "achievement":
                        ShowAchievementNotification(notification.Data);
                        break;
                    case "system":
                        ShowSystemNotification(notification.Data);
                        break;
                    default:
                        CreateTextNotification(message);
                        break;
                }
            }
            catch
            {
                // Если не JSON, отображаем как обычный текст
                CreateTextNotification(message);
            }
        }
        else
        {
            // Обычное текстовое сообщение
            CreateTextNotification(message);
        }
    }
    
    // Создание уведомления в UI
    private void CreateTextNotification(string text)
    {
        GameObject notification = Instantiate(notificationPrefab, notificationsContainer);
        notification.GetComponentInChildren<TextMeshProUGUI>().text = text;
        
        // Автоматически уничтожить через время
        Destroy(notification, displayTime);
    }
    
    // Кастомные обработчики для специальных уведомлений
    private void ShowAchievementNotification(string data)
    {
        // Кастомная логика для отображения уведомления о достижении
    }
    
    private void ShowSystemNotification(string data)
    {
        // Кастомная логика для отображения системного уведомления
    }
    
    [Serializable]
    private class JsonNotification
    {
        public string Type;
        public string Data;
    }
    
    private void OnDestroy()
    {
        // Отписаться
        var notificationClient = Mst.Client.Notifications;
        if (notificationClient != null)
        {
            notificationClient.OnNotificationReceivedEvent -= ShowNotification;
        }
    }
}
```

## Пример использования из комнаты

```csharp
public class RoomManager : MonoBehaviour, IRoomManager
{
    // Отправить уведомление всем игрокам в комнате, когда один из игроков готов
    public void OnPlayerReadyStatusChanged(int peerId, bool isReady)
    {
        var player = Mst.Server.Rooms.GetPlayer(currentRoomId, peerId);
        
        if (player != null && isReady)
        {
            var username = player.GetExtension<IUserPeerExtension>()?.Username ?? "Unknown";
            
            // Создать пакет уведомления
            var packet = new NotificationPacket
            {
                RoomId = currentRoomId,
                Message = $"Игрок {username} готов к игре!",
                IgnoreRecipients = new List<int> { peerId } // Не отправлять самому игроку
            };
            
            // Отправить уведомление через сервер
            Mst.Server.Connection.SendMessage(MstOpCodes.Notification, packet);
        }
    }
}
```

## Обещанные сообщения

Особенность модуля уведомлений - возможность сохранять "обещанные сообщения", которые будут доставлены новым пользователям при входе в систему. Это полезно для системных объявлений, новостей, которые должны получить все игроки.

```csharp
// Отправить всем и сохранить как обещанное сообщение
notificationModule.NoticeToAll("Новое обновление игры! Версия 1.2.0 доступна!", true);
```

Настройка количества сохраняемых обещанных сообщений:
```csharp
[SerializeField] private int maxPromisedMessages = 10;
```

## Лучшие практики

1. **Разделяйте типы уведомлений** - используйте различные форматирование или префиксы для различных типов уведомлений
2. **Используйте JSON для сложных уведомлений** - когда требуется передать структурированные данные
3. **Управляйте уровнями доступа** - настройте `notifyPermissionLevel` для ограничения возможности отправки уведомлений
4. **Интегрируйте с другими модулями** - используйте события из других модулей для отправки уведомлений
5. **Создавайте фильтры уведомлений** на клиенте - позволяйте пользователям настраивать, какие уведомления им показывать
6. **Не злоупотребляйте обещанными сообщениями** - сохраняйте как обещанные только действительно важные системные сообщения
7. **Используйте локализацию** для интернациональных проектов
