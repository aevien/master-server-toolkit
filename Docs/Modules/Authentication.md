# Master Server Toolkit - Authentication

## Описание
Модуль аутентификации для регистрации, входа в систему, восстановления паролей и управления пользователями.

## AuthModule

Основной класс модуля аутентификации.

### Настройка:
```csharp
[Header("Settings")]
[SerializeField] private int usernameMinChars = 4;
[SerializeField] private int usernameMaxChars = 12;
[SerializeField] private int userPasswordMinChars = 8;
[SerializeField] private bool emailConfirmRequired = true;

[Header("Guest Settings")]
[SerializeField] private bool allowGuestLogin = true;
[SerializeField] private string guestPrefix = "user_";

[Header("Security")]
[SerializeField] private string tokenSecret = "your-secret-key";
[SerializeField] private int tokenExpiresInDays = 7;
```

### Типы аутентификации:

#### 1. По имени и паролю:
```csharp
// Клиент отправляет
var credentials = new MstProperties();
credentials.Set(MstDictKeys.USER_NAME, "playerName");
credentials.Set(MstDictKeys.USER_PASSWORD, "password123");
credentials.Set(MstDictKeys.USER_DEVICE_ID, "device123");
credentials.Set(MstDictKeys.USER_DEVICE_NAME, "iPhone 12");

// Шифрование и отправка
var encryptedData = Mst.Security.EncryptAES(credentials.ToBytes(), aesKey);
Mst.Client.Connection.SendMessage(MstOpCodes.SignIn, encryptedData);
```

#### 2. По email:
```csharp
// Отправляет пароль на email
var credentials = new MstProperties();
credentials.Set(MstDictKeys.USER_EMAIL, "player@game.com");
Mst.Client.Connection.SendMessage(MstOpCodes.SignIn, credentials);
```

#### 3. По токену:
```csharp
// Автоматический вход
var credentials = new MstProperties();
credentials.Set(MstDictKeys.USER_AUTH_TOKEN, savedToken);
Mst.Client.Connection.SendMessage(MstOpCodes.SignIn, credentials);
```

#### 4. Гостевой вход:
```csharp
var credentials = new MstProperties();
credentials.Set(MstDictKeys.USER_IS_GUEST, true);
credentials.Set(MstDictKeys.USER_DEVICE_ID, "device123");
Mst.Client.Connection.SendMessage(MstOpCodes.SignIn, credentials);
```

## Регистрация

```csharp
// Создание аккаунта
var credentials = new MstProperties();
credentials.Set(MstDictKeys.USER_NAME, "newPlayer");
credentials.Set(MstDictKeys.USER_PASSWORD, "securePassword");
credentials.Set(MstDictKeys.USER_EMAIL, "new@player.com");

// Шифрование
var encrypted = Mst.Security.EncryptAES(credentials.ToBytes(), aesKey);
Mst.Client.Connection.SendMessage(MstOpCodes.SignUp, encrypted);
```

## Управление пользователями

### Работа с залогиненными пользователями:
```csharp
// Получить пользователя
var user = authModule.GetLoggedInUserByUsername("playerName");
var user = authModule.GetLoggedInUserById("userId");
var user = authModule.GetLoggedInUserByEmail("email@game.com");

// Проверить залогинен ли
bool isOnline = authModule.IsUserLoggedInByUsername("playerName");

// Получить всех онлайн
var allOnline = authModule.LoggedInUsers;

// Разлогинить
authModule.SignOut("playerName");
authModule.SignOut(userExtension);
```

### События:
```csharp
// Подписка на события
authModule.OnUserLoggedInEvent += (user) => {
    Debug.Log($"User {user.Username} logged in");
};

authModule.OnUserLoggedOutEvent += (user) => {
    Debug.Log($"User {user.Username} logged out");
};

authModule.OnUserRegisteredEvent += (peer, account) => {
    Debug.Log($"New user registered: {account.Username}");
};
```

## Восстановление пароля

