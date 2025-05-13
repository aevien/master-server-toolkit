# Master Server Toolkit - Spawner

## Описание
Модуль для управления процессами запуска игровых серверов в различных регионах с поддержкой балансировки нагрузки и очередей.

## Основные компоненты

### SpawnersModule
```csharp
// Настройки
[SerializeField] protected int createSpawnerPermissionLevel = 0; // Минимальный уровень прав для регистрации спаунера
[SerializeField] protected float queueUpdateFrequency = 0.1f;    // Частота обновления очередей
[SerializeField] protected bool enableClientSpawnRequests = true; // Разрешить запросы на спаун от клиентов

// События
public event Action<RegisteredSpawner> OnSpawnerRegisteredEvent;  // При регистрации спаунера
public event Action<RegisteredSpawner> OnSpawnerDestroyedEvent;   // При удалении спаунера
public event SpawnedProcessRegistrationHandler OnSpawnedProcessRegisteredEvent; // При регистрации процесса
```

### RegisteredSpawner
```csharp
// Основные свойства
public int SpawnerId { get; }                // Уникальный ID спаунера
public IPeer Peer { get; }                   // Подключение к спаунеру
public SpawnerOptions Options { get; }       // Настройки спаунера
public int ProcessesRunning { get; }         // Количество запущенных процессов
public int QueuedTasks { get; }              // Количество задач в очереди

// Методы
public bool CanSpawnAnotherProcess();        // Может ли запустить еще один процесс
public int CalculateFreeSlotsCount();        // Расчет количества свободных слотов
```

### SpawnerOptions
```csharp
// Настройки спаунера
public string MachineIp { get; set; }           // IP машины спаунера 
public int MaxProcesses { get; set; } = 5;      // Максимальное количество процессов
public string Region { get; set; } = "Global";  // Регион спаунера
public Dictionary<string, string> CustomOptions { get; set; } // Дополнительные настройки
```

### SpawnTask
```csharp
// Свойства
public int Id { get; }                      // ID задачи
public RegisteredSpawner Spawner { get; }   // Спаунер, выполняющий задачу
public MstProperties Options { get; }       // Настройки запуска
public SpawnStatus Status { get; }          // Текущий статус
public string UniqueCode { get; }           // Уникальный код для безопасности
public IPeer Requester { get; set; }        // Запросивший клиент
public IPeer RegisteredPeer { get; private set; } // Зарегистрированный процесс

// События
public event Action<SpawnStatus> OnStatusChangedEvent; // При изменении статуса
```

## Статусы процесса SpawnStatus
```csharp
public enum SpawnStatus
{
    None,               // Начальный статус
    Queued,             // В очереди
    ProcessStarted,     // Процесс запущен
    ProcessRegistered,  // Процесс зарегистрирован
    Finalized,          // Финализирован
    Aborted,            // Прерван
    Killed              // Убит
}
```

## Процесс работы

### Регистрация спаунера
```csharp
// Клиент
var options = new SpawnerOptions
{
    MachineIp = "192.168.1.10",
    MaxProcesses = 10,
    Region = "eu-west",
    CustomOptions = new Dictionary<string, string> {
        { "mapsList", "map1,map2,map3" }
    }
};

Mst.Client.Spawners.RegisterSpawner(options, (successful, spawnerId) =>
{
    if (successful)
        Debug.Log($"Spawner registered with ID: {spawnerId}");
});

// Сервер
RegisteredSpawner spawner = CreateSpawner(peer, options);
```

### Запрос на запуск игрового сервера
```csharp
// Клиент
var spawnOptions = new MstProperties();
spawnOptions.Set(Mst.Args.Names.RoomName, "MyGame");
spawnOptions.Set(Mst.Args.Names.RoomMaxPlayers, 10);
spawnOptions.Set(Mst.Args.Names.RoomRegion, "eu-west");

Mst.Client.Spawners.RequestSpawn(spawnOptions, (successful, spawnId) =>
{
    if (successful)
    {
        Debug.Log($"Spawn request created with ID: {spawnId}");
        
        // Подписка на изменения статуса
        Mst.Client.Spawners.OnStatusChangedEvent += OnSpawnStatusChanged;
    }
});

// Сервер
SpawnTask task = Spawn(options, region);
```

