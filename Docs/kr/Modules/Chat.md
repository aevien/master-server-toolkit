# 마스터 서버 툴킷 - 채팅

## 설명
채널, 비공개 메시지 및 음란 한 단어 검사를 지원하는 채팅 시스템을 작성하기위한 모듈.

## chatModule

채팅 제어를위한 메인 클래스.

### 설정 :
````csharp
[헤더 ( "일반 설정")]
[Serializefield] Protected bool useauthmodule = true;
[Serializefield] Protected bool uscensormodule = true;
[Serializefield] Protected bool allowusernamepicking = true;

[Serializefield] 보호 된 bool setfirstchannnelaslocal = true;
[Serializefield] Protected Bool setlastChannnellaslocal = true;

[Serializefield] 보호 된 Intranchannnemelenger = 5;

TODO: 나머지 문서는 한국어 번역이 필요합니다.
