# Master Server Toolkit - Localization

## Описание
Система локализации для мультиязычной поддержки игры. Поддерживает загрузку переводов из текстовых файлов и динамическую смену языка.

## MstLocalization

Главный класс для работы с локализацией.

### Основные свойства:
```csharp
// Текущий язык
string Lang { get; set; }

// Получение перевода по ключу
string this[string key] { get; }

// Событие смены языка
event Action<string> LanguageChangedEvent;
```

### Доступ к локализации:
```csharp
// Через глобальный экземпляр
Mst.Localization.Lang = "ru";
string welcomeText = Mst.Localization["welcome_message"];

// Создание собственного экземпляра
var localization = new MstLocalization();
```

## Формат файлов локализации

### Структура файла:
```
# Комментарии начинаются с #
;key;en;ru;de

# Сообщения UI
ui_welcome;Welcome!;Добро пожаловать!;Willkommen!
ui_loading;Loading...;Загрузка...;Wird geladen...
ui_error;Error occurred;Произошла ошибка;Fehler aufgetreten

# Игровые сообщения
game_start;Game Started;Игра началась;Spiel gestartet
game_over;Game Over;Игра окончена;Spiel beendet

# Кнопки
btn_ok;OK;ОК;OK
btn_cancel;Cancel;Отмена;Abbrechen
btn_save;Save;Сохранить;Speichern
```

### Расположение файлов:
```
Resources/
└── Localization/
    ├── localization.txt          # Основной файл
    └── custom_localization.txt   # Кастомные переводы
```

## Использование в коде

### Базовое использование:
```csharp
public class UIManager : MonoBehaviour
{
    [Header("UI Elements")]
    public Text titleText;
    public Text statusText;
    public Button okButton;
    
    void Start()
    {
        // Подписка на смену языка
        Mst.Localization.LanguageChangedEvent += OnLanguageChanged;
        
        // Установка начальных текстов
        UpdateTexts();
    }
    
    void OnLanguageChanged(string newLang)
    {
        UpdateTexts();
    }
    
    void UpdateTexts()
    {
        titleText.text = Mst.Localization["game_title"];
        statusText.text = Mst.Localization["status_ready"];
        okButton.GetComponentInChildren<Text>().text = Mst.Localization["btn_ok"];
    }
}
```

### Создание компонента локализации:
```csharp
public class LocalizedText : MonoBehaviour
{
    [Header("Localization")]
    public string key;
    
    private Text textComponent;
    
    void Awake()
    {
        textComponent = GetComponent<Text>();
    }
    
    void Start()
    {
        Mst.Localization.LanguageChangedEvent += UpdateText;
        UpdateText(Mst.Localization.Lang);
    }
    
    void OnDestroy()
    {
        Mst.Localization.LanguageChangedEvent -= UpdateText;
    }
    
    void UpdateText(string lang)
    {
        if (textComponent && !string.IsNullOrEmpty(key))
        {
            textComponent.text = Mst.Localization[key];
        }
    }
}
```

### Смена языка с сохранением:
```csharp
public class LanguageSelector : MonoBehaviour
{
    [Header("Available Languages")]
    public string[] availableLanguages = { "en", "ru", "de" };
    
    public Dropdown languageDropdown;
    
    void Start()
    {
        // Загрузка сохраненного языка
        string savedLang = PlayerPrefs.GetString("SelectedLanguage", "en");
        Mst.Localization.Lang = savedLang;
        
        // Настройка выпадающего списка
        SetupDropdown();
    }
    
    void SetupDropdown()
    {
        languageDropdown.ClearOptions();
        
        var options = new List<Dropdown.OptionData>();
        int selectedIndex = 0;
        
        for (int i = 0; i < availableLanguages.Length; i++)
        {
            string lang = availableLanguages[i];
            options.Add(new Dropdown.OptionData(GetLanguageName(lang)));
            
            if (lang == Mst.Localization.Lang)
                selectedIndex = i;
        }
        
        languageDropdown.AddOptions(options);
        languageDropdown.value = selectedIndex;
        languageDropdown.onValueChanged.AddListener(OnLanguageSelected);
    }
    
    void OnLanguageSelected(int index)
    {
        string newLang = availableLanguages[index];
        
        // Смена языка
        Mst.Localization.Lang = newLang;
        
        // Сохранение выбора
        PlayerPrefs.SetString("SelectedLanguage", newLang);
        PlayerPrefs.Save();
    }
    
    string GetLanguageName(string lang)
    {
        switch (lang)
        {
            case "en": return "English";
            case "ru": return "Русский";
            case "de": return "Deutsch";
            default: return lang.ToUpper();
        }
    }
}
```

