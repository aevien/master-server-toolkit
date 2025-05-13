# Master Server Toolkit - Quests

## Описание
Модуль для создания, отслеживания и выполнения игровых квестов с возможностью настройки цепочек заданий, временных ограничений и наград.

## Основные структуры

### QuestData (ScriptableObject)
```csharp
[CreateAssetMenu(menuName = "Master Server Toolkit/Quests/New Quest")]
public class QuestData : ScriptableObject
{
    // Основная информация
    public string Key => key;                 // Уникальный ключ квеста
    public string Title => title;             // Название
    public string Description => description; // Описание
    public int RequiredProgress => requiredProgress; // Кол-во для завершения
    public Sprite Icon => icon;               // Иконка квеста
    
    // Сообщения для разных статусов
    public string StartMessage => startMessage;       // Сообщение при взятии квеста
    public string ActiveMessage => activeMessage;     // Сообщение во время выполнения
    public string CompletedMessage => completedMessage; // Сообщение при завершении
    public string CancelMessage => cancelMessage;      // Сообщение при отмене
    public string ExpireMessage => expireMessage;      // Сообщение при истечении срока
    
    // Настройки
    public bool IsOneTime => isOneTime;               // Одноразовый квест
    public int TimeToComplete => timeToComplete;      // Время на выполнение (мин)
    
    // Связи с другими квестами
    public QuestData ParentQuest => parentQuest;       // Родительский квест
    public QuestData[] ChildrenQuests => childrenQuests; // Дочерние квесты
}
```

### Статусы квестов
```csharp
public enum QuestStatus 
{ 
    Inactive,   // Квест неактивен
    Active,     // Квест активен
    Completed,  // Квест завершен
    Canceled,   // Квест отменен
    Expired     // Время квеста истекло
}
```

### Интерфейс IQuestInfo
```csharp
public interface IQuestInfo
{
    string Id { get; set; }                 // Уникальный ID
    string Key { get; set; }                // Ключ квеста
    string UserId { get; set; }             // ID пользователя
    int Progress { get; set; }              // Текущий прогресс
    int Required { get; set; }              // Требуемый прогресс
    DateTime StartTime { get; set; }        // Время начала
    DateTime ExpireTime { get; set; }       // Время истечения
    DateTime CompleteTime { get; set; }     // Время завершения
    QuestStatus Status { get; set; }        // Статус
    string ParentQuestKey { get; set; }     // Ключ родительского квеста
    string ChildrenQuestsKeys { get; set; } // Ключи дочерних квестов
    bool TryToComplete(int progress);       // Метод для завершения квеста
}
```

## QuestsModule (Сервер)

```csharp
// Зависимости
AddDependency<AuthModule>();
AddDependency<ProfilesModule>();

// Настройки
[Header("Permission"), SerializeField]
protected bool clientCanUpdateProgress = false; // Может ли клиент обновлять прогресс

[Header("Settings"), SerializeField]
protected QuestsDatabase[] questsDatabases; // Базы данных квестов
```

### Основные операции сервера
1. Получение списка квестов
2. Начало квеста
3. Обновление прогресса квеста
4. Отмена квеста
5. Проверка истечения срока квестов

### Интеграция с профилями
```csharp
private void ProfilesModule_OnProfileLoaded(ObservableServerProfile profile)
{
    if (profile.TryGet(ProfilePropertyOpCodes.quests, out ObservableQuests property))
    {
        // Инициализация квестов пользователя
    }
}
```

## QuestsModuleClient (Клиент)

```csharp
// Получение списка доступных квестов
questsClient.GetQuests((quests) => {
    foreach (var quest in quests)
    {
        Debug.Log($"Quest: {quest.Title}, Status: {quest.Status}");
    }
});

// Начать квест
questsClient.StartQuest("quest_key", (isStarted, quest) => {
    if (isStarted)
        Debug.Log($"Started quest: {quest.Title}");
});

// Обновить прогресс квеста
questsClient.UpdateQuestProgress("quest_key", 5, (isUpdated) => {
    if (isUpdated)
        Debug.Log("Quest progress updated");
});

// Отменить квест
questsClient.CancelQuest("quest_key", (isCanceled) => {
    if (isCanceled)
        Debug.Log("Quest canceled");
});
```

## Пример создания квестов

### Создание базы данных квестов
```csharp
// В редакторе Unity
[CreateAssetMenu(menuName = "Master Server Toolkit/Quests/QuestsDatabase")]
public class QuestsDatabase : ScriptableObject
{
    [SerializeField]
    private List<QuestData> quests = new List<QuestData>();
    
    public IReadOnlyCollection<QuestData> Quests => quests;
}

// Создание базы данных
var database = ScriptableObject.CreateInstance<QuestsDatabase>();
```

### Создание квеста
```csharp
// В редакторе Unity
var quest = ScriptableObject.CreateInstance<QuestData>();
quest.name = "CollectResources";
// Настройка квеста через инспектор

// Программно
var questData = new QuestData
{
    Key = "collect_wood",
    Title = "Collect Wood",
    Description = "Collect 10 pieces of wood",
    RequiredProgress = 10,
    TimeToComplete = 60 // 60 минут на выполнение
};
```

### Цепочки квестов
```csharp
// Создание зависимостей между квестами
var mainQuest = ScriptableObject.CreateInstance<QuestData>();
mainQuest.name = "MainQuest";

var subQuest1 = ScriptableObject.CreateInstance<QuestData>();
subQuest1.name = "SubQuest1";
// Установка mainQuest как родительского для subQuest1

var subQuest2 = ScriptableObject.CreateInstance<QuestData>();
subQuest2.name = "SubQuest2";
// Установка mainQuest как родительского для subQuest2

// В родительском квесте указываем дочерние
// mainQuest.ChildrenQuests = new QuestData[] { subQuest1, subQuest2 };
```

## Лучшие практики

1. **Создавайте осмысленные ключи квестов** для лучшей идентификации
2. **Группируйте квесты** по тематическим базам данных
3. **Определяйте реалистичные сроки** выполнения квестов
4. **Используйте цепочки квестов** для создания сюжетных линий
5. **Обеспечьте отказоустойчивость** при обработке квестов
6. **Предоставляйте понятные сообщения** для каждого статуса квеста
