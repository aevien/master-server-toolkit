# Master Server Toolkit - WebGL

## Описание
Модуль для улучшения поддержки WebGL-платформы, содержащий вспомогательные компоненты и утилиты для работы с веб-специфичными особенностями и ограничениями Unity WebGL.

## WebGlTextMeshProInput

Компонент для улучшения работы с текстовыми полями ввода TextMeshPro в WebGL-сборках. Решает проблемы с виртуальными клавиатурами на мобильных устройствах и особенностями ввода в веб-среде.

### Основные возможности

```csharp
[RequireComponent(typeof(TMP_InputField))]
public class WebGlTextMeshProInput : MonoBehaviour, IPointerClickHandler
{
    [Header("Settings"), SerializeField]
    private string title = "Input Field"; // Заголовок модального окна ввода

    // Обработка нажатия на поле ввода
    public void OnPointerClick(PointerEventData eventData)
    {
        // Вызов нативного JavaScript-окна ввода
    }

    // Обработка подтверждения ввода
    public void OnPromptOk(string message)
    {
        GetComponent<TMP_InputField>().text = message;
    }

    // Обработка отмены ввода
    public void OnPromptCancel()
    {
        GetComponent<TMP_InputField>().text = "";
    }
}
```

### Использование

```csharp
// Добавление к существующему полю ввода
TMP_InputField inputField = GetComponent<TMP_InputField>();
inputField.gameObject.AddComponent<WebGlTextMeshProInput>();

// Настройка через редактор Unity
// 1. Добавьте компонент WebGlTextMeshProInput к объекту с TMP_InputField
// 2. Настройте заголовок для модального окна ввода
```

### JavaScript интеграция

WebGlTextMeshProInput использует jslib-плагин для вызова нативного JavaScript-кода:

```javascript
// MstWebGL.jslib
mergeInto(LibraryManager.library, {
  MstPrompt: function(name, title, defaultValue) {
    var result = window.prompt(UTF8ToString(title), UTF8ToString(defaultValue));
    
    if (result !== null) {
      var gameObject = UTF8ToString(name);
      SendMessage(gameObject, "OnPromptOk", result);
    } else {
      var gameObject = UTF8ToString(name);
      SendMessage(gameObject, "OnPromptCancel");
    }
  }
});
```

## Мобильная поддержка

Компонент особенно полезен при использовании WebGL на мобильных устройствах:

1. **Решает проблему с виртуальными клавиатурами** на iOS и Android
2. **Обеспечивает корректный ввод** на устройствах с различными форматами экранов
3. **Поддерживает многострочные поля ввода** через нативный интерфейс

### Пример для многострочного ввода

```csharp
public class WebGlMultilineInput : MonoBehaviour
{
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private Button editButton;
    [SerializeField] private string dialogTitle = "Введите текст";
    
    private void Start()
    {
        // Добавляем обработчик кнопки редактирования
        editButton.onClick.AddListener(OnEditButtonClick);
    }
    
    private void OnEditButtonClick()
    {
        // Вызываем модальное окно ввода
        WebGLInput.ShowPrompt(dialogTitle, inputField.text, OnPromptComplete);
    }
    
    private void OnPromptComplete(string result, bool isCancelled)
    {
        if (!isCancelled)
        {
            inputField.text = result;
            // Дополнительная обработка введенного текста
        }
    }
}
```

## Интеграция с другими веб-функциями

### Взаимодействие с буфером обмена

```csharp
public class WebGlClipboard : MonoBehaviour
{
    // Копирование текста в буфер обмена
    public void CopyToClipboard(string text)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WebGLCopyToClipboard(text);
#else
        GUIUtility.systemCopyBuffer = text;
#endif
    }
    
    // Вставка текста из буфера обмена
    public void PasteFromClipboard(TMP_InputField inputField)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WebGLRequestClipboardContent(gameObject.name);
#else
        inputField.text = GUIUtility.systemCopyBuffer;
#endif
    }
    
    // Обработчик для получения содержимого буфера обмена
    public void OnClipboardContent(string content)
    {
        // Использование полученного содержимого
        FindObjectOfType<TMP_InputField>().text = content;
    }
    
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void WebGLCopyToClipboard(string text);
    
    [DllImport("__Internal")]
    private static extern void WebGLRequestClipboardContent(string gameObjectName);
#endif
}
```

### Адаптация к ориентации экрана

