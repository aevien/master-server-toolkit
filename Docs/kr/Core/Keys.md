# Master Server Toolkit - 키 관리

## 설명
네트워크 메시지, 이벤트, 프로퍼티에서 사용하는 코드와 키들을 모아두는 중앙 레지스트리입니다. 모든 작업에서 타입 안전한 상수를 제공하여 실수를 줄여줍니다.

## MstOpCodes
클라이언트와 서버 간 메시지 교환에 사용되는 작업 코드입니다.

### 기본 코드
```csharp
// 오류 및 핑
MstOpCodes.Error        // 오류 메시지
MstOpCodes.Ping         // 연결 확인

// 인증
MstOpCodes.SignIn       // 로그인
MstOpCodes.SignUp       // 회원가입
MstOpCodes.SignOut      // 로그아웃
```

### 사용 예
```csharp
// 특정 OpCode로 메시지 전송
client.SendMessage(MstOpCodes.SignIn, loginData);

// OpCode 처리기 등록
client.RegisterMessageHandler(MstOpCodes.LobbyInfo, HandleLobbyInfo);

// 사용자 정의 OpCode 생성
public static ushort MyCustomCode = "myCustomAction".ToUint16Hash();
```

### OpCode 카테고리
#### 1. 인증 및 계정
```csharp
MstOpCodes.SignIn
MstOpCodes.SignUp
MstOpCodes.SignOut
MstOpCodes.GetPasswordResetCode
MstOpCodes.ConfirmEmail
MstOpCodes.ChangePassword
```

#### 2. 방과 스폰
```csharp
MstOpCodes.RegisterRoomRequest
MstOpCodes.DestroyRoomRequest
MstOpCodes.GetRoomAccessRequest
MstOpCodes.SpawnProcessRequest
MstOpCodes.CompleteSpawnProcess
```

#### 3. 로비
```csharp
MstOpCodes.CreateLobby
MstOpCodes.JoinLobby
MstOpCodes.LeaveLobby
MstOpCodes.SetLobbyProperties
MstOpCodes.StartLobbyGame
```

#### 4. 채팅
```csharp
MstOpCodes.ChatMessage
MstOpCodes.JoinChannel
MstOpCodes.LeaveChannel
MstOpCodes.PickUsername
```

## MstEventKeys
UI와 게임 로직에서 사용하는 이벤트 키입니다.

### UI 이벤트
```csharp
// 다이얼로그
MstEventKeys.showOkDialogBox
MstEventKeys.hideOkDialogBox
MstEventKeys.showYesNoDialogBox

// 화면 전환
MstEventKeys.showSignInView
MstEventKeys.hideSignInView
MstEventKeys.showLobbyListView
```

### 사용 예
```csharp
// 다이얼로그 표시
Mst.Events.Invoke(MstEventKeys.showOkDialogBox, "환영합니다!");

// 이벤트 구독
Mst.Events.AddListener(MstEventKeys.gameStarted, OnGameStarted);

// 사용자 정의 이벤트
public static string MyCustomEvent = "game.levelCompleted";
```

### 이벤트 카테고리
#### 1. 네비게이션
```csharp
MstEventKeys.goToZone
MstEventKeys.leaveRoom
MstEventKeys.showLoadingInfo
```

#### 2. 게임 이벤트
```csharp
MstEventKeys.gameStarted
MstEventKeys.gameOver
MstEventKeys.playerStartedGame
MstEventKeys.playerFinishedGame
```

#### 3. 화면 요소
```csharp
MstEventKeys.showLoadingInfo
MstEventKeys.hideLoadingInfo
MstEventKeys.showPickUsernameView
```

## MstDictKeys
메시지에 포함되는 딕셔너리 데이터 키입니다.

### 사용자 데이터
```csharp
MstDictKeys.USER_ID          // "-userId"
MstDictKeys.USER_NAME        // "-userName"
MstDictKeys.USER_EMAIL       // "-userEmail"
MstDictKeys.USER_AUTH_TOKEN  // "-userAuthToken"
```

### 사용 예
```csharp
// 데이터가 포함된 메시지 생성
var userData = new MstProperties();
userData.Set(MstDictKeys.USER_NAME, "Player1");
userData.Set(MstDictKeys.USER_EMAIL, "player@game.com");

// 전송
client.SendMessage(MstOpCodes.SignUp, userData);

// 수신 측
string userName = message.AsString(MstDictKeys.USER_NAME);
```

