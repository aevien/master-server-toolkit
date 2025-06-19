# 마스터 서버 툴킷 - 검열

## 설명
외설적 인 어휘 또는 모욕과 같은 원치 않는 콘텐츠를 필터링하기위한 검열 모듈.플레이어 간의 안전한 의사 소통을 제공합니다.

## censormodule

검열 모듈의 주요 클래스.

### 설정 :
````csharp
[헤더 ( "설정")]]]
[Serializefield] 개인 TextAsset [] WordStrists;
[Serializefield, Textarea (5, 10)] 개인 문자열 matchpattern = @"\ b {0} \ b";
```

## H 초기화 :
````csharp
// 무대에서 모듈 추가
var censormodule = gameObject.addComponent <censormodule> ();

TODO: 나머지 문서는 한국어 번역이 필요합니다.
