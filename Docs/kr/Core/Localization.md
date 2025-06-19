# Master Server Toolkit - Localization

## 설명
게임의 다국어 지원을 위한 로컬라이제이션 시스템입니다. 텍스트 파일에서 번역을 로드하고 실행 중 언어를 변경할 수 있습니다.

## MstLocalization

로컬라이제이션을 관리하는 주요 클래스입니다.

### 주요 속성:
```csharp
// 현재 언어
string Lang { get; set; }

// 키로 번역 얻기
string this[string key] { get; }

// 언어 변경 이벤트
event Action<string> LanguageChangedEvent;
```

### 로컬라이제이션 접근:
```csharp
// 전역 인스턴스를 통해
Mst.Localization.Lang = "ru";
string welcomeText = Mst.Localization["welcome_message"];

// 직접 인스턴스 생성
var localization = new MstLocalization();
```

## 로컬라이제이션 파일 포맷

### 파일 구조:
```
# 주석은 # 으로 시작
;key;en;ru;de

# UI 메시지
ui_welcome;Welcome!;Добро пожаловать!;Willkommen!
ui_loading;Loading...;Загрузка...;Wird geladen...
ui_error;Error occurred;Произошла ошибка;Fehler aufgetreten

# 게임 메시지
game_start;Game Started;Игра началась;Spiel gestartet
game_over;Game Over;Игра окончена;Spiel beendet

# 버튼
btn_ok;OK;ОК;OK
btn_cancel;Cancel;Отмена;Abbrechen
btn_save;Save;Сохранить;Speichern
```

### 파일 위치:
```
Resources/
└── Localization/
    ├── localization.txt          # 기본 파일
    └── custom_localization.txt   # 사용자 정의 번역
```

## 코드에서 사용하기

### 기본 사용
```csharp
public class UIManager : MonoBehaviour
{
    [Header("UI Elements")]
    public Text titleText;
    public Text statusText;
    public Button okButton;

    void Start()
    {
        // 언어 변경 이벤트 구독
        Mst.Localization.LanguageChangedEvent += OnLanguageChanged;

        // 초기 텍스트 설정
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

### 로컬라이즈드 컴포넌트 작성
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

### 언어 선택 후 저장
```csharp
public class LanguageSelector : MonoBehaviour
{
    [Header("Available Languages")]
    public string[] availableLanguages = { "en", "ru", "de" };

    public Dropdown languageDropdown;

    void Start()
    {
        // 저장된 언어 로드
        string savedLang = PlayerPrefs.GetString("SelectedLanguage", "en");
        Mst.Localization.Lang = savedLang;

        // 드롭다운 설정
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

        // 언어 변경
        Mst.Localization.Lang = newLang;

        // 선택 저장
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

## 동적 로컬라이제이션

### 매개변수를 포함한 로컬라이즈
```csharp
// 로컬라이제이션 파일:
# player_score;Score: {0};Счет: {0};Punkte: {0}

// 코드에서:
string scoreText = string.Format(
    Mst.Localization["player_score"],
    currentScore
);

// 헬퍼 사용
public static string GetLocalizedFormat(string key, params object[] args)
{
    string template = Mst.Localization[key];
    return string.Format(template, args);
}

// 사용 예:
string message = GetLocalizedFormat("welcome_player", playerName);
```

### 열거형 로컬라이즈
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

// 로컬라이제이션 파일:
# enum_GameMode_Single;Single Player;Одиночная игра;Einzelspieler
# enum_GameMode_Multiplayer;Multiplayer;Многопользовательская игра;Mehrspieler
```

## 고급 기능

### 사용자 정의 파일 형식
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

### 이미지 로컬라이징
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

## 모범 사례
1. **키에 네임스페이스를 사용하세요**:
```
ui_main_menu_title
ui_settings_volume
game_message_victory
error_network_timeout
```

2. **긴 텍스트는 별도 파일로 보관**:
```
# tutorial_step1;Press [WASD] to move;Нажмите [WASD] для передвижения;...
```

3. **시작 시 번역 검증**:
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

4. **명령줄 인수**:
```bash
# 실행 시 기본 언어 설정
./Game.exe -defaultLanguage ru
```

## 이벤트 시스템과 통합
```csharp
// 언어 변경 알림
Mst.Localization.LanguageChangedEvent += (lang) => {
    Mst.Events.Invoke("languageChanged", lang);
};

// 번역 누락 알림
public static void ReportMissingTranslation(string key, string lang)
{
    Mst.Events.Invoke("translationMissing", new { key, lang });
    Debug.LogWarning($"Missing translation: {key} for {lang}");
}
```
