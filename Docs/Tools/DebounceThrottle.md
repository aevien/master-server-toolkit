# Master Server Toolkit - DebounceThrottle

## Описание
Система для управления частыми вызовами функций с использованием паттернов Debounce и Throttle. Помогает оптимизировать производительность, контролируя количество выполнений операций в единицу времени.

## Основные компоненты

### DebounceDispatcher
Отложенный вызов - выполняет функцию только один раз, после окончания серии вызовов, с заданной задержкой.

```csharp
// Создание с интервалом в миллисекундах
var debouncer = new DebounceDispatcher(500); // 500 мс задержка

// Использование с асинхронной функцией
await debouncer.DebounceAsync(async () => {
    await SaveDataAsync();
});

// Использование с синхронной функцией
debouncer.Debounce(() => {
    UpdateUi();
});
```

### ThrottleDispatcher
Ограничение вызовов - гарантирует, что функция будет вызвана не чаще, чем указанный интервал, отбрасывая промежуточные вызовы.

```csharp
// Создание с интервалом и опциями
var throttler = new ThrottleDispatcher(
    1000,                       // 1000 мс интервал
    delayAfterExecution: false, // Начинать отсчет интервала с начала выполнения
    resetIntervalOnException: true // Сбрасывать интервал при исключении
);

// Использование с асинхронной функцией
await throttler.ThrottleAsync(async () => {
    await SendAnalyticsDataAsync();
});

// Использование с синхронной функцией
throttler.Throttle(() => {
    UpdateProgressBar();
});
```

## Дженерик-версии

### DebounceDispatcher<T>
Версия с типизированным возвращаемым значением.

```csharp
var typedDebouncer = new DebounceDispatcher<int>(500);

// Получение результата из функции
int result = await typedDebouncer.DebounceAsync(async () => {
    return await CalculateValueAsync();
});
```

### ThrottleDispatcher<T>
Версия с типизированным возвращаемым значением.

```csharp
var typedThrottler = new ThrottleDispatcher<List<string>>(1000);

// Получение результата из функции
List<string> items = await typedThrottler.ThrottleAsync(async () => {
    return await FetchItemsAsync();
});
```

## Практические примеры

### Поиск в реальном времени

```csharp
public class SearchHandler : MonoBehaviour
{
    private DebounceDispatcher debouncer;
    
    private void Start()
    {
        debouncer = new DebounceDispatcher(300); // 300 мс задержка
    }
    
    // Вызывается при изменении текста в поле ввода
    public void OnSearchTextChanged(string text)
    {
        debouncer.Debounce(() => {
            // Запрос к API только когда пользователь перестал печатать
            StartCoroutine(PerformSearch(text));
        });
    }
}
```

### Ограничение запросов к серверу

```csharp
public class ApiClient : MonoBehaviour
{
    private ThrottleDispatcher throttler;
    
    private void Start()
    {
        // Не более 1 запроса в секунду
        throttler = new ThrottleDispatcher(1000);
    }
    
    public async Task<PlayerData> GetPlayerDataAsync(string playerId)
    {
        return await throttler.ThrottleAsync(async () => {
            // Запрос к серверу с ограничением частоты
            return await FetchPlayerDataFromServerAsync(playerId);
        });
    }
}
```

### Обработка ввода пользователя

```csharp
public class InputHandler : MonoBehaviour
{
    private ThrottleDispatcher throttler;
    
    private void Start()
    {
        // Обрабатывать не чаще 10 раз в секунду
        throttler = new ThrottleDispatcher(100);
    }
    
    private void Update()
    {
        if (Input.GetKey(KeyCode.Space))
        {
            throttler.Throttle(() => {
                // Действие по нажатию пробела, ограниченное по частоте
                FireWeapon();
            });
        }
    }
}
```

### Автосохранение с отложенным запуском

```csharp
public class DocumentEditor : MonoBehaviour
{
    private DebounceDispatcher saveDebouncer;
    
    private void Start()
    {
        // Сохранять через 2 секунды после последнего изменения
        saveDebouncer = new DebounceDispatcher(2000);
    }
    
    public void OnDocumentChanged()
    {
        // Визуальная индикация несохраненных изменений
        ShowUnsavedChangesIndicator();
        
        // Отложенное сохранение
        saveDebouncer.Debounce(() => {
            SaveDocument();
            HideUnsavedChangesIndicator();
        });
    }
}
```

## Отличия Debounce от Throttle

### Debounce (Отложенный вызов)
- Задерживает выполнение до тех пор, пока не пройдет указанный интервал без вызовов
- Идеально для событий, которые могут быстро следовать друг за другом, но требуют обработки только после их завершения
- Примеры: поиск при вводе, изменение размера окна, прокрутка

### Throttle (Ограничение частоты)
- Гарантирует, что функция выполняется не чаще, чем один раз в указанный интервал
- Идеально для ограничения частоты повторяющихся действий
- Примеры: отправка аналитики, обновление интерфейса, запросы к API

## Опции и настройки

### DebounceDispatcher
- `interval` - время в миллисекундах, которое должно пройти без вызовов, прежде чем функция будет выполнена

### ThrottleDispatcher
- `interval` - минимальное время в миллисекундах между вызовами функции
- `delayAfterExecution` - если true, интервал отсчитывается после завершения выполнения функции
- `resetIntervalOnException` - если true, сбрасывает интервал при возникновении исключения
