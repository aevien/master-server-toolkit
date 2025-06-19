# Master Server Toolkit - 서버

## 설명
서버 애플리케이션을 구축하기 위한 기본 인프라를 제공합니다. ServerBehaviour를 통한 네트워크 서버와 BaseServerModule을 통한 모듈 구성이 포함됩니다.

## ServerBehaviour
자동으로 연결과 모듈을 관리하는 서버용 기본 클래스입니다.

### 주요 속성
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

### 서버 제어
```csharp
// 서버 시작
server.StartServer();
server.StartServer(8080);
server.StartServer("192.168.1.100", 8080);

// 서버 중지
server.StopServer();

// 상태 확인
bool isRunning = server.IsRunning;
int connectedClients = server.PeersCount;
```

### 서버 이벤트
```csharp
// 클라이언트 접속
server.OnPeerConnectedEvent += (peer) => {
    Debug.Log($"Client {peer.Id} connected");
};

// 클라이언트 종료
server.OnPeerDisconnectedEvent += (peer) => {
    Debug.Log($"Client {peer.Id} disconnected");
};

// 서버 시작/중지
server.OnServerStartedEvent += () => Debug.Log("Server started");
server.OnServerStoppedEvent += () => Debug.Log("Server stopped");
```

### 메시지 핸들러 등록
```csharp
// 메시지 핸들러 등록
server.RegisterMessageHandler(MstOpCodes.SignIn, HandleSignIn);

// 비동기 처리 예
server.RegisterMessageHandler(CustomOpCodes.GetData, async (message) => {
    var data = await LoadDataAsync();
    message.Respond(data, ResponseStatus.Success);
});
```

## BaseServerModule
서버 모듈을 작성하기 위한 기본 클래스입니다.

### 모듈 예시
```csharp
public class AccountsModule : BaseServerModule
{
    protected override void Awake()
    {
        base.Awake();
        // 의존성 추가
        AddDependency<DatabaseModule>();
        AddOptionalDependency<EmailModule>();
    }

    public override void Initialize(IServer server)
    {
        // 핸들러 등록
        server.RegisterMessageHandler(MstOpCodes.SignIn, HandleSignIn);
        server.RegisterMessageHandler(MstOpCodes.SignUp, HandleSignUp);
    }

    private void HandleSignIn(IIncomingMessage message)
    {
        // 로그인 로직
    }
}
```

### 모듈 의존성
```csharp
// 필수
AddDependency<DatabaseModule>();
AddDependency<PermissionsModule>();

// 선택
AddOptionalDependency<EmailModule>();
AddOptionalDependency<AnalyticsModule>();

// 다른 모듈 참조
var dbModule = Server.GetModule<DatabaseModule>();
var emailModule = Server.GetModule<EmailModule>();
```

## 고급 기능

### 커스텀 ServerBehaviour
```csharp
public class GameServerBehaviour : ServerBehaviour
{
    protected override void OnPeerConnected(IPeer peer)
    {
        base.OnPeerConnected(peer);
        // 새 플레이어에 대한 추가 로직
        NotifyOtherPlayers(peer);
    }

    protected override void ValidateConnection(ProvideServerAccessCheckPacket packet, SuccessCallback callback)
    {
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

### 통계와 모니터링
```csharp
// 서버 정보
MstProperties info = server.Info();
MstJson jsonInfo = server.JsonInfo();

// 접속 통계
Debug.Log($"Active clients: {server.PeersCount}");
Debug.Log($"Total clients: {server.Info().Get("Total clients")}");
Debug.Log($"Highest clients: {server.Info().Get("Highest clients")}");
```

## 커맨드라인 인수
```bash
# 기본 파라미터
./Server.exe -masterip 192.168.1.100 -masterport 5000

# 보안 설정
./Server.exe -mstUseSecure true -certificatePath cert.pfx -certificatePassword pass

# 성능 설정
./Server.exe -targetFrameRate 60 -clientInactivityTimeout 30
```

### 인수로 자동 설정
```csharp
// 주소와 포트
serverIp = Mst.Args.AsString(Mst.Args.Names.MasterIp, serverIp);
serverPort = Mst.Args.AsInt(Mst.Args.Names.MasterPort, serverPort);

// 보안
useSecure = Mst.Args.AsBool(Mst.Args.Names.UseSecure, useSecure);
certificatePath = Mst.Args.AsString(Mst.Args.Names.CertificatePath, certificatePath);

// 타임아웃
inactivityTimeout = Mst.Args.AsFloat(Mst.Args.Names.ClientInactivityTimeout, inactivityTimeout);
```

## 연결 관리
### 피어 처리
```csharp
// ID로 피어 얻기
IPeer peer = server.GetPeer(peerId);

// 모든 클라이언트 연결 해제
foreach (var peer in connectedPeers.Values)
{
    peer.Disconnect("Server maintenance");
}

// 인증 정보 확인
var securityInfo = peer.GetExtension<SecurityInfoPeerExtension>();
int permissionLevel = securityInfo.PermissionLevel;
```

### 권한 설정
```csharp
[SerializeField]
private List<PermissionEntry> permissions = new List<PermissionEntry>
{
    new PermissionEntry { key = "admin", permissionLevel = 100 },
    new PermissionEntry { key = "moderator", permissionLevel = 50 }
};

// 권한 확인
if (peer.HasPermission(50))
{
    // 모더레이터 이상 허용
}
```

## 모범 사례
1. **모듈을 사용해 논리를 구분**
   - AuthModule - 인증
   - GameModule - 게임 로직
   - ChatModule - 채팅
   - DatabaseModule - 데이터베이스
2. **의존성 관리**
   - Awake()에서 의존성 선언
   - 추가 기능은 OptionalDependency 사용
3. **오류 처리**
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
4. **성능 최적화**
   - 서버 FPS 제한
   - 타임아웃 조절
   - 최대 연결 수 활용
5. **중요 이벤트 로깅**
```csharp
logger.Info($"Server started on {Address}:{Port}");
logger.Debug($"Module {GetType().Name} initialized");
logger.Error($"Failed to handle message: {ex.Message}");
```
