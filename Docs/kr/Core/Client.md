# Master Server Toolkit - 클라이언트

## 설명
이 문서는 Master Server에 연결하기 위한 클라이언트 시스템을 설명합니다. 기본 클래스와 연결 도우미들로 구성되어 있습니다.

## BaseClientBehaviour
클라이언트 컴포넌트를 만들 때 상속받는 기본 클래스입니다.

### 주요 속성
```csharp
// 현재 연결
public IClientSocket Connection { get; protected set; }

// 연결 여부
public bool IsConnected => Connection != null && Connection.IsConnected;

// 모듈 로거
public Logger Logger { get; set; }
```

### 사용 예
```csharp
public class MyClientModule : BaseClientBehaviour
{
    protected override void OnInitialize()
    {
        // 시작 시 초기화
        Logger.Info("Module started");
    }

    protected override void OnConnectionStatusChanged(ConnectionStatus status)
    {
        Logger.Info($"Connection status: {status}");
    }
}
```

### 주요 메서드
```csharp
// 메시지 핸들러 등록
RegisterMessageHandler(IPacketHandler handler);
RegisterMessageHandler(ushort opCode, IncommingMessageHandler handler);

// 연결 변경
ChangeConnection(IClientSocket connection, bool clearHandlers = false);

// 연결 해제 및 정리
ClearConnection(bool clearHandlers = true);
```

## BaseClientModule
클라이언트 모듈을 위한 기본 클래스입니다.

### 예시
```csharp
public class GameStatisticsModule : BaseClientModule
{
    public override void OnInitialize(BaseClientBehaviour parentBehaviour)
    {
        base.OnInitialize(parentBehaviour);

        // 핸들러 등록
        parentBehaviour.RegisterMessageHandler(OpCodes.Statistics, HandleStatistics);
    }

    private void HandleStatistics(IIncommingMessage message)
    {
        // 통계 처리
    }
}
```

## ClientToMasterConnector
Master Server에 자동으로 연결해 주는 컴포넌트입니다.

### 설정
```csharp
// GameObject에 추가
var connector = GetComponent<ClientToMasterConnector>();

// 인스펙터나 코드에서 설정
connector.serverIp = "192.168.1.100";
connector.serverPort = 5000;
connector.connectOnStart = true;
```

### 커맨드라인 인수
```bash
# IP와 포트를 자동 설정
./Client.exe -masterip 192.168.1.100 -masterport 5000
```

## ConnectionHelper
자동 재시도를 지원하는 연결 도우미의 기본 클래스입니다.

### 주요 설정
```csharp
// 연결 시도 횟수
[SerializeField] protected int maxAttemptsToConnect = 5;

// 연결 타임아웃
[SerializeField] protected float timeout = 5f;

// 시작 시 자동 연결
[SerializeField] protected bool connectOnStart = true;

// 보안 연결 사용
[SerializeField] protected bool useSecure = false;
```

### 이벤트
```csharp
// 성공적으로 연결됨
OnConnectedEvent

// 연결 실패
OnFailedConnectEvent

// 연결 끊김
OnDisconnectedEvent
```

### 사용자 정의 커넥터 예시
```csharp
public class MyCustomConnector : ConnectionHelper<MyCustomConnector>
{
    protected override void Start()
    {
        // 연결 전 추가 로직
        base.Start();
    }

    protected override void OnConnectedEventHandler(IClientSocket client)
    {
        base.OnConnectedEventHandler(client);
        // 연결 후 추가 로직
    }
}
```

## 모듈 구조

### 모듈 계층
1. BaseClientBehaviour - 기본 컴포넌트
2. BaseClientModule - 자식 모듈들
3. 실행 시 모듈 자동 초기화

### 예시 구조
```
GameObject
├── MyClientBehaviour (BaseClientBehaviour)
└── ChildModules
    ├── AuthModule (BaseClientModule)
    ├── ChatModule (BaseClientModule)
    └── ProfileModule (BaseClientModule)
```

## 모범 사례
1. 연결 관리는 ConnectionHelper를 사용하세요.
2. 기본 컴포넌트는 BaseClientBehaviour를 상속하세요.
3. 특화된 기능은 BaseClientModule을 통해 구현하세요.
4. 메시지 핸들러는 OnInitialize에서 등록하세요.
5. OnDestroy에서 리소스를 정리하세요.
