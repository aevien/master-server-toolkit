# Master Server Toolkit - Profiles

## Описание
Модуль профилей для управления пользовательскими данными, наблюдения за изменениями и синхронизации между клиентами и серверами.

## ProfilesModule

Основной класс для управления профилями пользователей.

### Настройка:
```csharp
[Header("General Settings")]
[SerializeField] protected int unloadProfileAfter = 20;
[SerializeField] protected int saveProfileDebounceTime = 1;
[SerializeField] protected int clientUpdateDebounceTime = 1;
[SerializeField] protected int editProfilePermissionLevel = 0;
[SerializeField] protected int maxUpdateSize = 1048576;

[Header("Timeout Settings")]
[SerializeField] protected int profileLoadTimeoutSeconds = 10;

[Header("Database")]
public DatabaseAccessorFactory databaseAccessorFactory;
[SerializeField] private ObservablePropertyPopulatorsDatabase populatorsDatabase;
```

## Свойства профиля

### Создание системы свойств:
```csharp
// Создание популятора
public class PlayerStatsPopulator : IObservablePropertyPopulator
{
    public IProperty Populate()
    {
        var properties = new ObservableBase();
        
        // Базовые статы
        properties.Set("playerLevel", new ObservableInt(1));
        properties.Set("experience", new ObservableInt(0));
        properties.Set("coins", new ObservableInt(0));
        
        // Словарь для инвентаря
        var inventory = new ObservableDictStringInt();
        inventory.Add("sword", 1);
        inventory.Add("potion", 5);
        properties.Set("inventory", inventory);
        
        return properties;
    }
}
```

## Работа с профилями

### Доступ к профилю (клиент):
```csharp
// Запрос профиля
Mst.Client.Connection.SendMessage(MstOpCodes.ClientFillInProfileValues);

// Подписка на обновления
Mst.Client.Connection.RegisterMessageHandler(MstOpCodes.UpdateClientProfile, OnProfileUpdated);

// Обработка ответа
private void OnProfileUpdated(IIncomingMessage message)
{
    var profile = new ObservableProfile();
    profile.FromBytes(message.AsBytes());
    
    // Доступ к свойствам
    int level = profile.GetProperty<ObservableInt>("playerLevel").Value;
    int coins = profile.GetProperty<ObservableInt>("coins").Value;
}
```

### Доступ к профилю (сервер):
```csharp
// Получение профиля по Id
var profile = profilesModule.GetProfileByUserId(userId);

// Получение профиля по Peer
var profile = profilesModule.GetProfileByPeer(peer);

// Изменение профиля
profile.GetProperty<ObservableInt>("playerLevel").Add(1);
profile.GetProperty<ObservableInt>("coins").Set(100);
```

## События профиля

### Подписка на изменения:
```csharp
// На сервере
profilesModule.OnProfileCreated += OnProfileCreated;
profilesModule.OnProfileLoaded += OnProfileLoaded;

// В профиле
profile.OnModifiedInServerEvent += OnProfileChanged;

// Конкретное свойство
profile.GetProperty<ObservableInt>("playerLevel").OnDirtyEvent += OnLevelChanged;
```

## Синхронизация с базой данных

### Реализация IProfilesDatabaseAccessor:
```csharp
public class ProfilesDatabaseAccessor : IProfilesDatabaseAccessor
{
    public async Task RestoreProfileAsync(ObservableServerProfile profile)
    {
        // Загрузка из БД
        var data = await LoadProfileDataFromDB(profile.UserId);
        if (data != null)
        {
            profile.FromBytes(data);
        }
    }
    
    public async Task UpdateProfilesAsync(List<ObservableServerProfile> profiles)
    {
        // Batch сохранение в БД
        foreach (var profile in profiles)
        {
            await SaveProfileToDB(profile.UserId, profile.ToBytes());
        }
    }
}
```

## Типы наблюдаемых свойств

