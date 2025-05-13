# Master Server Toolkit - World Rooms

## Описание
Модуль WorldRooms расширяет функциональность базового модуля Rooms для создания постоянных игровых зон (локаций) в открытом мире, с возможностью автоматического запуска и управления ими.

## Основные компоненты

### WorldRoomsModule
```csharp
// Настройки
[Header("Zones Settings"), SerializeField]
private string[] zoneScenes; // Сцены зон, которые будут автоматически запущены

// Зависимости
protected SpawnersModule spawnersModule; // Для запуска серверов зон
```

## Автоматическое создание зон мира

```csharp
// При регистрации спаунера автоматически запускаются все зоны
private async void Spawners_OnSpawnerRegisteredEvent(RegisteredSpawner spawner)
{
    await Task.Delay(100);

    foreach (string zoneScene in zoneScenes)
    {
        spawnersModule.Spawn(SpawnerProperties(zoneScene)).WhenDone(task =>
        {
            logger.Info($"{zoneScene} zone status is: {task.Status}");
        });
    }
}
```

## Настройка свойств зоны

```csharp
// Создание настроек для запуска зоны на спаунере
protected virtual MstProperties SpawnerProperties(string zoneId)
{
    var properties = new MstProperties();
    properties.Set(Mst.Args.Names.RoomName, zoneId);        // Имя комнаты
    properties.Set(Mst.Args.Names.RoomOnlineScene, zoneId); // Имя сцены
    properties.Set(Mst.Args.Names.RoomIsPrivate, true);     // Приватная комната
    properties.Set(MstDictKeys.WORLD_ZONE, zoneId);         // Маркер зоны мира
    
    return properties;
}
```

## Получение информации о зоне

```csharp
// На сервере - поиск комнаты зоны по ID
RegisteredRoom zoneRoom = roomsList.Values
    .Where(r => r.Options.CustomOptions.AsString(MstDictKeys.WORLD_ZONE) == zoneId)
    .FirstOrDefault();

// Формирование информации о зоне для отправки клиенту
var game = new GameInfoPacket
{
    Id = zoneRoom.RoomId,
    Address = zoneRoom.Options.RoomIp + ":" + zoneRoom.Options.RoomPort,
    MaxPlayers = zoneRoom.Options.MaxConnections,
    Name = zoneRoom.Options.Name,
    OnlinePlayers = zoneRoom.OnlineCount,
    Properties = GetPublicRoomOptions(message.Peer, zoneRoom, null),
    IsPasswordProtected = !string.IsNullOrEmpty(zoneRoom.Options.Password),
    Type = GameInfoType.Room,
    Region = zoneRoom.Options.Region
};
```

## Клиентский запрос информации о зоне

```csharp
// Запрос информации о зоне по ее ID
Mst.Client.Connection.SendMessage(MstOpCodes.GetZoneRoomInfo, "Forest", (status, response) =>
{
    if (status == ResponseStatus.Success)
    {
        // Получение данных о зоне
        var zoneInfo = response.AsPacket<GameInfoPacket>();
        
        // Подключение к серверу зоны
        string address = zoneInfo.Address;
        int roomId = zoneInfo.Id;
        
        ConnectToZone(address, roomId);
    }
    else
    {
        Debug.LogError("Failed to get zone room info");
    }
});
```

## Переход между зонами

```csharp
// Клиентский код для перехода между зонами
public void RequestZoneTransition(string targetZoneId, Vector3 spawnPosition)
{
    // 1. Запрос информации о целевой зоне
    Mst.Client.Connection.SendMessage(MstOpCodes.GetZoneRoomInfo, targetZoneId, (status, response) =>
    {
        if (status == ResponseStatus.Success)
        {
            // 2. Получение информации о зоне
            var zoneInfo = response.AsPacket<GameInfoPacket>();
            
            // 3. Сохранение позиции появления для новой зоны
            PlayerPrefs.SetFloat("SpawnPosX", spawnPosition.x);
            PlayerPrefs.SetFloat("SpawnPosY", spawnPosition.y);
            PlayerPrefs.SetFloat("SpawnPosZ", spawnPosition.z);
            
            // 4. Отключение от текущей зоны
            Mst.Client.Connection.Disconnect();
            
            // 5. Подключение к новой зоне
            ConnectToZone(zoneInfo.Address, zoneInfo.Id);
        }
    });
}

// Подключение к серверу зоны
private void ConnectToZone(string address, int roomId)
{
    // Разбор адреса в формате "ip:port"
    string[] addressParts = address.Split(':');
    string ip = addressParts[0];
    int port = int.Parse(addressParts[1]);
    
    // Подключение к серверу
    Mst.Client.Connection.Connect(ip, port, (successful, connectedPeer) =>
    {
        if (successful)
        {
            Debug.Log($"Connected to zone server: {address}");
            
            // Присоединение к комнате после подключения к серверу
            Mst.Client.Rooms.JoinRoom(roomId, "", (successfulJoin, roomAccess) =>
            {
                if (successfulJoin)
                {
                    Debug.Log($"Joined zone room: {roomId}");
                }
            });
        }
    });
}
```

