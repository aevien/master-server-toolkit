# Master Server Toolkit - UI Components

## Обзор

UI компоненты в Master Server Toolkit предоставляют готовые решения для отображения данных, настраиваемых свойств, прогресс-баров и многого другого. Они созданы для простой интеграции с системой представлений и упрощения создания сложных пользовательских интерфейсов.

## Ключевые компоненты

### UIProperty

Универсальный компонент для отображения именованных свойств с возможностью задания иконки, значения и прогресс-бара:

```csharp
public class UIProperty : MonoBehaviour
{
    // Форматы вывода значений (F0 - F5)
    public enum UIPropertyValueFormat { F0, F1, F2, F3, F4, F5 }
    
    // Компоненты
    [SerializeField] protected Image iconImage;
    [SerializeField] protected TextMeshProUGUI lableText;
    [SerializeField] protected TextMeshProUGUI valueText;
    [SerializeField] protected Image progressBar;
    [SerializeField] protected Color minColor = Color.red;
    [SerializeField] protected Color maxColor = Color.green;
    
    // Настройки
    [SerializeField] protected string id = "propertyId";
    [SerializeField] protected float minValue = 0f;
    [SerializeField] protected float currentValue = 50f;
    [SerializeField] protected float maxValue = float.MaxValue;
    [SerializeField] protected float progressSpeed = 1f;
    [SerializeField] protected bool smoothValue = true;
    [SerializeField] protected string lable = "";
    [SerializeField] protected UIPropertyValueFormat formatValue = UIPropertyValueFormat.F1;
    [SerializeField] protected bool invertValue = false;
    
    // Методы установки значений
    public void SetMin(float value);
    public void SetMax(float value);
    public void SetValue(float value);
}
```

### UIProgressBar

Упрощенная версия UIProperty, сфокусированная на отображении прогресса:

```csharp
public class UIProgressBar : UIProperty
{
    // Специализированная версия со специфичной логикой отображения прогресса
}
```

### UIProgressProperty

Компонент для связывания прогресс-бара с динамически меняющимся свойством:

```csharp
public class UIProgressProperty : MonoBehaviour
{
    [SerializeField] protected UIProperty property;
    [SerializeField] protected float updateInterval = 0.2f;
    [SerializeField] protected UnityEvent<float> onValueChangedEvent;
    
    // Связывание с источником значения и автоматическое обновление
}
```

### UILable

Компонент для работы с текстовыми метками:

```csharp
public class UILable : MonoBehaviour
{
    [SerializeField] protected TextMeshProUGUI textComponent;
    [SerializeField] protected string text = "";
    
    public string Text { get; set; }
    public TextMeshProUGUI TextComponent { get; }
}
```

### UIMultiLable

Компонент для синхронизации нескольких текстовых меток:

```csharp
public class UIMultiLable : MonoBehaviour
{
    [SerializeField] protected string text = "";
    [SerializeField] protected List<TextMeshProUGUI> labels;
    
    // Синхронизирует текст для группы меток
}
```

### DataTableLayoutGroup

Компонент для создания табличного представления данных:

```csharp
public class DataTableLayoutGroup : MonoBehaviour
{
    [SerializeField] protected GameObject cellPrefab;
    [SerializeField] protected int rowsCount = 0;
    [SerializeField] protected int columnsCount = 0;
    [SerializeField] protected float spacing = 2f;
    [SerializeField] protected Vector2 cellSize = new Vector2(100f, 30f);
    
    // Методы для создания и наполнения таблицы
    public void SetValue(int row, int column, string value);
    public void Clear();
    public void Rebuild(int rows, int columns);
}
```

## Пример использования

### Отображение статистики игрока

```csharp
public class PlayerStatsView : UIView
{
    [SerializeField] private UIProperty healthProperty;
    [SerializeField] private UIProperty manaProperty;
    [SerializeField] private UIProperty experienceProperty;
    
    private Player player;
    
    public void Initialize(Player player)
    {
        this.player = player;
        
        // Настройка свойств
        healthProperty.SetMin(0);
        healthProperty.SetMax(player.maxHealth);
        healthProperty.SetValue(player.currentHealth);
        
        manaProperty.SetMin(0);
        manaProperty.SetMax(player.maxMana);
        manaProperty.SetValue(player.currentMana);
        
        experienceProperty.SetMin(0);
        experienceProperty.SetMax(player.experienceToNextLevel);
        experienceProperty.SetValue(player.currentExperience);
        
        // Подписка на обновления
        player.OnHealthChanged += (newValue) => healthProperty.SetValue(newValue);
        player.OnManaChanged += (newValue) => manaProperty.SetValue(newValue);
        player.OnExperienceChanged += (newValue) => experienceProperty.SetValue(newValue);
    }
}
```

### Создание таблицы лидеров

```csharp
public class LeaderboardView : UIView
{
    [SerializeField] private DataTableLayoutGroup dataTable;
    
    public void PopulateLeaderboard(List<PlayerScore> scores)
    {
        // Создание таблицы размером по количеству игроков и 3 колонки
        dataTable.Rebuild(scores.Count, 3);
        
        // Заполнение заголовков
        dataTable.SetValue(0, 0, "Ранг");
        dataTable.SetValue(0, 1, "Игрок");
        dataTable.SetValue(0, 2, "Счет");
        
        // Заполнение данных игроков
        for (int i = 0; i < scores.Count; i++)
        {
            dataTable.SetValue(i + 1, 0, (i + 1).ToString());
            dataTable.SetValue(i + 1, 1, scores[i].PlayerName);
            dataTable.SetValue(i + 1, 2, scores[i].Score.ToString());
        }
    }
}
```

### Прогресс-бар загрузки

```csharp
public class LoadingScreen : UIView
{
    [SerializeField] private UIProgressBar progressBar;
    [SerializeField] private UILable statusLabel;
    
    public void UpdateProgress(float progress, string status)
    {
        progressBar.SetValue(progress);
        statusLabel.Text = status;
    }
    
    // Использование:
    // loadingScreen.UpdateProgress(0.5f, "Загрузка активов...");
}
```

## Рекомендации по использованию

1. **Именование свойств**: Присваивайте уникальные ID свойствам для удобного доступа к ним
2. **Анимация переходов**: Используйте `smoothValue = true` для плавного изменения значений
3. **Форматирование**: Подбирайте подходящий `formatValue` для читаемого вывода чисел
4. **Инверсия**: Используйте `invertValue` для инвертирования прогресс-баров (например, для урона)
5. **Реактивность**: Подписывайтесь на события изменения данных для автоматического обновления UI

UI компоненты в Master Server Toolkit предназначены для работы как самостоятельно, так и в составе более сложных представлений. Они предоставляют базовую функциональность, которую можно расширять и настраивать под конкретные нужды проекта.