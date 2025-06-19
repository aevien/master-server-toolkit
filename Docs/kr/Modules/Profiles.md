# 마스터 서버 툴킷 - 프로파일

## 설명
관리 데이터, 변경 관찰 및 고객과 서버 간의 동기화에 대한 프로파일 모듈.

## profilesModule

사용자 프로필 관리를위한 메인 클래스.

### 설정 :
````csharp
[헤더 ( "일반 설정")]
[Serializefield] 보호 된 int unloadprofilect = 20;
[Serializefield] 보호 된 int saveprofinedebouncetime = 1;
[Serializefield] 보호 된 int ClientUpDateBouncetime = 1;
[Serializefield] 보호 된 intletprofilepermissionlevel = 0;
[Serializefield] 보호 된 int maxupdatesize = 1048576;

[헤더 ( "타임 아웃 설정")]
[Serializefield] ProfiloadTimeOutseconds = 10에서 보호됩니다.

TODO: 나머지 문서는 한국어 번역이 필요합니다.
