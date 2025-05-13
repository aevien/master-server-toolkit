# Master Server Toolkit - Server

## Описание
Базовая инфраструктура для создания серверных приложений. Включает ServerBehaviour для сетевого сервера и BaseServerModule для модулей.

## ServerBehaviour

Базовый класс для создания серверных приложений с автоматическим управлением подключениями и модулями.

### Основные свойства:
```csharp
[Header("Server Settings")]
public string serverIp = "localhost";
public int serverPort = 5000;
public ushort maxConnections = 0;
public string service = "mst";

[Header("Security Settings")]
public bool useSecure = false;
public string password = "mst";
```

### Управление сервером:
```csharp
// Запуск сервера
server.StartServer();
server.StartServer(8080);
server.StartServer("192.168.1.100", 8080);

// Остановка сервера
server.StopServer();

// Проверка статуса
bool isRunning = server.IsRunning;
int connectedClients = server.PeersCount;
```

### События сервера:
```csharp
// Подключение клиента
server.OnPeerConnectedEvent += (peer) => {
    Debug.Log($"Client {peer.Id} connected");
};

// Отключение клиента
server.OnPeerDisconnectedEvent += (peer) => {
    Debug.Log($"Client {peer.Id} disconnected");
};

// Старт/остановка сервера
server.OnServerStartedEvent += () => Debug.Log("Server started");
server.OnServerStoppedEvent += () => Debug.Log("Server stopped");
```

### Регистрация обработчиков:
```csharp
// Регистрация обработчика сообщений
server.RegisterMessageHandler(MstOpCodes.SignIn, HandleSignIn);

// Асинхронный обработчик
server.RegisterMessageHandler(CustomOpCodes.GetData, async (message) => {
    var data = await LoadDataAsync();
    message.Respond(data, ResponseStatus.Success);
});
```

## BaseServerModule

Базовый класс для создания модульных компонентов сервера.

### Создание модуля:
```csharp
public class AccountsModule : BaseServerModule
{
    protected override void Awake()
    {
        base.Awake();
        // Добавление зависимостей
        AddDependency<DatabaseModule>();
        AddOptionalDependency<EmailModule>();
    }
    
    public override void Initialize(IServer server)
    {
        // Регистрация обработчиков
        server.RegisterMessageHandler(MstOpCodes.SignIn, HandleSignIn);
        server.RegisterMessageHandler(MstOpCodes.SignUp, HandleSignUp);
    }
    
    private void HandleSignIn(IIncomingMessage message)
    {
        // Логика входа
    }
}
```

### Зависимости модулей:
```csharp
// Обязательные зависимости
AddDependency<DatabaseModule>();
AddDependency<PermissionsModule>();

// Опциональные зависимости
AddOptionalDependency<EmailModule>();
AddOptionalDependency<AnalyticsModule>();

// Получение других модулей
var dbModule = Server.GetModule<DatabaseModule>();
var emailModule = Server.GetModule<EmailModule>();
```

## Расширенные возможности

### Кастомный ServerBehaviour:
```csharp
public class GameServerBehaviour : ServerBehaviour
{
    protected override void OnPeerConnected(IPeer peer)
    {
        base.OnPeerConnected(peer);
        // Кастомная логика для нового игрока
        NotifyOtherPlayers(peer);
    }
    
    protected override void ValidateConnection(ProvideServerAccessCheckPacket packet, SuccessCallback callback)
    {
        // Кастомная валидация подключения
        if (CheckServerPassword(packet.Password) && CheckGameVersion(packet.Version))
        {
            callback.Invoke(true, string.Empty);
        }
        else
        {
            callback.Invoke(false, "Access denied");
        }
    }
}
```

### Статистика и мониторинг:
```csharp
// Получение информации о сервере
MstProperties info = server.Info();
MstJson jsonInfo = server.JsonInfo();

// Статистика подключений
Debug.Log($"Active clients: {server.PeersCount}");
Debug.Log($"Total clients: {server.Info().Get("Total clients")}");
Debug.Log($"Highest clients: {server.Info().Get("Highest clients")}");
```

## Аргументы командной строки

```bash
# Основные параметры
./Server.exe -masterip 192.168.1.100 -masterport 5000

# Безопасность
./Server.exe -mstUseSecure true -certificatePath cert.pfx -certificatePassword pass

# Производительность
./Server.exe -targetFrameRate 60 -clientInactivityTimeout 30
```

### Автоматическая настройка из аргументов:
```csharp
// Адрес и порт
serverIp = Mst.Args.AsString(Mst.Args.Names.MasterIp, serverIp);
serverPort = Mst.Args.AsInt(Mst.Args.Names.MasterPort, serverPort);

// Безопасность
useSecure = Mst.Args.AsBool(Mst.Args.Names.UseSecure, useSecure);
certificatePath = Mst.Args.AsString(Mst.Args.Names.CertificatePath, certificatePath);

// Таймауты
inactivityTimeout = Mst.Args.AsFloat(Mst.Args.Names.ClientInactivityTimeout, inactivityTimeout);
```

## Работа с подключениями

### Управление пирами:
```csharp
// Получение пира по ID
IPeer peer = server.GetPeer(peerId);

// Отключение всех клиентов
foreach (var peer in connectedPeers.Values)
{
    peer.Disconnect("Server maintenance");
}

// Проверка подлинности
var securityInfo = peer.GetExtension<SecurityInfoPeerExtension>();
int permissionLevel = securityInfo.PermissionLevel;
```

### Права доступа:
```csharp
[SerializeField]
private List<PermissionEntry> permissions = new List<PermissionEntry>
{
    new PermissionEntry { key = "admin", permissionLevel = 100 },
    new PermissionEntry { key = "moderator", permissionLevel = 50 }
};

// Проверка прав
if (peer.HasPermission(50))
{
    // Разрешено для модераторов и выше
}
```

## Лучшие практики

1. **Используйте модули для логической группировки**:
   - AuthModule - аутентификация
   - GameModule - игровая логика
   - ChatModule - чат
   - DatabaseModule - работа с БД

2. **Управляйте зависимостями**:
   - Объявляйте зависимости в Awake()
   - Используйте опциональные зависимости для дополнительных функций

3. **Обрабатывайте ошибки**:
```csharp
protected override void OnMessageReceived(IIncomingMessage message)
{
    try
    {
        base.OnMessageReceived(message);
    }
    catch (Exception ex)
    {
        logger.Error($"Message error: {ex.Message}");
        message.Respond(ResponseStatus.Error);
    }
}
```

4. **Оптимизируйте производительность**:
   - Ограничивайте FPS сервера
   - Настраивайте таймауты
   - Используйте максимальное количество подключений

5. **Логируйте важные события**:
```csharp
logger.Info($"Server started on {Address}:{Port}");
logger.Debug($"Module {GetType().Name} initialized");
logger.Error($"Failed to handle message: {ex.Message}");
```
