# Master Server Toolkit - Notification

## 설명
서버가 클라이언트에게 메시지를 전송할 수 있는 알림 모듈입니다. 개별 사용자, 방의 그룹, 혹은 모든 접속자에게 알림을 보낼 수 있습니다.

## NotificationModule

알림 모듈의 핵심 서버 클래스입니다.

### 설정
```csharp
[Header("General Settings")]
[SerializeField, Tooltip("If true, notification module will subscribe to auth module, and automatically setup recipients when they log in")]
protected bool useAuthModule = true;
[SerializeField, Tooltip("If true, notification module will subscribe to rooms module to be able to send notifications to room players")]
protected bool useRoomsModule = true;
[SerializeField, Tooltip("Permission level to be able to send notifications")]
protected int notifyPermissionLevel = 1;
[SerializeField]
private int maxPromisedMessages = 10;
```

### 의존성
- AuthModule (선택 사항) - 사용자가 로그인할 때 자동으로 수신자 목록에 추가하기 위해 사용
- RoomsModule (선택 사항) - 방 안의 플레이어에게 알림을 보내기 위해 사용

## 주요 메서드

### 알림 보내기
```csharp
// 모듈 가져오기
var notificationModule = Mst.Server.Modules.GetModule<NotificationModule>();

// 모든 사용자에게 전송
notificationModule.NoticeToAll("서버가 5분 후 재시작 됩니다");

// 모든 사용자에게 보내고 새 이용자에게도 전달되도록 저장
notificationModule.NoticeToAll("우리 세계에 오신 것을 환영합니다!", true);

// 특정 사용자에게 전송 (peer ID 이용)
notificationModule.NoticeToRecipient(peerId, "새로운 업적을 획득했습니다");

// 여러 사용자에게 전송
List<int> peerIds = new List<int> { 123, 456, 789 };
notificationModule.NoticeToRecipients(peerIds, "새로운 그룹 퀘스트가 열렸습니다");

// 방의 모든 사용자에게 전송
notificationModule.NoticeToRoom(roomId, new List<int>(), "방이 2분 후 종료됩니다");

// 특정 사용자를 제외하고 방의 모두에게 전송
List<int> ignorePeerIds = new List<int> { 123 };
notificationModule.NoticeToRoom(roomId, ignorePeerIds, "플레이어가 방에 입장했습니다");
```

### 수신자 관리
```csharp
// 수신자 존재 여부 확인
bool hasUser = notificationModule.HasRecipient(userId);

// 수신자 가져오기
NotificationRecipient recipient = notificationModule.GetRecipient(userId);

// 안전하게 수신자 가져오기
if (notificationModule.TryGetRecipient(userId, out NotificationRecipient recipient))
{
    // 알림 전송
    recipient.Notify("개인 메시지");
}

// 수동으로 수신자 추가
NotificationRecipient newRecipient = notificationModule.AddRecipient(userExtension);

// 수신자 제거
notificationModule.RemoveRecipient(userId);
```

## 클라이언트 측 - MstNotificationClient
```csharp
// 클라이언트 가져오기
var notificationClient = Mst.Client.Notifications;

// 알림 구독
notificationClient.Subscribe((isSuccess, error) =>
{
    if (isSuccess)
    {
        Debug.Log("알림 구독 성공");
    }
    else
    {
        Debug.LogError($"알림 구독 실패: {error}");
    }
});

// 알림 수신 이벤트 구독
notificationClient.OnNotificationReceivedEvent += OnNotificationReceived;

// 알림 처리기
private void OnNotificationReceived(string message)
{
    // UI에 표시
    uiNotificationManager.ShowNotification(message);
}

// 구독 해제
notificationClient.Unsubscribe((isSuccess, error) =>
{
    if (isSuccess)
    {
        Debug.Log("알림 구독 해제 성공");
    }
    else
    {
        Debug.LogError($"알림 구독 해제 실패: {error}");
    }
});

// 이벤트 해제
notificationClient.OnNotificationReceivedEvent -= OnNotificationReceived;
```

## 패킷과 구조체

### NotificationPacket
```csharp
public class NotificationPacket : SerializablePacket
{
    public int RoomId { get; set; } = -1;               // 방 ID (방 알림일 경우)
    public string Message { get; set; } = string.Empty; // 알림 텍스트
    public List<int> Recipients { get; set; } = new List<int>();        // 수신자 목록
    public List<int> IgnoreRecipients { get; set; } = new List<int>();  // 제외 대상
}
```

### NotificationRecipient
```csharp
public class NotificationRecipient
{
    public string UserId { get; set; }
    public IPeer Peer { get; set; }

    // 특정 수신자에게 알림 전송
    public void Notify(string message)
    {
        Peer.SendMessage(MstOpCodes.Notification, message);
    }
}
```

