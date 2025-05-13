# Master Server Toolkit - Achievements

## Описание
Модуль достижений для создания, отслеживания и разблокировки достижений игроков. Интегрируется с профилями пользователей для сохранения прогресса.

## AchievementsModule

Основной класс модуля достижений.

### Настройка:
```csharp
[Header("Permission")]
[SerializeField] protected bool clientCanUpdateProgress = false;

[Header("Settings")]
[SerializeField] protected AchievementsDatabase achievementsDatabase;
```

### Зависимости:
Модуль требует наличия:
- AuthModule - для аутентификации пользователей
- ProfilesModule - для сохранения прогресса достижений

## Создание базы данных достижений

### AchievementsDatabase:
```csharp
// Создание ScriptableObject с достижениями
[CreateAssetMenu(menuName = "Master Server Toolkit/Achievements Database")]
public class AchievementsDatabase : ScriptableObject
{
    [SerializeField] private List<AchievementData> achievements = new List<AchievementData>();
    
    public IReadOnlyCollection<AchievementData> Achievements => achievements;
}
```

### Определение достижений:
```csharp
// Создание в Unity Editor
var database = ScriptableObject.CreateInstance<AchievementsDatabase>();

// Добавление достижений
database.Add(new AchievementData
{
    Key = "first_victory",
    Title = "First Victory",
    Description = "Win your first match",
    Type = AchievementType.Normal,
    MaxProgress = 1,
    Unlockable = true
});

database.Add(new AchievementData
{
    Key = "win_streak",
    Title = "Win Streak",
    Description = "Win 10 matches in a row",
    Type = AchievementType.Incremental,
    MaxProgress = 10,
    Unlockable = true
});

// Сохранение базы достижений
AssetDatabase.CreateAsset(database, "Assets/Resources/AchievementsDatabase.asset");
AssetDatabase.SaveAssets();
```

## Типы достижений

### AchievementType:
```csharp
public enum AchievementType
{
    Normal,         // Обычное достижение (0/1)
    Incremental,    // Постепенное достижение (0/N)
    Infinite        // Бесконечное достижение (трекинг без разблокировки)
}
```

### Структура данных достижения:
```csharp
[Serializable]
public class AchievementData
{
    public string Key;
    public string Title;
    public string Description;
    public AchievementType Type;
    public int MaxProgress;
    public bool Unlockable;
    public Sprite Icon;
    public AchievementExtraData[] ResultCommands;
    
    [Serializable]
    public class AchievementExtraData
    {
        public string CommandKey;
        public string[] CommandValues;
    }
}
```

## Обновление прогресса достижений

### Со стороны сервера:
```csharp
// Получить модуль
var achievementsModule = Mst.Server.Modules.GetModule<AchievementsModule>();

// Обновить прогресс достижения
void UpdateAchievement(string userId, string achievementKey, int progress)
{
    var packet = new UpdateAchievementProgressPacket
    {
        userId = userId, 
        key = achievementKey,
        progress = progress
    };
    
    // Отправить пакет обновления
    Mst.Server.SendMessage(MstOpCodes.ServerUpdateAchievementProgress, packet);
}

// Пример использования
UpdateAchievement(user.UserId, "first_victory", 1);
```

### Со стороны клиента (если разрешено):
```csharp
// Клиентский запрос на обновление прогресса
void UpdateAchievement(string achievementKey, int progress)
{
    var packet = new UpdateAchievementProgressPacket
    {
        key = achievementKey,
        progress = progress
    };
    
    Mst.Client.Connection.SendMessage(MstOpCodes.ClientUpdateAchievementProgress, packet, (status, response) =>
    {
        if (status == ResponseStatus.Success)
        {
            Debug.Log($"Achievement progress updated for {achievementKey}");
        }
    });
}
```

## Интеграция с профилями

Достижения автоматически синхронизируются с профилями пользователей через свойство профиля:

