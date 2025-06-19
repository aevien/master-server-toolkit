# Master Server Toolkit - 핵심 시스템

## 개요
Master Server Toolkit의 핵심은 멀티플레이어 게임을 구축하기 위한 기본 컴포넌트 모음입니다. 이 컴포넌트들은 서로 긴밀하게 연동되어 유연하고 확장 가능한 구조를 제공합니다.

## 핵심 구조

### [MasterServer](MasterServer.md)
`Mst` 클래스를 통해 모든 시스템에 접근할 수 있는 중심 컴포넌트로 서버의 주요 설정을 포함합니다.

### [Client](Client.md)
서버에 연결하고 요청을 보내며 응답을 처리하는 클라이언트 측 기능을 담당합니다. 클라이언트 모듈의 기본 클래스를 포함합니다.

### [Server](Server.md)
연결 처리, 메시지 핸들러 등록, 모듈 관리를 담당하는 서버 측 구성요소입니다.

### [Database](Database.md)
여러 종류의 데이터베이스를 사용할 수 있도록 추상화된 계층을 제공합니다.

### [Events](Events.md)
컴포넌트 간의 느슨한 결합을 위한 이벤트 시스템을 제공합니다.

### [Keys](Keys.md)
데이터 접근 시 오타를 방지하기 위해 상수와 키를 체계화합니다.

### [Localization](Localization.md)
다양한 언어 지원을 위한 로컬라이제이션 시스템입니다.

### [Logger](Logger.md)
애플리케이션 로그를 남기기 위한 시스템으로 여러 로그 레벨과 포맷을 지원합니다.

### [Mail](Mail.md)
회원 가입 확인, 비밀번호 복구 등을 위한 이메일 발송 기능을 제공합니다.

## 컴포넌트 상호 작용

![아키텍처](../Images/core_architecture.png)

### 주요 연결 구조

1. **MasterServer**는 `Mst` 클래스를 통해 다른 모든 컴포넌트에 접근합니다:
   ```csharp
   // 클라이언트 접근
   Mst.Client

   // 서버 접근
   Mst.Server

   // 이벤트 시스템
   Mst.Events
   ```
2. **Client와 Server**는 동일한 네트워크 메시징 시스템을 사용하지만 서로 다른 측에서 동작합니다:
   ```csharp
   // 클라이언트 측
   Mst.Client.Connection.SendMessage(MstOpCodes.SignIn, credentials);

   // 서버 측
   server.RegisterMessageHandler(MstOpCodes.SignIn, HandleSignIn);
   ```
3. **Database**는 서버 모듈에서 데이터에 접근할 때 사용됩니다:
   ```csharp
   var dbAccessor = Mst.Server.DbAccessors.GetAccessor<IAccountsDatabaseAccessor>();
   var account = await dbAccessor.GetAccountByUsername(username);
   ```
4. **Events**는 모든 컴포넌트 간 정보를 느슨하게 교환할 때 이용됩니다:
   ```csharp
   Mst.Events.Invoke("userLoggedIn", userId);
   Mst.Events.AddListener("userLoggedIn", OnUserLoggedIn);
   ```
5. **Logger**는 디버그와 모니터링에 활용됩니다:
   ```csharp
   Mst.Logger.Debug("Connection established");
   Mst.Logger.Error($"Failed to connect: {error}");
   ```
6. **Keys**는 데이터 접근을 표준화합니다:
   ```csharp
   properties.Set(MstDictKeys.USER_ID, userId);
   properties.Set(MstDictKeys.USER_NAME, username);
   ```
7. **Localization**은 다국어 지원을 제공합니다:
   ```csharp
   string welcomeText = Mst.Localization.GetString("welcome_message");
   ```
8. **Mail**은 서버 모듈이 사용자에게 메일을 보낼 때 사용됩니다:
   ```csharp
   await Mst.Server.Mail.SendEmailAsync(email, subject, body);
   ```

## 초기화 과정
1. `MasterServerBehaviour` 인스턴스 생성
2. IP, 포트, 보안 등 연결 설정 정의
3. 필요한 모듈 등록
4. 서버 시작 또는 클라이언트 연결
5. 등록된 모든 모듈 초기화
6. 메시지 핸들러 설정

## 모듈 설계 원칙
핵심은 모듈 생성을 위한 기본 클래스를 제공합니다:
- `BaseServerModule` - 서버 모듈용
- `BaseClientModule` - 클라이언트 모듈용

모듈은 다음 원칙을 따릅니다:
1. **단일 책임** - 각 모듈은 한 가지 기능만 담당
2. **낮은 결합도** - 이벤트와 공통 인터페이스를 통해 소통
3. **확장성** - 기본 동작을 상속 및 재정의 가능
4. **추상화 의존** - 구체 구현보다 인터페이스 사용

## 핵심 사용 팁
1. 직접 호출 대신 이벤트를 활용하여 결합도를 낮춥니다.
2. 로직은 전용 모듈로 분리해 캡슐화합니다.
3. 기본 컴포넌트 구조를 따라 새로운 기능을 구현합니다.
4. 새로 만들기보다는 제공되는 기본 클래스를 활용합니다.
5. 인터페이스 문서를 작성하여 유지 보수를 쉽게 합니다.