### Выбор спаунера для запуска
```csharp
// Фильтрация спаунеров по региону
var spawners = GetSpawnersInRegion(region);

// Сортировка по доступным слотам
var availableSpawners = spawners
    .OrderByDescending(s => s.CalculateFreeSlotsCount())
    .Where(s => s.CanSpawnAnotherProcess())
    .ToList();

// Выбор спаунера
if (availableSpawners.Count > 0)
{
    return availableSpawners[0];
}
```

### Завершение процесса спауна
```csharp
// На стороне процесса
var finalizationData = new MstProperties();
finalizationData.Set("roomId", 12345);
finalizationData.Set("connectionAddress", "192.168.1.10:12345");

// Отправка данных о завершении
Mst.Client.Spawners.FinalizeSpawn(spawnTaskId, finalizationData);

// На стороне клиента
Mst.Client.Spawners.GetFinalizationData(spawnId, (successful, data) =>
{
    if (successful)
    {
        string connectionAddress = data.AsString("connectionAddress");
        int roomId = data.AsInt("roomId");
        
        // Подключение к комнате
        ConnectToRoom(connectionAddress, roomId);
    }
});
```

## Управление регионами

```csharp
// Получение всех регионов
List<RegionInfo> regions = spawnersModule.GetRegions();

// Получение спаунеров в конкретном регионе
List<RegisteredSpawner> regionalSpawners = spawnersModule.GetSpawnersInRegion("eu-west");

// Создание спаунера в регионе
var options = new SpawnerOptions { Region = "eu-west" };
var spawner = spawnersModule.CreateSpawner(peer, options);
```

## Балансировка нагрузки

```csharp
// Пример балансировки по наименее загруженным серверам
public RegisteredSpawner GetLeastBusySpawner(string region)
{
    var spawners = GetSpawnersInRegion(region);
    
    // Если нет спаунеров в регионе, используем все доступные
    if (spawners.Count == 0)
        spawners = GetSpawners();
    
    // Сортировка по загруженности
    return spawners
        .OrderByDescending(s => s.CalculateFreeSlotsCount())
        .FirstOrDefault(s => s.CanSpawnAnotherProcess());
}

// Расширенная логика выбора спаунера
public RegisteredSpawner GetOptimalSpawner(MstProperties options)
{
    string region = options.AsString(Mst.Args.Names.RoomRegion, "");
    string gameMode = options.AsString("gameMode", "");
    
    // Фильтрация по региону и игровому режиму
    var filtered = spawnersList.Values
        .Where(s => (string.IsNullOrEmpty(region) || s.Options.Region == region) &&
                  (string.IsNullOrEmpty(gameMode) || 
                   s.Options.CustomOptions.ContainsKey("gameModes") && 
                   s.Options.CustomOptions["gameModes"].Contains(gameMode)))
        .ToList();
    
    return filtered
        .OrderByDescending(s => s.CalculateFreeSlotsCount())
        .FirstOrDefault(s => s.CanSpawnAnotherProcess());
}
```

## Практический пример

### Настройка системы:
```csharp
// 1. Регистрация спаунеров
RegisterSpawner("EU", "10.0.0.1", 10);
RegisterSpawner("US", "10.0.1.1", 15);
RegisterSpawner("ASIA", "10.0.2.1", 8);

// 2. Клиентский запрос на создание игрового сервера
var options = new MstProperties();
options.Set(Mst.Args.Names.RoomName, "CustomGame");
options.Set(Mst.Args.Names.RoomMaxPlayers, 16);
options.Set(Mst.Args.Names.RoomRegion, "EU");
options.Set("gameMode", "deathmatch");

// 3. Обработка запроса, выбор спаунера и запуск процесса
SpawnTask task = spawnersModule.Spawn(options, "EU");

// 4. Клиент ожидает завершения процесса
// 5. Созданный процесс регистрируется и отправляет данные для подключения
```

## Лучшие практики

1. **Группируйте спаунеры по регионам** для оптимальной задержки
2. **Настраивайте лимиты процессов** для каждого спаунера с учетом мощности сервера
3. **Используйте кастомные опции** для гибкой настройки спаунера
4. **Реализуйте отказоустойчивость** - если спаунер недоступен, перенаправьте задачу в другой регион
5. **Мониторьте загруженность спаунеров** для обнаружения узких мест
6. **Добавляйте тайм-ауты для задач** в очереди, чтобы избежать застревания
