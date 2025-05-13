# Master Server Toolkit - System Views

## Обзор

Система Views в Master Server Toolkit предоставляет мощный инструмент для организации пользовательского интерфейса. Она основана на концепции отдельных представлений, которые могут независимо отображаться, скрываться и анимироваться.

## Ключевые классы

### IUIView

Интерфейс, определяющий базовые операции представления:

```csharp
public interface IUIView
{
    string Id { get; }
    bool IsVisible { get; }
    RectTransform Rect { get; }
    bool IgnoreHideAll { get; set; }
    bool BlockInput { get; set; }
    bool UnlockCursor { get; set; }
    
    void Show(bool instantly = false);
    void Hide(bool instantly = false);
    void Toggle(bool instantly = false);
}
```

### UIView

Базовый класс представления, реализующий `IUIView`:

```csharp
[RequireComponent(typeof(Canvas), typeof(GraphicRaycaster), typeof(CanvasGroup))]
public class UIView : MonoBehaviour, IUIView
{
    [Header("Identity Settings")]
    [SerializeField] protected string id = "New View Id";
    [SerializeField] protected string title = "";

    [Header("Shared Settings")]
    [SerializeField] protected bool hideOnStart = true;
    [SerializeField] protected bool allwaysOnTop = false;
    [SerializeField] protected bool ignoreHideAll = false;
    [SerializeField] protected bool useRaycastBlock = true;
    [SerializeField] protected bool blockInput = false;
    [SerializeField] protected bool unlockCursor = false;
    
    // События
    public UnityEvent OnShowEvent;
    public UnityEvent OnHideEvent;
    public UnityEvent OnShowFinishedEvent;
    public UnityEvent OnHideFinishedEvent;
    
    // Методы Show, Hide, Toggle
}
```

### UIViewPanel

Расширение `UIView` для создания панелей:

```csharp
public class UIViewPanel : UIView
{
    // Дополнительная функциональность для панелей
}
```

### UIViewSync

Компонент для синхронизации нескольких представлений:

```csharp
public class UIViewSync : MonoBehaviour
{
    [SerializeField] protected string mainViewId;
    [SerializeField] protected List<string> syncViewIds = new List<string>();
    [SerializeField] protected bool hideOnStart = true;
    
    // Синхронизирует состояние всех присоединенных представлений
}
```

### PopupView

Специализированное представление для диалоговых окон:

```csharp
public class PopupView : MonoBehaviour, IUIViewComponent
{
    public Button confirmButton;
    public Button declineButton;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI messageText;

    // События для подтверждения/отклонения
    public UnityEvent OnConfirmEvent;
    public UnityEvent OnDeclineEvent;
}
```

## ViewsManager

Статический класс для управления всеми представлениями:

```csharp
public static class ViewsManager
{
    // Проверка блокировки ввода и курсора
    public static bool AnyInputBlockViewVisible { get; }
    public static bool AnyCursorUnlockViewVisible { get; }

    // Регистрация и получение представлений
    public static void Register(string viewId, IUIView view);
    public static void Unregister(string viewId);
    public static T GetView<T>(string viewId) where T : class, IUIView;

    // Управление представлениями
    public static void Show(string viewId);
    public static void Hide(string viewId);
    public static void HideAllViews(bool instantly = false);
    public static void HideViewsByName(bool instantly = false, params string[] names);
}
```

## Компоненты взаимодействия с представлениями

### IUIViewComponent

Интерфейс для компонентов, работающих с представлениями:

```csharp
public interface IUIViewComponent
{
    void OnOwnerShow(IUIView owner);
    void OnOwnerHide(IUIView owner);
}
```

### IUIViewInputHandler

Интерфейс для обработчиков ввода:

```csharp
public interface IUIViewInputHandler : IUIViewComponent
{
    // Специфичная логика обработки ввода
}
```

### UIViewKeyInputHandler

Обработчик ввода с клавиатуры:

```csharp
public class UIViewKeyInputHandler : MonoBehaviour, IUIViewInputHandler
{
    [SerializeField] protected KeyCode toggleKey = KeyCode.Escape;
    [SerializeField] protected string toggleViewId = "";
    
    // Переключает вид при нажатии указанной клавиши
}
```

### IUIViewTweener

Интерфейс для аниматоров представлений:

```csharp
public interface IUIViewTweener : MonoBehaviour
{
    IUIView UIView { get; set; }
    void PlayShow();
    void PlayHide();
    void OnFinished(UnityAction callback);
}
```

## UI Sound Components

Компоненты для воспроизведения звуков UI:

```csharp
public class UIViewSound : MonoBehaviour, IUIViewComponent
{
    [SerializeField] protected AudioClip showClip;
    [SerializeField] protected AudioClip hideClip;
    
    // Воспроизводит звуки при появлении/скрытии представления
}

public class UIButtonSound : MonoBehaviour
{
    [SerializeField] protected AudioClip clickClip;
    [SerializeField] protected AudioClip hoverClip;
    
    // Добавляет звуки к кнопкам
}

public class UIToggleSound : MonoBehaviour 
{
    [SerializeField] protected AudioClip onClip;
    [SerializeField] protected AudioClip offClip;
    
    // Добавляет звуки к переключателям
}
```

## Примеры использования

### Базовое создание представления

```csharp
public class GameMenuView : UIView 
{
    [SerializeField] private Button playButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button quitButton;
    
    protected override void Awake()
    {
        base.Awake();
        
        // Подписка на события кнопок
        playButton.onClick.AddListener(OnPlayClick);
        optionsButton.onClick.AddListener(OnOptionsClick);
        quitButton.onClick.AddListener(OnQuitClick);
    }
    
    private void OnPlayClick()
    {
        // Скрываем текущее представление
        Hide();
        // Показываем представление выбора игры
        ViewsManager.Show("GameSelectionView");
    }
    
    private void OnOptionsClick()
    {
        ViewsManager.Show("OptionsView");
    }
    
    private void OnQuitClick()
    {
        Application.Quit();
    }
}
```

### Создание диалога подтверждения

```csharp
// Получение popup представления
var popup = ViewsManager.GetView<PopupView>("ConfirmPopup");

// Настройка текста
popup.SetTitle("Подтверждение");
popup.SetMessage("Вы уверены, что хотите выйти?");

// Подписка на события
popup.OnConfirmEvent.AddListener(() => {
    // Действие при подтверждении
    Application.Quit();
});

popup.OnDeclineEvent.AddListener(() => {
    // Действие при отмене
    popup.Hide();
});

// Показ popup
popup.Show();
```

### Работа с анимированными представлениями

```csharp
// Создание анимированного представления
var view = gameObject.AddComponent<UIView>();
var tweener = gameObject.AddComponent<FadeTweener>(); // Наследник IUIViewTweener

// Показ с анимацией
view.Show(); // Анимированный
view.Show(true); // Мгновенный

// Подписка на завершение анимации
view.OnShowFinishedEvent.AddListener(() => {
    Debug.Log("Анимация появления завершена");
});
```