```csharp
// Регистрация свойства достижений в ProfilesModule
profilesModule.RegisterProperty(ProfilePropertyOpCodes.achievements, null, () =>
{
    return new ObservableAchievements();
});

// Получение достижений из профиля
void GetAchievements(ObservableServerProfile profile)
{
    if (profile.TryGet(ProfilePropertyOpCodes.achievements, out ObservableAchievements achievements))
    {
        // Список всех достижений пользователя
        var allAchievements = achievements.GetAll();
        
        // Проверка разблокировано ли достижение
        bool isUnlocked = achievements.IsUnlocked("first_victory");
        
        // Получить прогресс достижения
        int progress = achievements.GetProgress("win_streak");
    }
}
```

## Отслеживание событий разблокировки

### Подписка на события клиента:
```csharp
// В клиентском классе
private void Start()
{
    Mst.Client.Connection.RegisterMessageHandler(MstOpCodes.ClientAchievementUnlocked, OnAchievementUnlocked);
}

private void OnAchievementUnlocked(IIncomingMessage message)
{
    string achievementKey = message.AsString();
    Debug.Log($"Achievement unlocked: {achievementKey}");
    
    // Показать интерфейс разблокировки достижения
    ShowAchievementUnlockedUI(achievementKey);
}
```

## Настройка наград за достижения

Модуль позволяет настроить команды, которые будут выполнены при разблокировке достижения:

```csharp
// Настройка ResultCommands в достижении
var achievement = new AchievementData
{
    Key = "100_matches_played",
    Title = "Veteran",
    Description = "Play 100 matches",
    Type = AchievementType.Incremental,
    MaxProgress = 100,
    Unlockable = true,
    
    // Команды для выполнения при разблокировке
    ResultCommands = new[]
    {
        new AchievementExtraData
        {
            CommandKey = "add_currency",
            CommandValues = new[] { "gold", "100" }
        },
        new AchievementExtraData
        {
            CommandKey = "unlock_avatar",
            CommandValues = new[] { "veteran_avatar" }
        }
    }
};
```

### Обработка команд:
```csharp
// Расширение модуля для обработки команд
public class MyAchievementsModule : AchievementsModule
{
    protected override void OnAchievementResultCommand(IUserPeerExtension user, string key, AchievementExtraData[] resultCommands)
    {
        foreach (var command in resultCommands)
        {
            switch (command.CommandKey)
            {
                case "add_currency":
                    AddCurrency(user, command.CommandValues[0], int.Parse(command.CommandValues[1]));
                    break;
                    
                case "unlock_avatar":
                    UnlockAvatar(user, command.CommandValues[0]);
                    break;
                
                // Другие команды
                default:
                    logger.Error($"Unknown achievement command: {command.CommandKey}");
                    break;
            }
        }
    }
    
    private void AddCurrency(IUserPeerExtension user, string currencyType, int amount)
    {
        // Добавление валюты игроку
    }
    
    private void UnlockAvatar(IUserPeerExtension user, string avatarId)
    {
        // Разблокировка аватара игроку
    }
}
```

## Клиентская обертка

### AchievementsModuleClient:
```csharp
// Пример использования
var client = Mst.Client.Modules.GetModule<AchievementsModuleClient>();

// Получение списка достижений
var achievements = client.GetAchievements();

// Отображение интерфейса
void ShowAchievementsUI()
{
    foreach (var achievement in achievements)
    {
        // Создать элемент интерфейса для достижения
        var item = Instantiate(achievementItemPrefab, container);
        
        // Заполнить данными
        item.SetTitle(achievement.Title);
        item.SetDescription(achievement.Description);
        item.SetIcon(achievement.Icon);
        item.SetProgress(achievement.CurrentProgress, achievement.MaxProgress);
        item.SetUnlocked(achievement.IsUnlocked);
    }
}
```

## Лучшие практики

1. **Используйте уникальные ключи** для каждого достижения
2. **Разделяйте одиночные и инкрементальные** достижения
3. **Внедряйте проверку на стороне сервера** для предотвращения читерства
4. **Выполняйте логику наград на сервере**
5. **Кэшируйте данные достижений** на клиенте для быстрого доступа
6. **Разблокируйте похожие достижения** автоматически (например, "Убить 10 монстров" автоматически разблокирует "Убить 5 монстров")
7. **Собирайте аналитику** по достижениям для оценки геймплея
