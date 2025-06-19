# 마스터 서버 툴킷 - 분석

## 설명
사용자 쿼리 및 데이터 여과를 지원하는 게임 이벤트 및 메트릭의 수집, 저장 및 분석 모듈.

## 주요 구성 요소

### AnalyticsModule (서버)
````csharp
// 설정
[Serializefield] 보호 된 Float SavedeBouncetime = 5f;// 데이터베이스를 저장하기 전에 지연됩니다
[Serializefield] Protected Bool UseAnalytics = true;// 분석을 켜거나 끕니다
[Serializefield] 보호 된 데이터 에코 시세 팩토리 데이터 와이션 팩토리;// 데이터베이스 공장
```

### AnalyticsModuleClient (클라이언트)
````csharp
// 이벤트 보내기 (한 번)
void sendeenent (문자열 키, 문자열 범주, 사전 <문자열, 문자열> 데이터);

TODO: 나머지 문서는 한국어 번역이 필요합니다.