### Запрос кода:
```csharp
// Клиент запрашивает код
Mst.Connection.SendMessage(MstOpCodes.GetPasswordResetCode, "email@game.com");

// Сервер отправляет код на email
```

### Смена пароля:
```csharp
var data = new MstProperties();
data.Set(MstDictKeys.RESET_PASSWORD_EMAIL, "email@game.com");
data.Set(MstDictKeys.RESET_PASSWORD_CODE, "123456");
data.Set(MstDictKeys.RESET_PASSWORD, "newPassword");

Mst.Connection.SendMessage(MstOpCodes.ChangePassword, data);
```

## Подтверждение email

```csharp
// Запрос кода подтверждения
Mst.Connection.SendMessage(MstOpCodes.GetEmailConfirmationCode);

// Подтверждение email
Mst.Connection.SendMessage(MstOpCodes.ConfirmEmail, "confirmationCode");
```

## Дополнительные свойства

```csharp
// Привязка дополнительных данных
var extraProps = new MstProperties();
extraProps.Set("playerLevel", 10);
extraProps.Set("gameClass", "warrior");

Mst.Connection.SendMessage(MstOpCodes.BindExtraProperties, extraProps);
```

## Настройка базы данных

```csharp
// Реализация IAccountsDatabaseAccessor
public class MyDatabaseAccessor : IAccountsDatabaseAccessor
{
    public async Task<IAccountInfoData> GetAccountByUsernameAsync(string username)
    {
        // Получение аккаунта из БД
    }
    
    public async Task InsertAccountAsync(IAccountInfoData account)
    {
        // Сохранение нового аккаунта
    }
    
    public async Task UpdateAccountAsync(IAccountInfoData account)
    {
        // Обновление аккаунта
    }
}

// Создание фабрики
public class DatabaseFactory : DatabaseAccessorFactory
{
    public override void CreateAccessors()
    {
        var accessor = new MyDatabaseAccessor();
        Mst.Server.DbAccessors.AddAccessor(accessor);
    }
}
```

## Проверка прав доступа

```csharp
// Получение информации о пире
Mst.Connection.SendMessage(MstOpCodes.GetAccountInfoByPeer, peerId);
Mst.Connection.SendMessage(MstOpCodes.GetAccountInfoByUsername, "playerName");

// Требует минимальный уровень прав
authModule.getPeerDataPermissionsLevel = 10;
```

## Кастомизация валидации

```csharp
// Переопределение в наследнике AuthModule
protected override bool IsUsernameValid(string username)
{
    if (!base.IsUsernameValid(username))
        return false;
    
    // Дополнительные проверки
    if (username.Contains("admin"))
        return false;
    
    return true;
}

protected override bool IsPasswordValid(string password)
{
    if (!base.IsPasswordValid(password))
        return false;
    
    // Проверка на сложность
    bool hasUpper = password.Any(char.IsUpper);
    bool hasNumber = password.Any(char.IsDigit);
    
    return hasUpper && hasNumber;
}
```

## Интеграция с email

```csharp
// Настройка mailer компонента
[SerializeField] protected Mailer mailer;

// Настройка Smtp (в SmtpMailer)
smtpHost = "smtp.gmail.com";
smtpPort = 587;
smtpUsername = "yourgame@gmail.com";
smtpPassword = "app-password";
mailFrom = "noreply@yourgame.com";
```

## Аргументы командной строки

```bash
# Токен настройки
./Server.exe -tokenSecret mysecret -tokenExpires 30

# Настройка issuer/audience
./Server.exe -tokenIssuer http://mygame.com -tokenAudience http://mygame.com/users
```

## Лучшие практики

1. **Всегда шифруйте пароли перед отправкой**
2. **Используйте токены для автовхода**
3. **Храните конфиденциальные данные в базе**
4. **Настройте email для восстановления паролей**
5. **Проверяйте имена на цензуру с помощью CensorModule**
6. **Ограничивайте количество попыток входа**
7. **Используйте HTTPS для production**
