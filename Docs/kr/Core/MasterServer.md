# Master Server Toolkit - 핵심

## 설명
Master Server Toolkit(버전 4.20.0)은 전용 서버와 멀티플레이어 게임 구축을 위한 시스템입니다. 핵심은 `Mst` 클래스와 `MasterServerBehaviour` 두 컴포넌트로 구성됩니다.

## Mst 클래스
프레임워크의 중심 클래스이며 모든 주요 시스템에 접근을 제공합니다.

### 주요 속성
```csharp
// 프레임워크 버전과 이름
Mst.Version     // "4.20.0"
Mst.Name        // "Master Server Toolkit"

// 마스터 서버 기본 연결
Mst.Connection  // IClientSocket

// 고급 설정
Mst.Settings    // MstAdvancedSettings
```

### 주요 구성 요소
```csharp
// 클라이언트 관련
Mst.Client      // 게임 클라이언트용 MstClient
Mst.Server      // 게임 서버용 MstServer

// 유틸리티
Mst.Helper      // 보조 메서드
Mst.Security    // 보안 및 암호화
Mst.Create      // 소켓 및 메시지 생성
Mst.Concurrency // 스레드 작업

// 시스템
Mst.Events      // 이벤트 채널
Mst.Runtime     // 런타임 데이터 처리
Mst.Args        // 커맨드라인 인수
```

## MasterServerBehaviour 클래스
Unity에서 마스터 서버 동작을 관리하는 싱글턴입니다.

### 사용 예
```csharp
// 인스턴스 가져오기
var masterServer = MasterServerBehaviour.Instance;

// 이벤트
MasterServerBehaviour.OnMasterStartedEvent += (server) => {
    Debug.Log("마스터 서버 시작!");
};

MasterServerBehaviour.OnMasterStoppedEvent += (server) => {
    Debug.Log("마스터 서버 종료!");
};
```

### 특징
- 씬 시작 시 자동 실행
- 싱글턴 패턴
- IP와 포트를 위한 커맨드라인 인수 지원
- 서버 상태 이벤트 제공

### 커맨드라인 인수
```csharp
// 마스터 서버 IP
Mst.Args.AsString(Mst.Args.Names.MasterIp, defaultIP);

// 마스터 서버 포트
Mst.Args.AsInt(Mst.Args.Names.MasterPort, defaultPort);
```

## 빠른 시작

1. MasterServerBehaviour를 씬에 추가합니다.
2. IP와 포트를 설정합니다.
3. 씬을 실행하거나 커맨드라인 인수를 사용합니다.

```bash
# 인수를 이용한 실행 예시
./MyGameServer.exe -masterip 127.0.0.1 -masterport 5000
```

## 주의 사항
- 각 마스터 서버는 단일 MasterServerBehaviour 인스턴스를 사용합니다.
- Mst 클래스를 처음 사용할 때 모든 구성 요소가 자동으로 초기화됩니다.
- OnMasterStarted/Stopped 이벤트는 서버 실행 전에 구독해야 합니다.
