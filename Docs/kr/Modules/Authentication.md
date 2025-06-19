# 마스터 서버 툴킷 - 인증

## 설명
등록, 시스템 입력, 암호 복원 및 사용자 관리를위한 인증 모듈.

## AuthModule

인증 모듈의 주요 클래스.

### 설정 :
````csharp
[헤더 ( "설정")]]]
[Serializefield] Private int usernameminchars = 4;
[Serializefield] usernamemaxchars의 개인 개인 = 12;
[Serializefield] 개인 int userPasswordMinchars = 8;
[Serializefield] Private bool emailsonFirmRequired = true;

[헤더 ( "게스트 설정")]
[Serializefield] private bool allowguestlogin = true;
[Serializefield] 개인 문자열 guestprefix = "user_";

TODO: 나머지 문서는 한국어 번역이 필요합니다.
