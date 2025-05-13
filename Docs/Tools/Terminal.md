# Master Server Toolkit - Terminal

## Описание
Встроенная консоль для Unity, позволяющая выполнять команды, просматривать логи и взаимодействовать с игрой в режиме выполнения. Терминал подключается к логам Unity и предоставляет интерфейс для регистрации и выполнения пользовательских команд.

## Основные компоненты

### Terminal
Основной класс, управляющий отображением и взаимодействием с консолью.

```csharp
// Получение экземпляра
Terminal terminal = Terminal.Instance;

// Логирование сообщений
Terminal.Log("Сообщение для консоли");
Terminal.Log(TerminalLogType.Warning, "Предупреждение: {0}", "текст предупреждения");
Terminal.Log(TerminalLogType.Error, "Ошибка: {0}", "текст ошибки");

// Управление состоянием
terminal.SetState(TerminalState.OpenSmall);  // Открыть маленькое окно
terminal.SetState(TerminalState.OpenFull);   // Открыть полное окно
terminal.SetState(TerminalState.Close);      // Закрыть консоль
terminal.ToggleState(TerminalState.OpenSmall); // Переключить состояние
```

### CommandShell
Обработчик команд в терминале.

```csharp
// Доступ к командному интерпретатору
CommandShell shell = Terminal.Shell;

// Выполнение команды
shell.RunCommand("help");

// Проверка наличия ошибок
if (Terminal.IssuedError)
{
    Debug.LogError(shell.IssuedErrorMessage);
}
```

### CommandHistory
Управляет историей ввода команд.

```csharp
// Доступ к истории команд
CommandHistory history = Terminal.History;

// Получение предыдущей команды (при нажатии "вверх")
string prevCommand = history.Previous();

// Получение следующей команды (при нажатии "вниз")
string nextCommand = history.Next();

// Добавление команды в историю
history.Push("help");
```

### CommandAutocomplete
Обеспечивает автодополнение команд.

```csharp
// Доступ к автодополнению
CommandAutocomplete autocomplete = Terminal.Autocomplete;

// Регистрация новой команды для автодополнения
autocomplete.Register("my_command");

// Получение вариантов автодополнения
string headText = "he";
string[] completions = autocomplete.Complete(ref headText);
// completions = ["lp", "llo"]
// headText = "he"
```

## Регистрация команд

### Использование атрибутов:
```csharp
// Регистрация команды через атрибут
public class MyCommands : MonoBehaviour
{
    [RegisterCommand("hello", "Выводит приветствие")]
    static void HelloCommand(CommandArg[] args)
    {
        Terminal.Log("Привет, мир!");
    }
    
    [RegisterCommand("add", "Складывает два числа")]
    static void AddCommand(CommandArg[] args)
    {
        if (args.Length < 2)
        {
            throw new CommandException("Необходимо указать два числа");
        }
        
        int a = args[0].Int;
        int b = args[1].Int;
        
        Terminal.Log("{0} + {1} = {2}", a, b, a + b);
    }
}
```

### Ручная регистрация:
```csharp
// Регистрация команды вручную
Terminal.Shell.RegisterCommand("time", "Показывает текущее время", args => 
{
    Terminal.Log("Текущее время: {0}", DateTime.Now.ToString("HH:mm:ss"));
});

// Регистрация команды с параметрами
Terminal.Shell.RegisterCommand("greet", "Приветствует пользователя", args => 
{
    string name = args.Length > 0 ? args[0].String : "незнакомец";
    Terminal.Log("Привет, {0}!", name);
});
```

## Настройка внешнего вида

```csharp
[Header("Window")]
[Range(0, 1)]
[SerializeField] float maxHeight = 0.7f;           // Максимальная высота окна

[Range(100, 1000)]
[SerializeField] float toggleSpeed = 360;          // Скорость открытия/закрытия

[SerializeField] string toggleHotkey = "`";        // Горячая клавиша для открытия
[SerializeField] string toggleFullHotkey = "#`";   // Горячая клавиша для полноэкранного режима

