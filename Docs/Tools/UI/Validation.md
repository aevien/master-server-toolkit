# Master Server Toolkit - Validation System

## Обзор

Система валидации в Master Server Toolkit предоставляет набор инструментов для проверки пользовательского ввода в формах. Она поддерживает проверку обязательных полей, валидацию по регулярным выражениям, сравнение значений полей и многое другое.

## Ключевые компоненты

### IValidatableComponent

Базовый интерфейс для всех компонентов валидации:

```csharp
public interface IValidatableComponent
{
    bool IsValid();
}
```

### ValidatableBaseComponent

Абстрактный базовый класс для компонентов валидации:

```csharp
public abstract class ValidatableBaseComponent : MonoBehaviour, IValidatableComponent
{
    [Header("Base Settings")]
    [SerializeField] protected bool isRequired = false;
    [SerializeField, TextArea(2, 10)] protected string requiredErrorMessage;
    [SerializeField] protected Color invalidColor = Color.red;
    [SerializeField] protected Color normalColor = Color.white;
    
    public abstract bool IsValid();
    
    protected void SetInvalidColor();
    protected void SetNormalColor();
}
```

### ValidatableInputFieldComponent

Компонент валидации для текстовых полей ввода:

```csharp
public class ValidatableInputFieldComponent : ValidatableBaseComponent
{
    [Header("Text Field Components")]
    [SerializeField] private TMP_InputField currentInputField;
    [SerializeField] private TMP_InputField compareToInputField;
    [SerializeField, TextArea(2, 10)] protected string compareErrorMessage;
    
    [Header("Text Field RegExp Validation")]
    [SerializeField, TextArea(2, 10)] protected string regExpPattern;
    [SerializeField, TextArea(2, 10)] protected string regExpErrorMessage;
    
    // Проверяет обязательность поля, соответствие регулярному выражению и
    // равенство значения с compareToInputField (если указан)
    public override bool IsValid();
}
```

### ValidatableDropdownComponent

Компонент валидации для выпадающих списков:

```csharp
public class ValidatableDropdownComponent : ValidatableBaseComponent
{
    [Header("Dropdown Components")]
    [SerializeField] private TMP_Dropdown currentDropdown;
    
    [Header("Dropdown Settings")]
    [SerializeField] protected List<int> invalidValues = new List<int>();
    [SerializeField, TextArea(2, 10)] protected string invalidValueErrorMessage;
    
    // Проверяет, что выбранное значение не находится в списке invalidValues
    public override bool IsValid();
}
```

### ValidationFormComponent

Компонент для валидации всей формы:

```csharp
public class ValidationFormComponent : MonoBehaviour, IUIViewComponent
{
    [Header("Settings")]
    [SerializeField] protected bool validateOnStart = false;
    [SerializeField] protected bool validateOnEnable = false;
    [SerializeField] protected bool validateBeforeSubmit = true;
    
    [Header("Components")]
    [SerializeField] protected Button submitButton;
    
    // События
    public UnityEvent OnFormValidEvent;
    public UnityEvent OnFormInvalidEvent;
    public UnityEvent OnSubmitEvent;
    
    // Публичные методы
    public bool Validate();
    public void Submit();
}
```

## Регулярные выражения для валидации

### Электронная почта

```
^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$
```

### Пароль (минимум 8 символов, хотя бы одна буква и одна цифра)

```
^(?=.*[A-Za-z])(?=.*\d)[A-Za-z\d]{8,}$
```

### Имя пользователя (только буквы и цифры, 3-16 символов)

```
^[a-zA-Z0-9]{3,16}$
```

### URL

```
^(https?:\/\/)?([\da-z\.-]+)\.([a-z\.]{2,6})([\/\w \.-]*)*\/?$
```

### Телефонный номер (международный формат)

```
^\+?[1-9]\d{1,14}$
```

## Примеры использования

### Форма регистрации

