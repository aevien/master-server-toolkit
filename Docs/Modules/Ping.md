# Master Server Toolkit - Ping

## Описание
Модуль Ping для проверки соединения между клиентом и сервером, измерения задержки и тестирования доступности сервера.

## PingModule

Основной класс модуля Ping.

### Настройка:
```csharp
[SerializeField, TextArea(3, 5)]
private string pongMessage = "Hello, Pong!";
```

### Свойства:
```csharp
public string PongMessage { get; set; }
```

## Использование на сервере

### Инициализация:
```csharp
// Модуль автоматически регистрирует обработчик сообщений Ping
public override void Initialize(IServer server)
{
    server.RegisterMessageHandler(MstOpCodes.Ping, OnPingRequestListener);
}

// Обработчик пинг-запросов
private Task OnPingRequestListener(IIncomingMessage message)
{
    message.Respond(pongMessage, ResponseStatus.Success);
    return Task.CompletedTask;
}
```

### Настройка сообщения Pong:
```csharp
// Получение модуля
var pingModule = Mst.Server.Modules.GetModule<PingModule>();

// Установка сообщения
pingModule.PongMessage = "Game server is running!";
```

## Использование на клиенте

### Отправка Ping:
```csharp
// Отправка пинг-запроса
Mst.Client.Connection.SendMessage(MstOpCodes.Ping, (status, response) =>
{
    if (status == ResponseStatus.Success)
    {
        // Получаем сообщение от сервера
        string pongMessage = response.AsString();
        
        // Вычисляем время отклика (RTT - Round Trip Time)
        float rtt = response.TimeElapsedSinceRequest;
        
        Debug.Log($"Ping successful. RTT: {rtt}ms. Message: {pongMessage}");
    }
    else
    {
        Debug.LogError("Ping failed. Server might be unavailable.");
    }
});
```

### Реализация периодического пинга:
```csharp
public class PingTester : MonoBehaviour
{
    [SerializeField] private float pingInterval = 1f;
    [SerializeField] private int maxFailedPings = 3;
    
    private float nextPingTime = 0f;
    private int failedPingsCount = 0;
    
    private void Update()
    {
        if (Time.time >= nextPingTime && Mst.Client.Connection.IsConnected)
        {
            nextPingTime = Time.time + pingInterval;
            SendPing();
        }
    }
    
    private void SendPing()
    {
        Mst.Client.Connection.SendMessage(MstOpCodes.Ping, (status, response) =>
        {
            if (status == ResponseStatus.Success)
            {
                // Сброс счётчика неудачных попыток
                failedPingsCount = 0;
                
                // Обновление отображения пинга в UI
                float rtt = response.TimeElapsedSinceRequest;
                UpdatePingDisplay(rtt);
            }
            else
            {
                failedPingsCount++;
                
                if (failedPingsCount >= maxFailedPings)
                {
                    // Обработка потери соединения
                    HandleConnectionLost();
                }
            }
        });
    }
    
    private void UpdatePingDisplay(float rtt)
    {
        // Пример обновления UI
        // pingText.text = $"Ping: {Mathf.RoundToInt(rtt)}ms";
    }
    
    private void HandleConnectionLost()
    {
        Debug.LogWarning("Connection to server lost!");
        // Обработка потери соединения с сервером
    }
}
```

## Расширенная реализация Ping

### Обновление модуля для передачи дополнительной информации:

```csharp
public class EnhancedPingModule : PingModule
{
    [SerializeField] private int serverCurrentLoad = 0;
    [SerializeField] private int maxServerLoad = 100;
    
    private Task OnPingRequestListener(IIncomingMessage message)
    {
        // Создаем расширенный ответ с дополнительной информацией
        var pingResponse = new PingResponseInfo
        {
            Message = PongMessage,
            ServerTime = System.DateTime.UtcNow.Ticks,
            ServerLoad = serverCurrentLoad,
            MaxServerLoad = maxServerLoad,
            OnlinePlayers = Mst.Server.ConnectionsCount
        };
        
        // Преобразуем в JSON
        string jsonResponse = JsonUtility.ToJson(pingResponse);
        
        // Отправляем ответ
        message.Respond(jsonResponse, ResponseStatus.Success);
        return Task.CompletedTask;
    }
    
    // Обновление информации о нагрузке
    public void UpdateServerLoad(int currentLoad)
    {
        serverCurrentLoad = currentLoad;
    }
    
    [System.Serializable]
    private class PingResponseInfo
    {
        public string Message;
        public long ServerTime;
        public int ServerLoad;
        public int MaxServerLoad;
        public int OnlinePlayers;
    }
}
```

### Клиентская обработка расширенного ответа:

