# Master Server Toolkit - 이벤트 시스템

## 설명
이벤트 시스템은 서로 강하게 결합되지 않은 상태에서 컴포넌트 간 통신을 가능하게 합니다. 이벤트 채널과 형식화된 메시지를 지원합니다.

## MstEventsChannel
이벤트를 보내고 받을 수 있는 채널입니다.

### 채널 생성 예시
```csharp
// 예외 처리가 활성화된 채널 생성
var defaultChannel = new MstEventsChannel("default", true);

// 단순 채널 생성
var gameChannel = new MstEventsChannel("game");

// 전역 채널 사용
var globalChannel = Mst.Events;
```

### 주요 메서드
```csharp
// 데이터 없이 이벤트 전송
channel.Invoke("playerDied");

// 데이터와 함께 전송
channel.Invoke("scoreUpdated", 100);
channel.Invoke("playerJoined", new Player("John"));

// 이벤트 리스너 등록
channel.AddListener("gameStarted", OnGameStarted);
channel.AddListener("levelCompleted", OnLevelCompleted, true);

// 이벤트 리스너 제거
channel.RemoveListener("gameStarted", OnGameStarted);
channel.RemoveAllListeners("playerDied");
```

## EventMessage
데이터를 담는 컨테이너로 타입 안전 접근을 제공합니다.

### 사용법
```csharp
// 빈 메시지 생성
var emptyMsg = EventMessage.Empty;

// 데이터 포함 메시지
var scoreMsg = new EventMessage(150);
var playerMsg = new EventMessage(new Player { Name = "Alex", Level = 10 });

// 데이터 가져오기
int score = scoreMsg.As<int>();
float damage = damageMsg.AsFloat();
string text = textMsg.AsString();
bool isWinner = resultMsg.AsBool();

// 데이터 존재 여부 확인
if (message.HasData())
{
    var data = message.As<MyData>();
}
```

## 사용 예시

### 1. 게임 이벤트
```csharp
public class GameManager : MonoBehaviour
{
    private MstEventsChannel gameEvents;

    void Start()
    {
        gameEvents = new MstEventsChannel("game");

        // 이벤트 구독
        gameEvents.AddListener("playerJoined", OnPlayerJoined);
        gameEvents.AddListener("scoreChanged", OnScoreChanged);
        gameEvents.AddListener("gameOver", OnGameOver);
    }

    private void OnPlayerJoined(EventMessage msg)
    {
        var player = msg.As<Player>();
        Debug.Log($"Player {player.Name} joined the game");

        // 다른 플레이어에게 알리기
        gameEvents.Invoke("playerListUpdated", GetPlayerList());
    }

    private void OnScoreChanged(EventMessage msg)
    {
        int newScore = msg.AsInt();
        UpdateScoreUI(newScore);

        // 최고 점수 확인
        if (newScore > highScore)
        {
            gameEvents.Invoke("newRecord", newScore);
        }
    }
}
```

### 2. UI 이벤트
```csharp
public class UIManager : MonoBehaviour
{
    void Start()
    {
        // 전역 이벤트 구독
        Mst.Events.AddListener("connectionLost", OnConnectionLost);
        Mst.Events.AddListener("dataLoaded", OnDataLoaded);
        Mst.Events.AddListener("errorOccurred", OnError);
    }

    private void OnConnectionLost(EventMessage msg)
    {
        ShowReconnectDialog();
    }

    private void OnDataLoaded(EventMessage msg)
    {
        var data = msg.As<GameData>();
        UpdateUI(data);
        HideLoadingScreen();
    }

    private void OnError(EventMessage msg)
    {
        string errorText = msg.AsString();
        ShowErrorDialog(errorText);
    }
}
```

### 3. 컴포넌트 간 통신
```csharp
public class InventorySystem : MonoBehaviour
{
    private MstEventsChannel inventoryEvents;

    void Start()
    {
        inventoryEvents = new MstEventsChannel("inventory");

        // 게임 이벤트 구독
        Mst.Events.AddListener("itemDropped", OnItemDropped);
        Mst.Events.AddListener("playerDied", OnPlayerDied);
    }

    public void AddItem(Item item)
    {
        if (CanAddItem(item))
        {
            // 아이템 추가
            items.Add(item);

            // 인벤토리 변경 알림
            inventoryEvents.Invoke("itemAdded", item);

            // 퀘스트 아이템 확인
            if (IsQuestItem(item))
            {
                Mst.Events.Invoke("questItemObtained", item);
            }
        }
        else
        {
            inventoryEvents.Invoke("inventoryFull", item);
        }
    }
}
```

## 이름이 있는 채널

### 특화 채널 생성
```csharp
// 전투 시스템 채널
var combatChannel = new MstEventsChannel("combat");
combatChannel.AddListener("enemySpotted", OnEnemySpotted);
combatChannel.AddListener("damageDealt", OnDamageDealt);

// 소셜 시스템 채널
var socialChannel = new MstEventsChannel("social");
socialChannel.AddListener("friendRequestReceived", OnFriendRequest);
socialChannel.AddListener("messageReceived", OnChatMessage);

// 경제 시스템 채널
var economyChannel = new MstEventsChannel("economy");
economyChannel.AddListener("purchaseCompleted", OnPurchase);
economyChannel.AddListener("currencyChanged", OnCurrencyUpdate);
```

## 모범 사례
1. **의미 있는 이벤트 이름 사용**
```csharp
// 좋은 예
"playerJoinedLobby"
"itemCraftingCompleted"
"achievementUnlocked"

// 나쁜 예
"event1"
"update"
"changed"
```

2. **이벤트 데이터에 타입 사용**
```csharp
// 복잡한 데이터는 구조체 정의
public struct ScoreChangedData
{
    public int oldScore;
    public int newScore;
    public string reason;
}

// 이벤트에 사용
channel.Invoke("scoreChanged", new ScoreChangedData
{
    oldScore = 100,
    newScore = 150,
    reason = "enemyKilled"
});
```

3. **이벤트 구독 해제**
```csharp
void OnDestroy()
{
    Mst.Events.RemoveListener("playerJoined", OnPlayerJoined);
    channel.RemoveAllListeners();
}
```

4. **채널을 논리적으로 구분하여 사용**
- 게임 관련 → "game"
- UI 관련 → "ui"
- 네트워크 → "network"
- 시스템 → "system"

5. **예외 처리 활성화**
```csharp
// 채널 생성 시 예외 처리 활성화
var channel = new MstEventsChannel("game", true);
```

## 다른 시스템과의 통합
```csharp
// 분석 시스템과 통합
Mst.Events.AddListener("levelCompleted", (msg) => {
    var data = msg.As<LevelCompletionData>();
    Analytics.TrackLevelCompletion(data);
});

// 저장 시스템과 통합
Mst.Events.AddListener("gameStateChanged", (msg) => {
    SaveSystem.SaveGameState(msg.As<GameState>());
});

// 네트워크와 통합
Mst.Events.AddListener("playerAction", (msg) => {
    NetworkManager.SendPlayerAction(msg.As<PlayerAction>());
});
```