## 서버 확장 예시
```csharp
public class GameNotificationModule : NotificationModule
{
    // 시스템 알림 형식
    public void SendSystemNotification(string message)
    {
        string formattedMessage = $"[시스템]: {message}";
        NoticeToAll(formattedMessage);
    }

    public void SendAdminNotification(string message, string adminName)
    {
        string formattedMessage = $"[관리자 - {adminName}]: {message}";
        NoticeToAll(formattedMessage, true); // 약속된 메시지로 저장
    }

    public void SendAchievementNotification(int peerId, string achievementTitle)
    {
        string formattedMessage = $"[업적]: '{achievementTitle}'을 획득했습니다!";
        NoticeToRecipient(peerId, formattedMessage);
    }

    // JSON 데이터 알림
    public void SendJSONNotification(int peerId, string type, object data)
    {
        var notification = new JSONNotification
        {
            Type = type,
            Data = JsonUtility.ToJson(data)
        };

        string jsonMessage = JsonUtility.ToJson(notification);
        NoticeToRecipient(peerId, jsonMessage);
    }

    [Serializable]
    private class JSONNotification
    {
        public string Type;
        public string Data;
    }
}
```

## UI 연동 예시
```csharp
public class NotificationUIManager : MonoBehaviour
{
    [SerializeField] private GameObject notificationPrefab;
    [SerializeField] private Transform notificationsContainer;
    [SerializeField] private float displayTime = 5f;

    private void Start()
    {
        // 클라이언트 가져오기
        var notificationClient = Mst.Client.Notifications;

        // 이벤트 구독
        notificationClient.OnNotificationReceivedEvent += ShowNotification;

        // 서버 구독 요청
        notificationClient.Subscribe((isSuccess, error) =>
        {
            if (!isSuccess)
            {
                Debug.LogError($"알림 구독 실패: {error}");
            }
        });
    }

    // 텍스트 알림 처리
    public void ShowNotification(string message)
    {
        if (message.StartsWith("{") && message.EndsWith("}"))
        {
            try
            {
                JsonNotification notification = JsonUtility.FromJson<JsonNotification>(message);
                switch (notification.Type)
                {
                    case "achievement":
                        ShowAchievementNotification(notification.Data);
                        break;
                    case "system":
                        ShowSystemNotification(notification.Data);
                        break;
                    default:
                        CreateTextNotification(message);
                        break;
                }
            }
            catch
            {
                CreateTextNotification(message);
            }
        }
        else
        {
            CreateTextNotification(message);
        }
    }

    // UI 알림 생성
    private void CreateTextNotification(string text)
    {
        GameObject notification = Instantiate(notificationPrefab, notificationsContainer);
        notification.GetComponentInChildren<TextMeshProUGUI>().text = text;
        Destroy(notification, displayTime);
    }

    // 특수 알림 처리 예시
    private void ShowAchievementNotification(string data)
    {
        // 업적 알림 표시 로직
    }

    private void ShowSystemNotification(string data)
    {
        // 시스템 알림 표시 로직
    }

    [Serializable]
    private class JsonNotification
    {
        public string Type;
        public string Data;
    }

    private void OnDestroy()
    {
        var notificationClient = Mst.Client.Notifications;
        if (notificationClient != null)
        {
            notificationClient.OnNotificationReceivedEvent -= ShowNotification;
        }
    }
}
```

## 방에서 사용 예시
```csharp
public class RoomManager : MonoBehaviour, IRoomManager
{
    // 한 플레이어가 준비되면 방의 모든 플레이어에게 알림
    public void OnPlayerReadyStatusChanged(int peerId, bool isReady)
    {
        var player = Mst.Server.Rooms.GetPlayer(currentRoomId, peerId);

        if (player != null && isReady)
        {
            var username = player.GetExtension<IUserPeerExtension>()?.Username ?? "Unknown";

            // 알림 패킷 생성
            var packet = new NotificationPacket
            {
                RoomId = currentRoomId,
                Message = $"플레이어 {username}이(가) 준비되었습니다!",
                IgnoreRecipients = new List<int> { peerId } // 본인에게는 보내지 않음
            };

            // 서버로 전송
            Mst.Server.Connection.SendMessage(MstOpCodes.Notification, packet);
        }
    }
}
```

## 약속된 메시지
알림 모듈은 새 사용자가 로그인했을 때 전달해야 하는 "약속된 메시지"를 저장할 수 있습니다. 이는 모든 플레이어가 받아야 하는 공지나 뉴스에 유용합니다.
```csharp
// 모든 플레이어에게 전송하고 약속된 메시지로 저장
notificationModule.NoticeToAll("새 업데이트 1.2.0이 출시되었습니다!", true);
```

저장되는 약속된 메시지 개수는 다음과 같이 설정할 수 있습니다.
```csharp
[SerializeField] private int maxPromisedMessages = 10;
```

## 모범 사례
1. **알림 유형을 구분하세요** - 다양한 포맷이나 접두사를 사용해 알림 종류를 구분합니다.
2. **복잡한 알림에는 JSON을 사용** - 구조화된 데이터를 전달해야 할 때 활용합니다.
3. **권한 레벨 관리** - `notifyPermissionLevel`을 설정하여 누가 알림을 보낼 수 있는지 제한합니다.
4. **다른 모듈과 연동** - 다른 모듈의 이벤트를 활용해 알림을 보낼 수 있습니다.
5. **클라이언트에서 필터 구현** - 사용자가 어떤 알림을 받을지 선택할 수 있게 합니다.
6. **약속된 메시지는 최소화** - 정말 중요한 시스템 메시지만 약속된 메시지로 저장합니다.
7. **다국어 지원** - 국제화를 위해 로컬라이제이션을 활용하세요.