[Header("Input")]
[SerializeField] Font consoleFont;                 // Шрифт консоли
[SerializeField] string inputCaret = ">";          // Строка ввода
[SerializeField] bool showGUIButtons;              // Показывать кнопки GUI
[SerializeField] bool rightAlignButtons;           // Выравнивание кнопок справа

[Header("Theme")]
[SerializeField] Color backgroundColor = Color.black;  // Цвет фона
[SerializeField] Color foregroundColor = Color.white;  // Основной цвет текста
[SerializeField] Color shellColor = Color.white;       // Цвет сообщений оболочки
[SerializeField] Color inputColor = Color.cyan;        // Цвет ввода
[SerializeField] Color warningColor = Color.yellow;    // Цвет предупреждений
[SerializeField] Color errorColor = Color.red;         // Цвет ошибок
```

## Обработка аргументов

CommandArg предоставляет методы для конвертации строковых аргументов в различные типы:

```csharp
Terminal.Shell.RegisterCommand("complex", "Демонстрация работы с аргументами", args => 
{
    if (args.Length < 3)
    {
        throw new CommandException("Укажите имя, возраст и баланс");
    }
    
    string name = args[0].String;  // Получить строку
    int age = args[1].Int;         // Получить целое число
    float balance = args[2].Float; // Получить число с плавающей точкой
    bool isPremium = args.Length > 3 ? args[3].Bool : false; // Получить булево значение
    
    Terminal.Log("Имя: {0}, Возраст: {1}, Баланс: {2:F2}, Премиум: {3}", 
        name, age, balance, isPremium ? "Да" : "Нет");
});
```

## Примеры использования

### Системные команды:
```csharp
// help - Список всех команд
// clear - Очистить консоль
// version - Показать версию
// quit - Выйти из приложения
```

### Создание игровых чит-кодов:
```csharp
public class CheatCommands : MonoBehaviour
{
    [RegisterCommand("god", "Включает режим бессмертия")]
    static void GodModeCommand(CommandArg[] args)
    {
        bool enable = args.Length == 0 || args[0].Bool;
        PlayerManager.Instance.SetGodMode(enable);
        Terminal.Log("Режим бессмертия: {0}", enable ? "включен" : "выключен");
    }
    
    [RegisterCommand("gold", "Добавляет золото")]
    static void AddGoldCommand(CommandArg[] args)
    {
        int amount = args.Length > 0 ? args[0].Int : 100;
        PlayerManager.Instance.AddGold(amount);
        Terminal.Log("Добавлено {0} золота", amount);
    }
    
    [RegisterCommand("spawn", "Создает объект")]
    static void SpawnCommand(CommandArg[] args)
    {
        if (args.Length == 0)
        {
            throw new CommandException("Укажите имя объекта для создания");
        }
        
        string prefabName = args[0].String;
        int count = args.Length > 1 ? args[1].Int : 1;
        
        GameManager.Instance.SpawnPrefab(prefabName, count);
        Terminal.Log("Создано {0}x {1}", count, prefabName);
    }
}
```

### Интеграция с системой логирования:
```csharp
public class CustomLogger : MonoBehaviour
{
    void Start()
    {
        // Перенаправляет логи Unity в терминал
        Application.logMessageReceived += HandleLog;
    }
    
    void HandleLog(string message, string stackTrace, LogType type)
    {
        TerminalLogType terminalLogType;
        
        switch (type)
        {
            case LogType.Warning:
                terminalLogType = TerminalLogType.Warning;
                break;
            case LogType.Error:
            case LogType.Exception:
                terminalLogType = TerminalLogType.Error;
                break;
            default:
                terminalLogType = TerminalLogType.Message;
                break;
        }
        
        Terminal.Log(terminalLogType, message);
    }
}
```

## Лучшие практики

1. **Используйте групповые префиксы** для связанных команд (например, `player_health`, `player_ammo`)
2. **Добавляйте информативные описания** для всех команд
3. **Обрабатывайте ошибки и краевые случаи** при работе с аргументами
4. **Категоризируйте сообщения** используя соответствующие типы логов
5. **Не выводите конфиденциальную информацию** через консоль в публичных сборках
6. **Разделяйте команды для отладки и команды для игроков**
7. **Реализуйте уровни доступа** для команд в публичных билдах