### Базовые типы:
```csharp
// Числовые
ObservableInt level = new ObservableInt(10);
ObservableFloat health = new ObservableFloat(100.0f);

// Строки
ObservableString name = new ObservableString("Player");

// Списки
ObservableListInt scores = new ObservableListInt();
scores.Add(100);

// Словари
ObservableDictStringInt items = new ObservableDictStringInt();
items.Add("sword", 1);
```

## Серверные обновления

### Отправка обновлений с игрового сервера:
```csharp
// Создание пакета обновлений
var updates = new ProfileUpdatePacket();
updates.UserId = userId;
updates.Properties = new MstProperties();
updates.Properties.Set("playerLevel", 15);
updates.Properties.Set("experience", 1500);

// Отправка на master server
Mst.Server.Connection.SendMessage(MstOpCodes.ServerUpdateProfileValues, updates);
```

## Производительность и оптимизация

### Debounce настройки:
```csharp
// Задержка сохранения в БД (секунды)
saveProfileDebounceTime = 1;

// Задержка отправки обновлений клиенту
clientUpdateDebounceTime = 0.5f;

// Время до выгрузки профиля после выхода
unloadProfileAfter = 20;
```

### Ограничения:
```csharp
// Максимальный размер обновления
maxUpdateSize = 1048576;

// Тайм-аут загрузки профиля
profileLoadTimeoutSeconds = 10;
```

## Примеры использования

### Игровая статистика:
```csharp
public class PlayerStats
{
    public ObservableInt Level { get; private set; }
    public ObservableInt Experience { get; private set; }
    public ObservableFloat Health { get; private set; }
    public ObservableDictStringInt Inventory { get; private set; }
    
    public PlayerStats(ObservableServerProfile profile)
    {
        Level = profile.GetProperty<ObservableInt>("playerLevel");
        Experience = profile.GetProperty<ObservableInt>("experience");
        Health = profile.GetProperty<ObservableFloat>("health");
        Inventory = profile.GetProperty<ObservableDictStringInt>("inventory");
    }
    
    public void AddExperience(int amount)
    {
        Experience.Add(amount);
        
        if (Experience.Value >= GetExperienceForNextLevel())
        {
            LevelUp();
        }
    }
    
    private void LevelUp()
    {
        Level.Add(1);
        Health.Set(100.0f); // Восстановление здоровья при уровне
        Experience.Set(0);
    }
}
```

### Клиентский профиль:
```csharp
public class ProfileUI : MonoBehaviour
{
    [Header("UI References")]
    public Text levelText;
    public Text coinsText;
    public Text healthText;
    
    private ObservableProfile profile;
    
    void Start()
    {
        // Запрос профиля
        Mst.Client.Connection.SendMessage(MstOpCodes.ClientFillInProfileValues);
        
        // Регистрация обработчика обновлений
        Mst.Client.Connection.RegisterMessageHandler(MstOpCodes.UpdateClientProfile, OnProfileUpdate);
    }
    
    private void OnProfileUpdate(IIncomingMessage message)
    {
        if (profile == null)
            profile = new ObservableProfile();
            
        profile.ApplyUpdates(message.AsBytes());
        
        // Обновление UI
        UpdateUI();
    }
    
    private void UpdateUI()
    {
        levelText.text = $"Level: {profile.GetProperty<ObservableInt>("playerLevel").Value}";
        coinsText.text = $"Coins: {profile.GetProperty<ObservableInt>("coins").Value}";
        healthText.text = $"Health: {profile.GetProperty<ObservableFloat>("health").Value}";
    }
}
```

## Лучшие практики

1. **Используйте популяторы** для инициализации профилей
2. **Группируйте обновления** для снижения нагрузки
3. **Настройте debounce** для оптимизации производительности
4. **Проверяйте размер обновлений** для предотвращения атак
5. **Используйте типизированные свойства** для безопасности
6. **Подписывайтесь на события** для реактивного программирования
7. **Очищайте неиспользуемые профили** для освобождения памяти
8. **Реализуйте резервное копирование** для важных данных
