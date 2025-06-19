# Master Server Toolkit - 메일 시스템

## 설명
SMTP 프로토콜을 사용해 HTML 템플릿 기반의 이메일을 전송하는 시스템입니다.

## SmtpMailer
SMTP를 통해 메일을 보내는 주요 클래스입니다.

### 설정 예시
```csharp
[Header("E-mail settings")]
public string smtpHost = "smtp.gmail.com";
public string smtpUsername = "your-email@gmail.com";
public string smtpPassword = "your-app-password";
public int smtpPort = 587;
public bool enableSsl = true;
public string mailFrom = "noreply@yourgame.com";
public string senderDisplayName = "Your Game Name";

[Header("E-mail template")]
[SerializeField] protected TextAsset emailBodyTemplate;
```

### 사용 예
```csharp
var mailer = GetComponent<SmtpMailer>();

// 간단한 메일 보내기
await mailer.SendMailAsync("player@example.com", "Welcome!", "Thank you!");

// HTML 템플릿 사용
await mailer.SendMailAsync(email, "Code: " + code, emailBody);
```

## 메일 템플릿
### HTML 예시
```html
<h1>#{MESSAGE_SUBJECT}</h1>
<div>#{MESSAGE_BODY}</div>
<footer>© #{MESSAGE_YEAR} Game Name</footer>
```

### 치환 토큰
- `#{MESSAGE_SUBJECT}` - 제목
- `#{MESSAGE_BODY}` - 본문
- `#{MESSAGE_YEAR}` - 현재 연도

## SMTP 설정 예
### Gmail
```csharp
smtpHost = "smtp.gmail.com";
smtpPort = 587;
enableSsl = true;
// App Password 사용
```

### SendGrid
```csharp
smtpHost = "smtp.sendgrid.net";
smtpPort = 587;
smtpUsername = "apikey";
smtpPassword = "your-sendgrid-api-key";
```

## 사용 예제
### 이메일 확인 코드 발송
```csharp
public async Task<bool> SendConfirmationCode(string email, string code)
{
    string subject = "Confirm your email";
    string body = $"Your code: <strong>{code}</strong>";

    return await mailer.SendMailAsync(email, subject, body);
}
```

### 비밀번호 초기화
```csharp
public async Task<bool> SendPasswordReset(string email, string resetLink)
{
    string subject = "Password Reset";
    string body = $"<a href='{resetLink}'>Reset Password</a>";

    return await mailer.SendMailAsync(email, subject, body);
}
```

## 인수로 설정하기
```bash
# 실행 시 설정 예
./Server.exe -smtpHost smtp.gmail.com -smtpUsername game@gmail.com -smtpPassword app-password
```

### 인수 읽기
```csharp
smtpHost = Mst.Args.AsString(Mst.Args.Names.SmtpHost, smtpHost);
smtpUsername = Mst.Args.AsString(Mst.Args.Names.SmtpUsername, smtpUsername);
smtpPassword = Mst.Args.AsString(Mst.Args.Names.SmtpPassword, smtpPassword);
```

## 중요 사항
- WebGL에서는 동작하지 않음
- 오류는 비동기로 로깅됨
- Gmail 사용 시 App Password 권장
- 방화벽 설정을 확인
- HTML 템플릿은 Resources에서 로드

## 모범 사례
1. SMTP 비밀번호를 안전하게 보관
2. 모든 호출은 비동기로 처리
3. 이메일 형식 검증 필요
4. 발송 제한을 두어 스팸을 방지
5. 여러 메일 제공업체로 테스트
