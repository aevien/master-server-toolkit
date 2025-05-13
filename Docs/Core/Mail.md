# Master Server Toolkit - Mail

## Описание
Система отправки электронной почты через SMTP протокол с поддержкой HTML шаблонов.

## SmtpMailer

Основной класс для отправки почты через SMTP.

### Настройка:
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

### Пример использования:
```csharp
var mailer = GetComponent<SmtpMailer>();

// Отправка простого письма
await mailer.SendMailAsync("player@example.com", "Welcome!", "Thank you!");

// С HTML шаблоном
await mailer.SendMailAsync(email, "Code: " + code, emailBody);
```

## Шаблоны писем

### HTML шаблон:
```html
<h1>#{MESSAGE_SUBJECT}</h1>
<div>#{MESSAGE_BODY}</div>
<footer>© #{MESSAGE_YEAR} Game Name</footer>
```

### Токены замены:
- `#{MESSAGE_SUBJECT}` - Заголовок
- `#{MESSAGE_BODY}` - Тело письма
- `#{MESSAGE_YEAR}` - Текущий год

## Настройка SMTP провайдеров

### Gmail:
```csharp
smtpHost = "smtp.gmail.com";
smtpPort = 587;
enableSsl = true;
// Используйте App Password
```

### SendGrid:
```csharp
smtpHost = "smtp.sendgrid.net";
smtpPort = 587;
smtpUsername = "apikey";
smtpPassword = "your-sendgrid-api-key";
```

## Интеграция примеры

### Подтверждение email:
```csharp
public async Task<bool> SendConfirmationCode(string email, string code)
{
    string subject = "Confirm your email";
    string body = $"Your code: <strong>{code}</strong>";
    
    return await mailer.SendMailAsync(email, subject, body);
}
```

### Сброс пароля:
```csharp
public async Task<bool> SendPasswordReset(string email, string resetLink)
{
    string subject = "Password Reset";
    string body = $"<a href='{resetLink}'>Reset Password</a>";
    
    return await mailer.SendMailAsync(email, subject, body);
}
```

## Настройка через аргументы

```bash
# При запуске
./Server.exe -smtpHost smtp.gmail.com -smtpUsername game@gmail.com -smtpPassword app-password
```

## Аргументы проверяются автоматически:
```csharp
smtpHost = Mst.Args.AsString(Mst.Args.Names.SmtpHost, smtpHost);
smtpUsername = Mst.Args.AsString(Mst.Args.Names.SmtpUsername, smtpUsername);
smtpPassword = Mst.Args.AsString(Mst.Args.Names.SmtpPassword, smtpPassword);
```

## Важные замечания
- Не работает в WebGL
- Ошибки логируются асинхронно
- Используйте App Password для Gmail
- Проверяйте настройки брандмауэра
- HTML шаблоны загружаются из Resources

## Лучшие практики
1. Храните SMTP пароли в безопасном месте
2. Используйте асинхронные вызовы
3. Проверяйте email на валидность
4. Внедряйте ограничения на отправку
5. Тестируйте с разными провайдерами
