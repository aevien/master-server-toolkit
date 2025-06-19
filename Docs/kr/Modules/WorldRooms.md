# 마스터 서버 툴킷 - 월드 룸

## 설명
Worldroom 모듈은 기본 룸 모듈의 기능을 확장하여 오픈 월드에서 지속적인 게임 영역 (위치)을 생성하며 자동으로 시작하고 관리 할 수 ​​있습니다.

## 주요 구성 요소

### WorldRomsModule
````csharp
// 설정
[헤더 ( "영역 설정"), Serializefield]
개인 문자열 [] Zonescenes;// 자동으로 시작될 영역의 장면

// 종속성
보호 된 SpawnersModule SpawnersModule;// ZONE SERVER를 시작합니다
```

## 세계 구역의 자동 생성

````csharp

TODO: 나머지 문서는 한국어 번역이 필요합니다.
