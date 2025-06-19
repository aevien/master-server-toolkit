# Master Server Toolkit - 로거

## 설명
이 로깅 시스템은 다양한 레벨과 이름을 가진 로거, 그리고 커스텀 출력 방식을 지원합니다.

## Logger
로그 메시지를 기록하는 기본 클래스입니다.

### 로거 생성
```csharp
// 이름을 가진 로거 생성
Logger logger = Mst.Create.Logger("MyModule");
logger.LogLevel = LogLevel.Info;

// LogManager를 통해 로거 가져오기
Logger networkLogger = LogManager.GetLogger("Network");
```

### 로그 레벨
```csharp
LogLevel.All      // 모든 메시지
LogLevel.Trace    // 상세 추적
LogLevel.Debug    // 디버그 정보
LogLevel.Info     // 일반 정보
LogLevel.Warn     // 경고
LogLevel.Error    // 오류
LogLevel.Fatal    // 치명적 오류
LogLevel.Off      // 로그 비활성화
LogLevel.Global   // 글로벌 설정 사용
```

### 로깅 메서드
```csharp
// 기본 사용
logger.Trace("Entering method GetPlayer()");
logger.Debug("Player position: {0}", playerPos);
logger.Info("Player connected successfully");
logger.Warn("Connection latency is high");
logger.Error("Failed to load game data");
logger.Fatal("Critical server error");

// 조건부 로깅
logger.Debug(player != null, "Player found: " + player.Name);
logger.Log(LogLevel.Info, "Custom message");
```

## LogManager
모든 로거를 관리하는 중앙 매니저입니다.

### 초기화
```csharp
// 기본 초기화
LogManager.Initialize(
    new[] { LogAppenders.UnityConsoleAppender },
    LogLevel.Info
);

// 커스텀 앱렌더 포함 초기화
LogManager.Initialize(new LogHandler[] {
    LogAppenders.UnityConsoleAppender,
    CustomFileAppender,
    NetworkLogAppender
}, LogLevel.Debug);
```

### 전역 설정
```csharp
// 모든 로거의 기본 레벨
LogManager.GlobalLogLevel = LogLevel.Warn;

// 강제 레벨 (모든 것을 덮어씀)
LogManager.LogLevel = LogLevel.Off;
```

## Logs
로거 인스턴스 없이 빠르게 로그를 남길 수 있는 정적 클래스입니다.
```csharp
// 사용 예
Logs.Info("Server started");
Logs.Error("Connection failed");
Logs.Debug("Processing player data");

// 조건부 로그
Logs.Warn(healthPoints < 10, "Player health is critical");
```

## 커스텀 앱렌더
### 파일 앱렌더 예시
```csharp
public static void FileAppender(Logger logger, LogLevel logLevel, object message)
{
    string logPath = Path.Combine(Application.persistentDataPath, "game.log");
    string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{logLevel}] [{logger.Name}] {message}\n";

    File.AppendAllText(logPath, logEntry);
}

// 앱렌더 등록
LogManager.AddAppender(FileAppender);
```

## 사용 예시
### 모듈별 로깅
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
            // 연결 코드

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

## 인수로 설정하기
```csharp
// 애플리케이션 시작 시
void ConfigureLogging()
{
    // 인수에서 레벨 읽기
    string logLevelArg = Mst.Args.AsString(Mst.Args.Names.LogLevel, "Info");
    LogLevel level = (LogLevel)Enum.Parse(typeof(LogLevel), logLevelArg);

    LogManager.GlobalLogLevel = level;

    // 파일 로그 사용 여부
    if (Mst.Args.AsBool(Mst.Args.Names.EnableFileLog, false))
    {
        LogManager.AddAppender(FileAppender);
    }
}

// 실행 예시
// ./Game.exe -logLevel Debug -enableFileLog true
```

## 권장 사항
1. **로거 이름 체계화**
```csharp
Logger("Network.Client")
Logger("Network.Server")
Logger("Game.Player")
Logger("Game.UI")
```
2. **프로덕션 레벨 설정**
- 서버: Info 이상
- 클라이언트: Warn 이상
- 개발 환경: Debug

3. **성능 고려**
```csharp
// 비추천 - 항상 문자열 생성
logger.Debug($"Processing {listItems.Count} items");

// 추천 - 레벨 확인 후
if (logger.IsLogging(LogLevel.Debug))
{
    logger.Debug($"Processing {listItems.Count} items");
}
```

4. **메시지 형식 통일**
```csharp
logger.Info("Player [P12345] joined room [R67890] at position (10, 20, 30)");
```

5. **민감한 정보 주의**
```csharp
logger.Info($"User logged in: {user.Id}"); // ✓
logger.Debug($"Password hash: {password.Substring(0, 8)}***"); // ✓
logger.Error($"Login failed for: {user.Password}"); // ✗
```

## MST와 통합
```csharp
public class MyModule : BaseServerModule
{
    protected override void Initialize()
    {
        Logger.Info("Module initializing...");

        // 이벤트 구독
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
