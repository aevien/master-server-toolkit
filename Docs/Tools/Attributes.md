# Master Server Toolkit - Attributes

## Описание
Инструменты для работы с атрибутами и аннотациями в редакторе Unity, улучшающие пользовательский интерфейс инспектора.

## HelpBox

Класс для создания информационных блоков в инспекторе Unity с поддержкой различных типов сообщений.

### Основные свойства:

```csharp
public string Text { get; set; }     // Текст сообщения
public float Height { get; set; }     // Высота блока
public HelpBoxType Type { get; set; } // Тип блока (Info, Warning, Error)
```

### Конструкторы:

```csharp
// Создание с указанием высоты
public HelpBox(string text, float height, HelpBoxType type = HelpBoxType.Info)

// Создание со стандартной высотой
public HelpBox(string text, HelpBoxType type = HelpBoxType.Info)

// Создание пустого блока
public HelpBox()
```

### Пример использования:

```csharp
// В ScriptableObject или MonoBehaviour
[SerializeField]
private HelpBox infoBox = new HelpBox("Это информационный блок", HelpBoxType.Info);

[SerializeField]
private HelpBox warningBox = new HelpBox("Внимание! Это предупреждение", 60, HelpBoxType.Warning);

[SerializeField]
private HelpBox errorBox = new HelpBox("Ошибка! Это сообщение об ошибке", HelpBoxType.Error);
```

### Интеграция в редакторе:

```csharp
// В пользовательском редакторе
[CustomPropertyDrawer(typeof(HelpBox))]
public class HelpBoxDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var textProp = property.FindPropertyRelative("Text");
        var typeProp = property.FindPropertyRelative("Type");
        var heightProp = property.FindPropertyRelative("Height");
        
        // Рисуем блок помощи в зависимости от типа
        EditorGUI.HelpBox(
            new Rect(position.x, position.y, position.width, heightProp.floatValue), 
            textProp.stringValue, 
            (MessageType)typeProp.enumValueIndex
        );
    }
    
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return property.FindPropertyRelative("Height").floatValue;
    }
}
```

### Типы блоков:

```csharp
public enum HelpBoxType
{
    Info,     // Информационное сообщение
    Warning,  // Предупреждение
    Error     // Ошибка
}
```

## Практическое применение

### Документирование настроек:

```csharp
[Serializable]
public class ConnectionSettings
{
    [SerializeField]
    private HelpBox helpBox = new HelpBox(
        "Укажите IP-адрес и порт сервера. Оставьте поле IP пустым для использования localhost.", 
        HelpBoxType.Info
    );
    
    [SerializeField]
    private string serverIp;
    
    [SerializeField]
    private int serverPort = 5000;
}
```

### Предупреждения в компонентах:

```csharp
[RequireComponent(typeof(NetworkIdentity))]
public class PlayerController : MonoBehaviour
{
    [SerializeField]
    private HelpBox helpBox = new HelpBox(
        "Этот компонент требует NetworkIdentity для корректной работы с сервером", 
        HelpBoxType.Warning
    );
    
    // Реализация компонента
}
```

### Сообщения об ошибках:

```csharp
[Serializable]
public class SecuritySettings
{
    [SerializeField]
    private HelpBox securityNote = new HelpBox(
        "ВНИМАНИЕ! Никогда не сохраняйте чувствительные данные в исходном коде!", 
        80,
        HelpBoxType.Error
    );
    
    [SerializeField]
    private string certificatePath;
    
    [SerializeField]
    private string privateKey;
}
```
