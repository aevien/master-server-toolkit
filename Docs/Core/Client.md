# Master Server Toolkit - Client

## Описание
Система клиентов для подключения к Master Server. Состоит из базовых классов и помощников подключения.

## BaseClientBehaviour

Базовый класс для создания клиентских компонентов.

### Основные свойства:
```csharp
// Текущее подключение
public IClientSocket Connection { get; protected set; }

// Проверка подключения
public bool IsConnected => Connection != null && Connection.IsConnected;

// Логгер модуля
public Logger Logger { get; set; }
```

### Пример использования:
```csharp
public class MyClientModule : BaseClientBehaviour
{
    protected override void OnInitialize()
    {
        // Инициализация при запуске
        Logger.Info("Module started");
    }
    
    protected override void OnConnectionStatusChanged(ConnectionStatus status)
    {
        Logger.Info($"Connection status: {status}");
    }
}
```

### Основные методы:
```csharp
// Регистрация обработчика сообщений
RegisterMessageHandler(IPacketHandler handler);
RegisterMessageHandler(ushort opCode, IncommingMessageHandler handler);

// Изменение подключения
ChangeConnection(IClientSocket connection, bool clearHandlers = false);

// Очистка подключения
ClearConnection(bool clearHandlers = true);
```

## BaseClientModule

Базовый класс для клиентских модулей.

### Пример:
```csharp
public class GameStatisticsModule : BaseClientModule
{
    public override void OnInitialize(BaseClientBehaviour parentBehaviour)
    {
        base.OnInitialize(parentBehaviour);
        
        // Регистрация обработчиков
        parentBehaviour.RegisterMessageHandler(OpCodes.Statistics, HandleStatistics);
    }
    
    private void HandleStatistics(IIncommingMessage message)
    {
        // Обработка статистики
    }
}
```

## ClientToMasterConnector

Компонент для автоматического подключения к Master Server.

### Настройка:
```csharp
// Добавить на GameObject
var connector = GetComponent<ClientToMasterConnector>();

// Настройка через Inspector или код
connector.serverIp = "192.168.1.100";
connector.serverPort = 5000;
connector.connectOnStart = true;
```

### Аргументы командной строки:
```bash
# Автоматическая настройка IP и порта
./Client.exe -masterip 192.168.1.100 -masterport 5000
```

## ConnectionHelper

Базовый помощник для создания подключений с автоматическими попытками.

### Основные настройки:
```csharp
// Количество попыток подключения
[SerializeField] protected int maxAttemptsToConnect = 5;

// Тайм-аут подключения
[SerializeField] protected float timeout = 5f;

// Автоматическое подключение
[SerializeField] protected bool connectOnStart = true;

// Безопасное подключение
[SerializeField] protected bool useSecure = false;
```

### События:
```csharp
// Успешное подключение
OnConnectedEvent

// Неудачное подключение
OnFailedConnectEvent

// Отключение
OnDisconnectedEvent
```

### Пример кастомного коннектора:
```csharp
public class MyCustomConnector : ConnectionHelper<MyCustomConnector>
{
    protected override void Start()
    {
        // Кастомная логика перед подключением
        base.Start();
    }
    
    protected override void OnConnectedEventHandler(IClientSocket client)
    {
        base.OnConnectedEventHandler(client);
        // Дополнительная логика после подключения
    }
}
```

## Архитектура модулей

### Иерархия модулей:
1. BaseClientBehaviour - основной компонент
2. BaseClientModule - дочерние модули
3. Автоматическая инициализация модулей при запуске

### Пример структуры:
```
GameObject
├── MyClientBehaviour (BaseClientBehaviour)
└── ChildModules
    ├── AuthModule (BaseClientModule)
    ├── ChatModule (BaseClientModule)
    └── ProfileModule (BaseClientModule)
```

## Лучшие практики
1. Используйте ConnectionHelper для управления подключением
2. Наследуйтесь от BaseClientBehaviour для основных компонентов
3. Создавайте специализированные модули через BaseClientModule
4. Регистрируйте обработчики в OnInitialize
5. Очищайте ресурсы в OnDestroy