### 키 카테고리
#### 1. 방 관련
```csharp
MstDictKeys.ROOM_ID
MstDictKeys.ROOM_CONNECTION_TYPE
MstDictKeys.WORLD_ZONE
```

#### 2. 로비
```csharp
MstDictKeys.LOBBY_FACTORY_ID
MstDictKeys.LOBBY_NAME
MstDictKeys.LOBBY_PASSWORD
MstDictKeys.LOBBY_TEAM
```

#### 3. 인증
```csharp
MstDictKeys.USER_ID
MstDictKeys.USER_PASSWORD
MstDictKeys.USER_AUTH_TOKEN
MstDictKeys.RESET_PASSWORD_CODE
```

## MstPeerPropertyCodes
연결된 피어(클라이언트/서버)의 속성에 대한 코드입니다.
```csharp
// 기본 속성
MstPeerPropertyCodes.Start

// 등록된 엔티티
MstPeerPropertyCodes.RegisteredRooms
MstPeerPropertyCodes.RegisteredSpawners

// 클라이언트 요청
MstPeerPropertyCodes.ClientSpawnRequest
```

### 사용 예
```csharp
// 피어 속성 설정
peer.SetProperty(MstPeerPropertyCodes.RegisteredRooms, roomsList);

// 속성 가져오기
var rooms = peer.GetProperty(MstPeerPropertyCodes.RegisteredRooms);
```

## 사용자 정의 키 만들기

### OpCodes 확장
```csharp
public static class CustomOpCodes
{
    public static ushort GetPlayerStats = "getPlayerStats".ToUint16Hash();
    public static ushort UpdateInventory = "updateInventory".ToUint16Hash();
    public static ushort CraftItem = "craftItem".ToUint16Hash();
}
```

### EventKeys 확장
```csharp
public static class GameEventKeys
{
    public static string itemCrafted = "game.itemCrafted";
    public static string achievementUnlocked = "game.achievementUnlocked";
    public static string questCompleted = "game.questCompleted";
}
```

### DictKeys 확장
```csharp
public static class CustomDictKeys
{
    public const string PLAYER_LEVEL = "-playerLevel";
    public const string INVENTORY_DATA = "-inventoryData";
    public const string ACHIEVEMENT_ID = "-achievementId";
}
```

## 모범 사례
1. **OpCode는 해시를 사용해 정의**
```csharp
// 권장
public static ushort MyAction = "myAction".ToUint16Hash();

// 지양 - 직접 숫자 사용
public static ushort MyAction = 1234;
```
2. **키 이름은 명확하게 작성**
```csharp
// 권장
MstDictKeys.USER_AUTH_TOKEN

// 지양
"-token"
```
3. **기능별로 그룹화**
```csharp
public struct ChatOpCodes { }
public struct LobbyOpCodes { }
public struct AuthOpCodes { }
```
4. **사용자 정의 키에는 문서 작성**
```csharp
/// <summary>
/// 현재 시즌 플레이어 통계 요청
/// </summary>
public static ushort GetSeasonStats = "getSeasonStats".ToUint16Hash();
```

## 다른 시스템과의 통합
```csharp
// 여러 키를 가진 메시지 생성
var message = new MstProperties();
message.Set(MstDictKeys.USER_ID, userId);
message.Set(MstDictKeys.ROOM_ID, roomId);
message.Set(CustomDictKeys.PLAYER_LEVEL, level);

// 네트워크 전송
Mst.Server.SendMessage(peer, CustomOpCodes.UpdatePlayerData, message);

// 이벤트를 이용한 처리
Mst.Events.AddListener(GameEventKeys.itemCrafted, (msg) => {
    var itemData = msg.As<CraftedItem>();
    // 처리 로직
});
```

## 디버깅과 모니터링
```csharp
// 들어오는 모든 OpCode 로깅
Connection.OnMessageReceived += (msg) => {
    Debug.Log($"Received OpCode: {msg.OpCode} ({GetOpCodeName(msg.OpCode)})");
};

// OpCode 이름 얻기
private string GetOpCodeName(ushort opCode)
{
    var fields = typeof(MstOpCodes).GetFields(BindingFlags.Public | BindingFlags.Static);
    foreach (var field in fields)
    {
        if ((ushort)field.GetValue(null) == opCode)
            return field.Name;
    }
    return "Unknown";
}
```