```csharp
// Отправка запроса
Mst.Client.Connection.SendMessage(MstOpCodes.Ping, (status, response) =>
{
    if (status == ResponseStatus.Success)
    {
        string jsonResponse = response.AsString();
        
        try
        {
            // Парсинг JSON-ответа
            PingResponseInfo pingInfo = JsonUtility.FromJson<PingResponseInfo>(jsonResponse);
            
            // Использование информации
            Debug.Log($"Server message: {pingInfo.Message}");
            Debug.Log($"Server time: {new System.DateTime(pingInfo.ServerTime)}");
            Debug.Log($"Server load: {pingInfo.ServerLoad}/{pingInfo.MaxServerLoad}");
            Debug.Log($"Online players: {pingInfo.OnlinePlayers}");
            
            // Расчёт разницы во времени между клиентом и сервером
            long clientTime = System.DateTime.UtcNow.Ticks;
            TimeSpan timeDifference = new System.DateTime(pingInfo.ServerTime) - new System.DateTime(clientTime);
            Debug.Log($"Time difference: {timeDifference.TotalMilliseconds}ms");
        }
        catch
        {
            // Если ответ в старом формате, обрабатываем как строку
            Debug.Log($"Server message: {jsonResponse}");
        }
    }
});
```

## Интеграция с другими системами

### Мониторинг соединения:
```csharp
public class ConnectionMonitor : MonoBehaviour
{
    [SerializeField] private float pingInterval = 5f;
    [SerializeField] private float maxPingTime = 500f; // ms
    
    private List<float> pingsHistory = new List<float>();
    private int historySize = 10;
    
    private void Start()
    {
        StartCoroutine(PingRoutine());
    }
    
    private IEnumerator PingRoutine()
    {
        while (true)
        {
            if (Mst.Client.Connection.IsConnected)
            {
                SendPing();
            }
            
            yield return new WaitForSeconds(pingInterval);
        }
    }
    
    private void SendPing()
    {
        Mst.Client.Connection.SendMessage(MstOpCodes.Ping, (status, response) =>
        {
            if (status == ResponseStatus.Success)
            {
                float rtt = response.TimeElapsedSinceRequest;
                
                // Добавляем в историю
                pingsHistory.Add(rtt);
                
                // Поддерживаем ограниченный размер истории
                if (pingsHistory.Count > historySize)
                {
                    pingsHistory.RemoveAt(0);
                }
                
                // Проверка на высокий пинг
                if (rtt > maxPingTime)
                {
                    OnHighPingDetected(rtt);
                }
                
                // Уведомление о среднем пинге
                float averagePing = pingsHistory.Average();
                OnPingUpdated(rtt, averagePing);
            }
            else
            {
                OnPingFailed();
            }
        });
    }
    
    // События для интеграции с игровыми системами
    private void OnPingUpdated(float currentPing, float averagePing)
    {
        // Обновление UI или состояния игры
    }
    
    private void OnHighPingDetected(float ping)
    {
        // Предупреждение игрока о плохом соединении
    }
    
    private void OnPingFailed()
    {
        // Обработка неудачной попытки пинга
    }
}
```

### Автоматическое переподключение:
```csharp
public class AutoReconnector : MonoBehaviour
{
    [SerializeField] private float checkInterval = 5f;
    [SerializeField] private int maxFailedPings = 3;
    [SerializeField] private int maxReconnectAttempts = 5;
    
    private int failedPingsCount = 0;
    private int reconnectAttempts = 0;
    
    private void Start()
    {
        StartCoroutine(ConnectionCheckRoutine());
    }
    
    private IEnumerator ConnectionCheckRoutine()
    {
        while (true)
        {
            if (Mst.Client.Connection.IsConnected)
            {
                // Проверка соединения через пинг
                CheckConnection();
            }
            else if (reconnectAttempts < maxReconnectAttempts)
            {
                // Попытка переподключения
                AttemptReconnect();
            }
            
            yield return new WaitForSeconds(checkInterval);
        }
    }
    
    private void CheckConnection()
    {
        Mst.Client.Connection.SendMessage(MstOpCodes.Ping, (status, response) =>
        {
            if (status == ResponseStatus.Success)
            {
                // Соединение работает, сбрасываем счётчики
                failedPingsCount = 0;
                reconnectAttempts = 0;
            }
            else
            {
                failedPingsCount++;
                
                if (failedPingsCount >= maxFailedPings)
                {
                    Debug.LogWarning("Connection lost. Attempting to reconnect...");
                    AttemptReconnect();
                }
            }
        });
    }
    
    private void AttemptReconnect()
    {
        reconnectAttempts++;
        
        Debug.Log($"Reconnect attempt {reconnectAttempts}/{maxReconnectAttempts}");
        
        // Попытка переподключения
        Mst.Client.Connection.Connect(Mst.Client.Connection.ConnectionIp, Mst.Client.Connection.ConnectionPort, (isSuccessful, error) =>
        {
            if (isSuccessful)
            {
                Debug.Log("Successfully reconnected");
                failedPingsCount = 0;
            }
            else
            {
                Debug.LogError($"Failed to reconnect: {error}");
            }
        });
    }
}
```

## Лучшие практики

1. **Используйте Ping для мониторинга состояния соединения** - периодические проверки помогают обнаружить проблемы раньше
2. **Реализуйте автоматическое переподключение** при обнаружении потери соединения
3. **Храните историю Ping** для анализа стабильности соединения
4. **Отображайте пинг в UI** для информирования игроков о качестве соединения
5. **Расширяйте базовый функционал** для передачи дополнительной информации о сервере
6. **Установите разумные интервалы пинга** - слишком частые запросы могут создать дополнительную нагрузку
7. **Адаптируйте геймплей** под текущее состояние соединения, например, уменьшайте количество отправляемых обновлений при высоком пинге
