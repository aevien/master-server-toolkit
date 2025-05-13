# Master Server Toolkit - Logger

## Описание
Гибкая система логирования с поддержкой уровней, именованных логгеров и настраиваемых выводов.

## Logger

Основной класс для логирования сообщений.

### Создание логгера:
```csharp
// Создание именованного логгера
Logger logger = Mst.Create.Logger("MyModule");
logger.LogLevel = LogLevel.Info;

// Получение логгера через LogManager
Logger networkLogger = LogManager.GetLogger("Network");
```

### Уровни логирования:
```csharp
LogLevel.All      // Все сообщения
LogLevel.Trace    // Детальное трассирование
LogLevel.Debug    // Отладочная информация
LogLevel.Info     // Информационные сообщения
LogLevel.Warn     // Предупреждения
LogLevel.Error    // Ошибки
LogLevel.Fatal    // Критические ошибки
LogLevel.Off      // Отключить логирование
LogLevel.Global   // Использовать глобальный уровень
```

### Методы логирования:
```csharp
// Базовые методы
logger.Trace("Entering method GetPlayer()");
logger.Debug("Player position: {0}", playerPos);
logger.Info("Player connected successfully");
logger.Warn("Connection latency is high");
logger.Error("Failed to load game data");
logger.Fatal("Critical server error");

// Условное логирование
logger.Debug(player != null, "Player found: " + player.Name);
logger.Log(LogLevel.Info, "Custom message");
```

## LogManager

Центральный менеджер для всех логгеров.

### Инициализация:
```csharp
// Базовая инициализация
LogManager.Initialize(
    new[] { LogAppenders.UnityConsoleAppender },
    LogLevel.Info
);

// Инициализация с кастомными аппендерами
LogManager.Initialize(new LogHandler[] {
    LogAppenders.UnityConsoleAppender,
    CustomFileAppender,
    NetworkLogAppender
}, LogLevel.Debug);
```

### Глобальные настройки:
```csharp
// Глобальный уровень (для всех логгеров)
LogManager.GlobalLogLevel = LogLevel.Warn;

// Принудительный уровень (переопределяет все)
LogManager.LogLevel = LogLevel.Off;
```

## Logs

Статический класс для быстрого логирования без создания логгера.

```csharp
// Использование статических методов
Logs.Info("Server started");
Logs.Error("Connection failed");
Logs.Debug("Processing player data");

// Условное логирование
Logs.Warn(healthPoints < 10, "Player health is critical");
```

## Кастомные аппендеры

### Создание файлового аппендера:
```csharp
public static void FileAppender(Logger logger, LogLevel logLevel, object message)
{
    string logPath = Path.Combine(Application.persistentDataPath, "game.log");
    string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{logLevel}] [{logger.Name}] {message}\n";
    
    File.AppendAllText(logPath, logEntry);
}

// Регистрация аппендера
LogManager.AddAppender(FileAppender);
```

## Примеры использования

### Модульное логирование:
```csharp
public class NetworkManager : MonoBehaviour
{
    private Logger logger;
    
    void Awake()
    {
        logger = Mst.Create.Logger("Network");
        logger.LogLevel = LogLevel.Debug;
    }
    
    public async Task ConnectToServer(string ip, int port)
    {
        logger.Info($"Attempting connection to {ip}:{port}");
        
        try
        {
            logger.Debug("Creating socket...");
            // Код подключения
            
            logger.Info("Successfully connected to server");
        }
        catch (Exception ex)
        {
            logger.Error($"Connection failed: {ex.Message}");
            throw;
        }
    }
}
```

## Настройка через аргументы

```csharp
// При запуске приложения
void ConfigureLogging()
{
    // Получение уровня из аргументов
    string logLevelArg = Mst.Args.AsString(Mst.Args.Names.LogLevel, "Info");
    LogLevel level = (LogLevel)Enum.Parse(typeof(LogLevel), logLevelArg);
    
    LogManager.GlobalLogLevel = level;
    
    // Настройка вывода в файл (если указано)
    if (Mst.Args.AsBool(Mst.Args.Names.EnableFileLog, false))
    {
        LogManager.AddAppender(FileAppender);
    }
}

// Пример запуска
// ./Game.exe -logLevel Debug -enableFileLog true
```

## Рекомендации

1. **Именование логгеров**: Используйте иерархические имена
```csharp
Logger("Network.Client")
Logger("Network.Server")
Logger("Game.Player")
Logger("Game.UI")
```

2. **Уровни в production**:
- Сервер: Info и выше
- Клиент: Warn и выше
- Разработка: Debug

3. **Производительность**:
```csharp
// Плохо - создает строку всегда
logger.Debug($"Processing {listItems.Count} items");

// Хорошо - проверяет уровень сначала
if (logger.IsLogging(LogLevel.Debug))
{
    logger.Debug($"Processing {listItems.Count} items");
}
```

4. **Структура сообщений**:
```csharp
// Последовательный формат
logger.Info("Player [P12345] joined room [R67890] at position (10, 20, 30)");
```

5. **Конфиденциальность**:
```csharp
// Избегайте логирования чувствительных данных
logger.Info($"User logged in: {user.Id}"); // ✓
logger.Debug($"Password hash: {password.Substring(0, 8)}***"); // ✓
logger.Error($"Login failed for: {user.Password}"); // ✗
```

## Интеграция с MST

```csharp
// Использование с модулями
public class MyModule : BaseServerModule
{
    protected override void Initialize()
    {
        Logger.Info("Module initializing...");
        
        // Подписка на события
        Mst.Events.AddListener("playerConnected", msg => {
            Logger.Debug($"Player connected event: {msg}");
        });
    }
    
    protected override void OnDestroy()
    {
        Logger.Info("Module shutting down...");
        base.OnDestroy();
    }
}
```