```csharp
public class WebGlOrientationHandler : MonoBehaviour
{
    [SerializeField] private CanvasScaler canvasScaler;
    [SerializeField] private float portraitMatchWidthOrHeight = 0.5f;
    [SerializeField] private float landscapeMatchWidthOrHeight = 0;
    
    private void Start()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WebGLAddOrientationChangeListener(gameObject.name);
        UpdateCanvasScaling();
#endif
    }
    
    // Вызывается из JavaScript при изменении ориентации
    public void OnOrientationChanged()
    {
        UpdateCanvasScaling();
    }
    
    private void UpdateCanvasScaling()
    {
        bool isPortrait = Screen.height > Screen.width;
        canvasScaler.matchWidthOrHeight = isPortrait ? portraitMatchWidthOrHeight : landscapeMatchWidthOrHeight;
    }
    
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void WebGLAddOrientationChangeListener(string gameObjectName);
#endif
}
```

## Обработка локализации

Компонент WebGlTextMeshProInput также интегрируется с системой локализации Master Server Toolkit для корректного отображения заголовков диалогов:

```csharp
// Использование локализации для заголовка
private string title = "Input Field";

public void OnPointerClick(PointerEventData eventData)
{
#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
    var input = GetComponent<TMP_InputField>();
    MstPrompt(name, Mst.Localization[title], input.text);
#endif
}
```

## Практические примеры

### Форма регистрации в WebGL

```csharp
public class WebGlRegistrationForm : MonoBehaviour
{
    [SerializeField] private TMP_InputField usernameField;
    [SerializeField] private TMP_InputField emailField;
    [SerializeField] private TMP_InputField passwordField;
    [SerializeField] private Button submitButton;
    
    private void Start()
    {
        // Добавляем компоненты для улучшения ввода в WebGL
        usernameField.gameObject.AddComponent<WebGlTextMeshProInput>().title = "Enter Username";
        emailField.gameObject.AddComponent<WebGlTextMeshProInput>().title = "Enter Email";
        passwordField.gameObject.AddComponent<WebGlTextMeshProInput>().title = "Enter Password";
        
        // Настраиваем кнопку отправки
        submitButton.onClick.AddListener(OnSubmitButtonClick);
    }
    
    private void OnSubmitButtonClick()
    {
        // Проверка ввода и отправка формы
        if (string.IsNullOrEmpty(usernameField.text) || 
            string.IsNullOrEmpty(emailField.text) || 
            string.IsNullOrEmpty(passwordField.text))
        {
            ShowError("All fields are required");
            return;
        }
        
        // Отправка данных на сервер
        SendRegistrationData(usernameField.text, emailField.text, passwordField.text);
    }
    
    private void SendRegistrationData(string username, string email, string password)
    {
        // Логика отправки данных
    }
    
    private void ShowError(string message)
    {
        // Отображение ошибки
    }
}
```

### Сохранение и загрузка данных

```csharp
public class WebGlStorageHandler : MonoBehaviour
{
    // Сохранение данных в LocalStorage
    public void SaveData(string key, string value)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WebGLSaveToLocalStorage(key, value);
#else
        PlayerPrefs.SetString(key, value);
        PlayerPrefs.Save();
#endif
    }
    
    // Загрузка данных из LocalStorage
    public string LoadData(string key, string defaultValue = "")
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return WebGLLoadFromLocalStorage(key, defaultValue);
#else
        return PlayerPrefs.GetString(key, defaultValue);
#endif
    }
    
    // Удаление данных
    public void DeleteData(string key)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WebGLRemoveFromLocalStorage(key);
#else
        PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
#endif
    }
    
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void WebGLSaveToLocalStorage(string key, string value);
    
    [DllImport("__Internal")]
    private static extern string WebGLLoadFromLocalStorage(string key, string defaultValue);
    
    [DllImport("__Internal")]
    private static extern void WebGLRemoveFromLocalStorage(string key);
#endif
}
```

## Лучшие практики

1. **Используйте условную компиляцию** для платформо-зависимого кода
```csharp
#if UNITY_WEBGL && !UNITY_EDITOR
    // WebGL-специфичный код
#else
    // Код для других платформ
#endif
```

2. **Тестируйте на реальных мобильных устройствах** для проверки работы виртуальных клавиатур

3. **Учитывайте ограничения WebGL**:
   - Отсутствие многопоточности
   - Ограничения безопасности браузеров
   - Проблемы с вводом на мобильных устройствах

4. **Предоставляйте альтернативные методы ввода** для сложных форм

5. **Интегрируйте с JavaScript API браузеров** для расширения функциональности:
   - LocalStorage для хранения данных
   - Clipboard API для работы с буфером обмена
   - Screen API для работы с ориентацией экрана

6. **Обрабатывайте потерю фокуса окна браузера** для корректного функционирования приложения
