# 마스터 서버 툴킷 - 웹 서버

## 설명
Webserver 모듈은 내장 된 HTTP 서버를 제공하여 웹 인터페이스, API 및 모니터링 시스템을 통해 게임 서버를 제어합니다.외부 서비스 및 관리자 패널 용 RESTFUL API를 만들 수 있습니다.

## WebServerModule

웹 서버 모듈의 주요 클래스.

### 설정 :
````csharp
[헤더 ( "HTTP 서버 설정")]]
[Serializefield] Protected bool autostart = true;
[Serializefield] 보호 된 문자열 httpaddress = "127.0.0.1";
[Serializefield] 보호 된 intport = 5056;
[Serializefield] Protected String [] defaultIndexpage = new String [] { "index", "home"};

[헤더 ( "사용자 자격 증명 설정")]]]]
[Serializefield] Protected bool usecredentials = true;
[Serializefield] Protected String username = "admin";

TODO: 나머지 문서는 한국어 번역이 필요합니다.
