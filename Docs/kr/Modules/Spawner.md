# 마스터 서버 툴킷 -Spawner

## 설명
로드 밸런싱 및 큐를 지원하는 다양한 지역에서 게임 서버를 시작하는 프로세스를 관리하기위한 모듈.

## 주요 구성 요소

### spawnersmodule
````csharp
// 설정
[Serializefield] 보호 된 int createSpawnerpermissionLevel = 0;// 스파이너 등록을위한 최소 권리 수준
[Serializefield] 보호 된 플로트 대기열 대기업 = 0.1f;// 큐 갱신 빈도
[Serializefield] Protected Bool enableClientsPawnReques = true;// 고객의 Spaun 요청을 할당합니다

// 이벤트
공개 이벤트 행동 // 스파이너를 등록 할 때
공개 이벤트 // 스파이너를 제거 할 때
공개 이벤트 스폰 된 프로세스 등록 핸들러 onspawnedprocessregisteredEnent;// 프로세스를 등록 할 때
```

TODO: 나머지 문서는 한국어 번역이 필요합니다.
