# Master Server Toolkit - Networking

## Обзор архитектуры

Сетевая система MST построена на гибкой архитектуре с поддержкой различных транспортных протоколов (WebSockets, TCP). Она обеспечивает надежную и безопасную передачу данных между клиентами и серверами.

```
Networking
├── Core Interfaces (IPeer, IClientSocket, IServerSocket)
├── Messages (IIncomingMessage, IOutgoingMessage)
├── Serialization
├── Transport Layer (WebSocketSharp)
└── Extensions
```

## Основные компоненты

### Клиент-серверное взаимодействие

#### IPeer
Представляет соединение между сервером и клиентом.

```csharp
// Основные свойства
int Id { get; }                    // Уникальный ID пира
bool IsConnected { get; }          // Статус подключения
DateTime LastActivity { get; set; } // Время последней активности

// События
event PeerActionHandler OnConnectionOpenEvent;   // Подключение
event PeerActionHandler OnConnectionCloseEvent;  // Отключение

// Расширяемость
T AddExtension<T>(T extension);    // Добавление расширения
T GetExtension<T>();               // Получение расширения
```

#### IClientSocket
Клиентский сокет для подключения к серверу.

```csharp
// Основные свойства
ConnectionStatus Status { get; }   // Статус подключения
bool IsConnected { get; }          // Подключен ли
string Address { get; }            // IP-адрес
int Port { get; }                  // Порт

// Методы подключения
IClientSocket Connect(string ip, int port, float timeoutSeconds);
void Close(bool fireEvent = true);

// Обработка сообщений
IPacketHandler RegisterMessageHandler(ushort opCode, IncommingMessageHandler handler);
```

#### IServerSocket
Серверный сокет для прослушивания подключений.

```csharp
// Безопасность
bool UseSecure { get; set; }                 // Использовать SSL
string CertificatePath { get; set; }         // Путь к сертификату
string CertificatePassword { get; set; }     // Пароль сертификата

// События
event PeerActionHandler OnPeerConnectedEvent;     // Подключение клиента
event PeerActionHandler OnPeerDisconnectedEvent;  // Отключение клиента

// Методы прослушивания
void Listen(int port);                          // Локальное прослушивание
void Listen(string ip, int port);               // Прослушивание на IP:Port
```

### Система сообщений

#### IIncomingMessage
Представляет входящее сообщение.

```csharp
// Основные свойства
ushort OpCode { get; }             // Код операции
byte[] Data { get; }               // Данные сообщения
IPeer Peer { get; }                // Отправитель

// Методы чтения данных
string AsString();                // Чтение данных как строки
int AsInt();                      // Чтение данных как int
float AsFloat();                  // Чтение данных как float
T AsPacket<T>();                  // Десериализация в пакет
```

#### IOutgoingMessage
Представляет исходящее сообщение.

```csharp
// Основные свойства
ushort OpCode { get; }             // Код операции
byte[] Data { get; }               // Данные сообщения

// Методы отправки
void Respond();                   // Отправка пустого ответа
void Respond(byte[] data);        // Отправка бинарных данных
void Respond(ISerializablePacket packet); // Отправка пакета
void Respond(ResponseStatus status);      // Отправка статуса
```

#### MessageFactory
Фабрика для создания сообщений.

```csharp
// Создание сообщений
IOutgoingMessage Create(ushort opCode);                 // Пустое сообщение
IOutgoingMessage Create(ushort opCode, byte[] data);    // С бинарными данными
IOutgoingMessage Create(ushort opCode, string data);    // Со строкой
IOutgoingMessage Create(ushort opCode, int data);       // С числом
```

### Сериализация данных

#### ISerializablePacket
Интерфейс для сериализуемых пакетов.

```csharp
// Обязательные методы
void ToBinaryWriter(EndianBinaryWriter writer); // Сериализация
void FromBinaryReader(EndianBinaryReader reader); // Десериализация
```

#### SerializablePacket
Базовый класс для сериализуемых пакетов.

```csharp
// Реализует ISerializablePacket
// Предоставляет базовые методы для наследников
```

#### Serializer
Утилиты для бинарной сериализации.

```csharp
byte[] Serialize(object data);    // Сериализация объекта в байты
T Deserialize<T>(byte[] bytes);   // Десериализация байтов в объект
```

## Транспортный слой

### WebSocketSharp

```csharp
// Клиентский сокет
WsClientSocket clientSocket = new WsClientSocket();
clientSocket.Connect("127.0.0.1", 5000);

// Серверный сокет
WsServerSocket serverSocket = new WsServerSocket();
serverSocket.Listen(5000);
```

## Шаблоны использования

### Регистрация обработчиков

```csharp
// Регистрация обработчика
client.RegisterMessageHandler(MstOpCodes.Ping, OnPingMessageHandler);

// Обработчик
private void OnPingMessageHandler(IIncomingMessage message)
{
    // Отправка ответа
    message.Respond("Pong");
}
```

### Отправка сообщений

```csharp
// Простая отправка
client.SendMessage(MstOpCodes.Ping);

// Отправка с данными
client.SendMessage(MstOpCodes.Auth, "username:password");

// Отправка пакета
client.SendMessage(MstOpCodes.JoinLobby, new JoinLobbyPacket{ LobbyId = 123 });

// Отправка с ожиданием ответа
client.SendMessage(MstOpCodes.GetRooms, (status, response) =>
{
    if (status == ResponseStatus.Success)
    {
        var rooms = response.AsPacketsList<RoomOptions>();
        ProcessRooms(rooms);
    }
});
```

### Создание пакетов

```csharp
public class PlayerInfoPacket : SerializablePacket
{
    public int PlayerId { get; set; }
    public string Name { get; set; }
    public float Score { get; set; }
    
    public override void ToBinaryWriter(EndianBinaryWriter writer)
    {
        writer.Write(PlayerId);
        writer.Write(Name);
        writer.Write(Score);
    }
    
    public override void FromBinaryReader(EndianBinaryReader reader)
    {
        PlayerId = reader.ReadInt32();
        Name = reader.ReadString();
        Score = reader.ReadFloat();
    }
}
```

## Безопасность и производительность

### Защищенные соединения (SSL/TLS)

```csharp
// Настройка SSL клиента
clientSocket.UseSecure = true;

// Настройка SSL сервера
serverSocket.UseSecure = true;
serverSocket.CertificatePath = "certificate.pfx";
serverSocket.CertificatePassword = "password";
```

### Управление таймаутами

```csharp
// Установка таймаута подключения
client.Connect("127.0.0.1", 5000, 10); // 10 секунд таймаут

// Установка таймаута ответа
client.SendMessage(opCode, data, callback, 5); // 5 секунд таймаут
```

### Методы доставки

```csharp
// Надежная доставка (гарантирует доставку)
peer.SendMessage(message, DeliveryMethod.Reliable);

// Ненадежная доставка (быстрее, но без гарантий)
peer.SendMessage(message, DeliveryMethod.Unreliable);
```
