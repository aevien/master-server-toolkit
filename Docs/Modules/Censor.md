# Master Server Toolkit - Censor

## Описание
Модуль цензуры для фильтрации нежелательного контента, такого как нецензурная лексика или оскорбления. Обеспечивает безопасную коммуникацию между игроками.

## CensorModule

Основной класс модуля цензуры.

### Настройка:
```csharp
[Header("Settings")]
[SerializeField] private TextAsset[] wordsLists;
[SerializeField, TextArea(5, 10)] private string matchPattern = @"\b{0}\b";
```

### Инициализация:
```csharp
// Добавление модуля на сцену
var censorModule = gameObject.AddComponent<CensorModule>();

// Настройка списков запрещенных слов
public TextAsset[] wordsLists;
wordsLists = new TextAsset[] { forbiddenWordsAsset };
```

### Формат файла словаря:
Словарь представляет собой текстовый файл с запрещенными словами, разделенными запятыми:
```
bad,words,list,here,separated,by,commas
```

## Использование в коде

### Проверка текста:
```csharp
// Получение экземпляра модуля
var censorModule = Mst.Server.Modules.GetModule<CensorModule>();

// Проверка текста на наличие запрещенных слов
bool hasBadWord = censorModule.HasCensoredWord("Text to check");

if (hasBadWord)
{
    // Текст содержит запрещенные слова
    Debug.Log("Text contains censored words");
}
else
{
    // Текст безопасен
    Debug.Log("Text is clean");
}
```

### Интеграция с чатом:
```csharp
// В обработчике сообщений чата
void HandleChatMessage(string message, IPeer sender)
{
    var censorModule = Mst.Server.Modules.GetModule<CensorModule>();
    
    if (censorModule.HasCensoredWord(message))
    {
        // Отклонить сообщение
        sender.SendMessage(MstOpCodes.MessageRejected, "Message contains forbidden words");
        return;
    }
    
    // Отправить сообщение всем пользователям
    BroadcastMessage(message);
}
```

### Проверка имени пользователя:
```csharp
// При регистрации в AuthModule
protected override bool IsUsernameValid(string username)
{
    if (!base.IsUsernameValid(username))
        return false;
    
    var censorModule = Mst.Server.Modules.GetModule<CensorModule>();
    
    // Проверка имени на запрещенные слова
    if (censorModule.HasCensoredWord(username))
        return false;
    
    return true;
}
```

## Настройка паттерна проверки

Параметр `matchPattern` определяет, как именно будут проверяться запрещенные слова:

```csharp
// По умолчанию: Целые слова (используя границы слов)
matchPattern = @"\b{0}\b";

// Более строгая проверка: включая частичные совпадения
matchPattern = @"{0}";

// С разделителями: проверяет только слова, разделенные пробелами
matchPattern = @"(\s|^){0}(\s|$)";
```

## Расширение модуля

Вы можете расширить базовую функциональность, создав наследника CensorModule:

```csharp
public class EnhancedCensorModule : CensorModule
{
    [SerializeField] private bool useRegexPatterns = false;
    [SerializeField] private TextAsset regexPatterns;
    
    private List<Regex> patterns = new List<Regex>();
    
    protected override void ParseTextFiles()
    {
        base.ParseTextFiles();
        
        // Загрузка дополнительных регулярных выражений
        if (useRegexPatterns && regexPatterns != null)
        {
            var patternLines = regexPatterns.text.Split('\n');
            foreach (var pattern in patternLines)
            {
                if (!string.IsNullOrEmpty(pattern))
                {
                    patterns.Add(new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled));
                }
            }
        }
    }
    
    public override bool HasCensoredWord(string text)
    {
        // Проверка базовым методом
        if (base.HasCensoredWord(text))
            return true;
        
        // Проверка с помощью регулярных выражений
        foreach (var regex in patterns)
        {
            if (regex.IsMatch(text))
                return true;
        }
        
        return false;
    }
    
    // Дополнительный метод для получения замаскированного текста
    public string GetCensoredText(string text, char maskChar = '*')
    {
        string result = text;
        
        // Замена запрещенных слов маской
        foreach (var word in censoredWords)
        {
            string pattern = string.Format(matchPattern, Regex.Escape(word));
            string replacement = new string(maskChar, word.Length);
            result = Regex.Replace(result, pattern, replacement, RegexOptions.IgnoreCase);
        }
        
        return result;
    }
}
```

## Лучшие практики

1. **Регулярно обновляйте словари** запрещенных слов
2. **Используйте границы слов** (`\b{0}\b`) для избежания ложных срабатываний
3. **Интегрируйте с системой чата** для автоматической фильтрации сообщений
4. **Добавляйте преобразование текста** перед проверкой для обхода простых попыток обмана (напр. "b@d w0rd")
5. **Включайте многоязычную поддержку** для интернациональных проектов
6. **Учитывайте контекст** при фильтрации - некоторые слова могут быть нормальными в одном контексте, но нежелательными в другом
7. **Используйте локализованные словари** для разных регионов

## Примеры интеграции

### Система автоматического предупреждения:
```csharp
public class ChatFilterSystem : MonoBehaviour
{
    [SerializeField] private int maxViolations = 3;
    private Dictionary<string, int> violations = new Dictionary<string, int>();
    
    public void CheckMessage(string username, string message)
    {
        var censorModule = Mst.Server.Modules.GetModule<CensorModule>();
        
        if (censorModule.HasCensoredWord(message))
        {
            if (!violations.ContainsKey(username))
                violations[username] = 0;
                
            violations[username]++;
            
            if (violations[username] >= maxViolations)
            {
                // Временный бан пользователя
                BanUser(username, TimeSpan.FromMinutes(10));
            }
        }
    }
}
```

### Интеграция с пользовательским контентом:
```csharp
// Проверка пользовательских названий
public bool ValidateUserContent(string text)
{
    var censorModule = Mst.Server.Modules.GetModule<CensorModule>();
    return !censorModule.HasCensoredWord(text);
}

// Использование при создании предметов, кланов и т.д.
public bool CreateClan(string clanName, string clanTag)
{
    if (!ValidateUserContent(clanName) || !ValidateUserContent(clanTag))
    {
        return false;
    }
    
    // Создание клана
    // ...
    
    return true;
}
```