## Динамическая локализация

### Локализация с параметрами:
```csharp
// В файле локализации:
# player_score;Score: {0};Счет: {0};Punkte: {0}

// В коде:
string scoreText = string.Format(
    Mst.Localization["player_score"], 
    currentScore
);

// Или с помощью хелпера:
public static string GetLocalizedFormat(string key, params object[] args)
{
    string template = Mst.Localization[key];
    return string.Format(template, args);
}

// Использование:
string message = GetLocalizedFormat("welcome_player", playerName);
```

### Локализация перечислений:
```csharp
public enum GameMode
{
    Single,
    Multiplayer,
    Tournament
}

public static string GetLocalizedEnum<T>(T enumValue) where T : Enum
{
    string key = $"enum_{typeof(T).Name}_{enumValue}";
    return Mst.Localization[key];
}

// В файле локализации:
# enum_GameMode_Single;Single Player;Одиночная игра;Einzelspieler
# enum_GameMode_Multiplayer;Multiplayer;Многопользовательская игра;Mehrspieler
```

## Расширенные возможности

### Кастомный формат файлов:
```csharp
public class CustomLocalizationLoader
{
    public static void LoadJson(string jsonPath)
    {
        var jsonText = Resources.Load<TextAsset>(jsonPath).text;
        var locData = JsonUtility.FromJson<LocalizationData>(jsonText);
        
        foreach (var entry in locData.entries)
        {
            foreach (var translation in entry.translations)
            {
                Mst.Localization.RegisterKey(
                    translation.lang, 
                    entry.key, 
                    translation.value
                );
            }
        }
    }
}

[System.Serializable]
public class LocalizationData
{
    public LocalizationEntry[] entries;
}

[System.Serializable]
public class LocalizationEntry
{
    public string key;
    public Translation[] translations;
}

[System.Serializable]
public class Translation
{
    public string lang;
    public string value;
}
```

### Локализация изображений:
```csharp
public class LocalizedSprite : MonoBehaviour
{
    [Header("Localized Sprites")]
    public Sprite englishSprite;
    public Sprite russianSprite;
    public Sprite germanSprite;
    
    private Image imageComponent;
    
    void Start()
    {
        imageComponent = GetComponent<Image>();
        Mst.Localization.LanguageChangedEvent += UpdateSprite;
        UpdateSprite(Mst.Localization.Lang);
    }
    
    void UpdateSprite(string lang)
    {
        switch (lang)
        {
            case "en": imageComponent.sprite = englishSprite; break;
            case "ru": imageComponent.sprite = russianSprite; break;
            case "de": imageComponent.sprite = germanSprite; break;
        }
    }
}
```

## Лучшие практики

1. **Используйте namespace для ключей**:
```
ui_main_menu_title
ui_settings_volume
game_message_victory
error_network_timeout
```

2. **Храните длинные тексты отдельно**:
```
# tutorial_step1;Press [WASD] to move;Нажмите [WASD] для передвижения;...
```

3. **Валидация переводов при старте**:
```csharp
void ValidateTranslations()
{
    string[] requiredKeys = { "ui_welcome", "btn_ok", "game_start" };
    string[] languages = { "en", "ru", "de" };
    
    foreach (var key in requiredKeys)
    {
        foreach (var lang in languages)
        {
            var oldLang = Mst.Localization.Lang;
            Mst.Localization.Lang = lang;
            
            if (Mst.Localization[key] == key)
            {
                Debug.LogWarning($"Missing translation for {key} in {lang}");
            }
            
            Mst.Localization.Lang = oldLang;
        }
    }
}
```

4. **Аргументы командной строки**:
```bash
# Установка языка при запуске
./Game.exe -defaultLanguage ru
```

## Интеграция с системой событий

```csharp
// Уведомление о смене языка
Mst.Localization.LanguageChangedEvent += (lang) => {
    Mst.Events.Invoke("languageChanged", lang);
};

// Уведомление об ошибках перевода
public static void ReportMissingTranslation(string key, string lang)
{
    Mst.Events.Invoke("translationMissing", new { key, lang });
    Debug.LogWarning($"Missing translation: {key} for {lang}");
}
```
