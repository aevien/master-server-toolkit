# 마스터 서버 툴킷 - 네트워킹

## 아키텍처 개요

MST 네트워크 시스템은 다양한 전송 프로토콜 (Websockets, TCP)을 지원하는 유연한 아키텍처를 기반으로합니다.고객과 서버간에 안정적이고 안전한 데이터 전송을 제공합니다.

```
Networking
├── Core Interfaces (IPeer, IClientSocket, IServerSocket)
├── Messages (IIncomingMessage, IOutgoingMessage)
├── Serialization
├── Transport Layer (WebSocketSharp)
└── Extensions
```

## 주요 구성 요소

## 클라이언트 서버 상호 작용

#### ipeer
서버와 클라이언트 간의 연결을 나타냅니다.

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

#### iclientsocket
서버에 연결하기위한 클라이언트 소켓.

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

#### iserversocket
연결을 듣기위한 서버 소켓.

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

### 게시물 시스템

#### 소득 관리
들어오는 메시지를 나타냅니다.

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

#### 나가는 메신저
나가는 메시지를 제시합니다.

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

#### messageFactory
메시지 생성 공장.

```csharp
// Создание сообщений
IOutgoingMessage Create(ushort opCode);                 // Пустое сообщение
IOutgoingMessage Create(ushort opCode, byte[] data);    // С бинарными данными
IOutgoingMessage Create(ushort opCode, string data);    // Со строкой
IOutgoingMessage Create(ushort opCode, int data);       // С числом
```

### 데이터의 직렬화

#### iserializablepacket
직렬화 된 패키지의 인터페이스.

```csharp
// Обязательные методы
void ToBinaryWriter(EndianBinaryWriter writer); // Сериализация
void FromBinaryReader(EndianBinaryReader reader); // Десериализация
```

#### SerializablePacket
직렬화 된 패키지의 기본 클래스.

```csharp
// Реализует ISerializablePacket
// Предоставляет базовые методы для наследников
```

#### 시리얼 라이저
이진 직렬화를위한 유틸리티.

```csharp
byte[] Serialize(object data);    // Сериализация объекта в байты
T Deserialize<T>(byte[] bytes);   // Десериализация байтов в объект
```

## 전송 계층

### WebSocketSharp

```csharp
// Клиентский сокет
WsClientSocket clientSocket = new WsClientSocket();
clientSocket.Connect("127.0.0.1", 5000);

// Серверный сокет
WsServerSocket serverSocket = new WsServerSocket();
serverSocket.Listen(5000);
```

## 템플릿을 사용하십시오

### 핸들러 등록

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

### 게시물 보내기

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

### 패키지 생성

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

## 안전 및 성능

### 보호 화합물 (SSL/TLS)

```csharp
// Настройка SSL клиента
clientSocket.UseSecure = true;

// Настройка SSL сервера
serverSocket.UseSecure = true;
serverSocket.CertificatePath = "certificate.pfx";
serverSocket.CertificatePassword = "password";
```

### tamouts 관리

```csharp
// Установка таймаута подключения
client.Connect("127.0.0.1", 5000, 10); // 10 секунд таймаут

// Установка таймаута ответа
client.SendMessage(opCode, data, callback, 5); // 5 секунд таймаут
```

### 배달 방법

```csharp
// Надежная доставка (гарантирует доставку)
peer.SendMessage(message, DeliveryMethod.Reliable);

// Ненадежная доставка (быстрее, но без гарантий)
peer.SendMessage(message, DeliveryMethod.Unreliable);
```
