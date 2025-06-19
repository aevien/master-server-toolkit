# 마스터 서버 툴킷 - 알림

## 설명
개별 사용자, 객실 또는 모든 연결된 사용자에게 알릴 가능성이있는 서버에서 고객에게 메시지를 전송하기위한 알림 모듈.

## notificationModule

알림 모듈의 기본 서버 클래스.

### 설정 :
````csharp
[헤더 ( "일반 설정")]
[Serializefield, Tooltip ( "True, 알림 모듈은 Auth Module 및 Automatically Setup Recepent가 로그인 할 때 구독합니다"]]
보호 된 bool useauthmodule = true;
[Serializefield, Tooltip ( "True, True, 알림 모듈은 객실 모듈에 룸 모듈을 구독하여 룸 플레이어에게 알림을 보냅니다"]].
보호 된 bool useroomsmodule = true;
[Serializefield, Tooltip ( "알림을 보낼 수있는 권한 레벨")]]]]
NotifyPermissionLevel = 1에서 보호됩니다.
[Serializefield]
MaxPromizedMessage의 개인 = 10;

TODO: 나머지 문서는 한국어 번역이 필요합니다.