```csharp
public class RegistrationForm : UIView
{
    [SerializeField] private ValidationFormComponent validationForm;
    
    // Поля ввода с компонентами валидации
    [SerializeField] private TMP_InputField usernameField;
    [SerializeField] private ValidatableInputFieldComponent usernameValidator;
    
    [SerializeField] private TMP_InputField emailField;
    [SerializeField] private ValidatableInputFieldComponent emailValidator;
    
    [SerializeField] private TMP_InputField passwordField;
    [SerializeField] private ValidatableInputFieldComponent passwordValidator;
    
    [SerializeField] private TMP_InputField confirmPasswordField;
    [SerializeField] private ValidatableInputFieldComponent confirmPasswordValidator;
    
    protected override void Awake()
    {
        base.Awake();
        
        // Настройка валидаторов
        usernameValidator.isRequired = true;
        usernameValidator.requiredErrorMessage = "Имя пользователя обязательно";
        usernameValidator.regExpPattern = "^[a-zA-Z0-9]{3,16}$";
        usernameValidator.regExpErrorMessage = "Имя должно содержать от 3 до 16 символов (только буквы и цифры)";
        
        emailValidator.isRequired = true;
        emailValidator.requiredErrorMessage = "Email обязателен";
        emailValidator.regExpPattern = "^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}$";
        emailValidator.regExpErrorMessage = "Неверный формат email";
        
        passwordValidator.isRequired = true;
        passwordValidator.requiredErrorMessage = "Пароль обязателен";
        passwordValidator.regExpPattern = "^(?=.*[A-Za-z])(?=.*\\d)[A-Za-z\\d]{8,}$";
        passwordValidator.regExpErrorMessage = "Пароль должен содержать минимум 8 символов, одну букву и одну цифру";
        
        confirmPasswordValidator.isRequired = true;
        confirmPasswordValidator.requiredErrorMessage = "Подтверждение пароля обязательно";
        confirmPasswordValidator.compareToInputField = passwordField;
        confirmPasswordValidator.compareErrorMessage = "Пароли не совпадают";
        
        // Подписка на события формы
        validationForm.OnFormValidEvent.AddListener(OnFormValid);
        validationForm.OnFormInvalidEvent.AddListener(OnFormInvalid);
        validationForm.OnSubmitEvent.AddListener(OnSubmit);
    }
    
    private void OnFormValid()
    {
        // Форма прошла валидацию
        Debug.Log("Форма валидна");
    }
    
    private void OnFormInvalid()
    {
        // Форма не прошла валидацию
        Debug.Log("Форма невалидна");
    }
    
    private void OnSubmit()
    {
        // Отправка данных формы
        string username = usernameField.text;
        string email = emailField.text;
        string password = passwordField.text;
        
        // Регистрация пользователя
        RegisterUser(username, email, password);
    }
    
    private void RegisterUser(string username, string email, string password)
    {
        // Логика регистрации пользователя
        // ...
    }
}
```

### Валидация в форме входа

```csharp
public class LoginForm : UIView
{
    [SerializeField] private ValidationFormComponent validationForm;
    [SerializeField] private TMP_InputField usernameField;
    [SerializeField] private ValidatableInputFieldComponent usernameValidator;
    [SerializeField] private TMP_InputField passwordField;
    [SerializeField] private ValidatableInputFieldComponent passwordValidator;
    [SerializeField] private Button loginButton;
    
    private void Start()
    {
        // Настройка валидации
        usernameValidator.isRequired = true;
        passwordValidator.isRequired = true;
        
        // Активация валидации при нажатии кнопки входа
        loginButton.onClick.AddListener(() => {
            if (validationForm.Validate())
            {
                // Если валидация успешна, выполняем вход
                Mst.Auth.SignInWithCredentials(usernameField.text, passwordField.text, (isSuccess, error) => {
                    if (isSuccess)
                    {
                        Hide();
                        ViewsManager.Show("MainMenuView");
                    }
                    else
                    {
                        Debug.LogError($"Ошибка входа: {error}");
                    }
                });
            }
        });
    }
}
```

### Кастомный валидатор

```csharp
// Пример создания кастомного валидатора для проверки возраста
public class AgeValidatorComponent : ValidatableBaseComponent
{
    [SerializeField] private TMP_InputField ageField;
    [SerializeField] private int minAge = 18;
    [SerializeField] private int maxAge = 100;
    [SerializeField] private string invalidAgeMessage = "Возраст должен быть от {0} до {1} лет";
    
    public override bool IsValid()
    {
        if (!ageField.interactable)
            return true;
            
        if (isRequired && string.IsNullOrEmpty(ageField.text))
        {
            Debug.LogError(requiredErrorMessage);
            SetInvalidColor();
            return false;
        }
        
        if (!int.TryParse(ageField.text, out int age))
        {
            Debug.LogError("Возраст должен быть числом");
            SetInvalidColor();
            return false;
        }
        
        if (age < minAge || age > maxAge)
        {
            Debug.LogError(string.Format(invalidAgeMessage, minAge, maxAge));
            SetInvalidColor();
            return false;
        }
        
        SetNormalColor();
        return true;
    }
}
```

## Лучшие практики

1. **Сообщения об ошибках**: Используйте ясные и понятные сообщения об ошибках, указывающие, как исправить проблему
2. **Визуальная обратная связь**: Сочетайте текстовые сообщения с визуальными индикаторами (цвет, иконки)
3. **Валидация в реальном времени**: Рассмотрите возможность валидации полей при изменении значения
4. **Предотвращение отправки**: Блокируйте кнопку отправки, если форма невалидна
5. **Кастомные валидаторы**: Создавайте специализированные валидаторы для особых случаев
6. **Группирование ошибок**: Собирайте и показывайте все ошибки сразу, а не по одной

Система валидации в Master Server Toolkit позволяет гибко настраивать проверку форм, обеспечивая хороший пользовательский опыт и предотвращая отправку некорректных данных на сервер.