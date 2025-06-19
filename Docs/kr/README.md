# 마스터 서버 툴킷 - 문서

## 설명
마스터 서버 툴킷은 클라이언트-서버 아키텍처로 멀티플레이어 게임을 제작하기 위한 프레임워크입니다. 인증, 프로필, 방, 로비, 채팅 등 다양한 멀티플레이어 기능을 위한 모듈을 제공합니다.

## 기본 모듈

### 시스템의 핵심
- [Authentication](Modules/Authentication.md) - 사용자의 인증 및 관리
- [Profiles](Modules/Profiles.md) - 사용자 프로필과 데이터 관리
- [Rooms](Modules/Rooms.md) - 방과 게임 세션 시스템

### 게임 모듈
- [Achievements](Modules/Achievements.md) - 업적 시스템
- [Censor](Modules/Censor.md) - 부적절한 콘텐츠 필터링
- [Chat](Modules/Chat.md) - 채팅 및 메시지 시스템
- [Lobbies](Modules/Lobbies.md) - 게임 전 로비 시스템
- [Matchmaker](Modules/Matchmaker.md) - 매치메이킹 및 필터링
- [Notification](Modules/Notification.md) - 알림 시스템
- [Ping](Modules/Ping.md) - 연결 확인과 지연 측정

### 인프라
- [Spawner](Modules/Spawner.md) - 게임 서버 실행
- [WebServer](Modules/WebServer.md) - API와 관리자 패널을 위한 내장 웹서버

### 분석 및 모니터링
- [AnalyticsModule](Modules/AnalyticsModule.md) - 게임 이벤트 수집 및 분석

### 도구
- [Tools](Tools/README.md) - 개발 보조 도구 모음
  - [UI Framework](Tools/UI/README.md) - 사용자 인터페이스 시스템
  - [Attributes](Tools/Attributes.md) - Unity 인스펙터 확장
  - [Terminal](Tools/Terminal.md) - 디버그 터미널
  - [Tweener](Tools/Tweener.md) - 애니메이션 도구
  - [Utilities](Tools/Utilities.md) - 유틸리티 모음