## Интеграция с персистентным миром

```csharp
// Пример кода сервера зоны для обработки подключения игрока
protected override void OnPlayerJoinedRoom(RoomPlayer player)
{
    base.OnPlayerJoinedRoom(player);
    
    // Получение расширения пользователя
    var userExt = player.GetExtension<IUserPeerExtension>();
    if (userExt != null)
    {
        // Загрузка данных игрока для текущей зоны
        LoadPlayerZoneData(userExt.UserId);
        
        // Отправка данных о других игроках в зоне
        SendZonePlayersData(player);
    }
}

// Загрузка данных игрока для конкретной зоны
private void LoadPlayerZoneData(string userId)
{
    // Получение данных из профиля или базы данных
    var zoneId = gameObject.scene.name;
    var zoneDataKey = $"zonedata_{zoneId}_{userId}";
    
    // Запрос данных из профиля
    Mst.Server.Profiles.GetProfileValues(userId, new string[] { zoneDataKey }, (success, data) =>
    {
        if (success && data.Has(zoneDataKey))
        {
            // Разбор данных зоны (позиция, инвентарь и т.д.)
            var zoneData = data.AsString(zoneDataKey);
            // Применение данных к игроку
            ApplyZoneDataToPlayer(userId, zoneData);
        }
        else
        {
            // Использование данных по умолчанию для новых игроков в этой зоне
            ApplyDefaultZoneData(userId);
        }
    });
}
```

## Сохранение данных при выходе из зоны

```csharp
// Сервер зоны: обработка выхода игрока из зоны
protected override void OnPlayerLeftRoom(RoomPlayer player)
{
    base.OnPlayerLeftRoom(player);
    
    // Получение расширения пользователя
    var userExt = player.GetExtension<IUserPeerExtension>();
    if (userExt != null)
    {
        // Сохранение данных игрока для текущей зоны
        SavePlayerZoneData(userExt.UserId);
    }
}

// Сохранение данных игрока для конкретной зоны
private void SavePlayerZoneData(string userId)
{
    // Сбор данных игрока (позиция, инвентарь и т.д.)
    string zoneData = GenerateZoneDataForPlayer(userId);
    
    // Формирование ключа зоны
    var zoneId = gameObject.scene.name;
    var zoneDataKey = $"zonedata_{zoneId}_{userId}";
    
    // Сохранение в профиль
    var data = new MstProperties();
    data.Set(zoneDataKey, zoneData);
    
    Mst.Server.Profiles.SetProfileValues(userId, data);
}
```

## Примеры использования

### Настройка перехода между зонами
```csharp
// Зона "Лес" соединяется с зоной "Пещера"
public class ZoneTransition : MonoBehaviour
{
    [SerializeField] private string targetZoneId = "Cave";
    [SerializeField] private Vector3 spawnPosition = new Vector3(10, 0, 10);
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Отправка запроса на переход в другую зону
            var zoneManager = FindObjectOfType<WorldZoneManager>();
            zoneManager.RequestZoneTransition(targetZoneId, spawnPosition);
        }
    }
}
```

### Глобальные события в зонах
```csharp
// Система глобальных событий для синхронизации между зонами
public class GlobalEventsManager : MonoBehaviour
{
    // Отправка глобального события всем зонам
    public void BroadcastGlobalEvent(string eventType, MstProperties eventData)
    {
        // Отправка события на мастер сервер
        var data = new MstProperties();
        data.Set("type", eventType);
        data.Set("data", eventData.ToJson());
        
        Mst.Client.Connection.SendMessage(MstOpCodes.GlobalZoneEvent, data);
    }
}

// На мастер сервере
private Task GlobalZoneEventHandler(IIncomingMessage message)
{
    try
    {
        var data = MstProperties.FromBytes(message.AsBytes());
        string eventType = data.AsString("type");
        string eventData = data.AsString("data");
        
        // Рассылка всем зонам
        foreach (var room in roomsList.Values)
        {
            if (room.Options.CustomOptions.Has(MstDictKeys.WORLD_ZONE))
            {
                room.SendMessage(MstOpCodes.GlobalZoneEvent, data.ToBytes());
            }
        }
        
        return Task.CompletedTask;
    }
    catch (System.Exception ex)
    {
        return Task.FromException(ex);
    }
}
```

## Лучшие практики

1. **Используйте зональное деление** для снижения нагрузки на сервер
2. **Сохраняйте данные** при переходах между зонами
3. **Оптимизируйте передачу данных** - отправляйте только необходимую информацию
4. **Реализуйте плавные переходы** между зонами с экранами загрузки
5. **Используйте общую систему событий** для синхронизации между зонами
6. **Разделяйте обязанности** между сервером зоны и мастер-сервером
