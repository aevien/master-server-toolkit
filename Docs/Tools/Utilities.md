# Master Server Toolkit - Utilities

## Описание
Набор универсальных утилит и вспомогательных классов для упрощения разработки. Включает паттерны проектирования, расширения стандартных типов, вспомогательные классы для Unity и многое другое.

## Основные компоненты

### Шаблоны проектирования

#### Singleton
```csharp
// Базовый синглтон для MonoBehaviour
public class PlayerManager : SingletonBehaviour<PlayerManager>
{
    // Доступ из любого места проекта
    public static PlayerManager Instance => GetInstance();
    
    public void DoSomething()
    {
        // Реализация
    }
}

// Использование
PlayerManager.Instance.DoSomething();
```

#### Динамический Singleton
```csharp
// Автоматически создаваемый синглтон
public class AudioManager : DynamicSingletonBehaviour<AudioManager>
{
    // Будет создан на сцене, если отсутствует
    public static AudioManager Instance => GetInstance();
}

// Использование
AudioManager.Instance.PlaySound("explosion");
```

#### Глобальный Singleton
```csharp
// Синглтон, который сохраняется при смене сцен
public class GameManager : GlobalDynamicSingletonBehaviour<GameManager>
{
    // Переживает смену сцен
    public static GameManager Instance => GetInstance();
}

// Использование
GameManager.Instance.StartNewGame();
```

#### Пул объектов
```csharp
// Создание пула для эффективного управления объектами
public class BulletPool : MonoBehaviour
{
    [SerializeField] private Bullet bulletPrefab;
    [SerializeField] private int initialSize = 50;
    
    private GenericPool<Bullet> bulletPool;
    
    private void Awake()
    {
        bulletPool = new GenericPool<Bullet>(CreateBullet, initialSize);
    }
    
    private Bullet CreateBullet()
    {
        return Instantiate(bulletPrefab);
    }
    
    public Bullet GetBullet()
    {
        return bulletPool.Get();
    }
    
    public void ReturnBullet(Bullet bullet)
    {
        bulletPool.Return(bullet);
    }
}

// Использование
var bullet = bulletPool.GetBullet();
// После использования
bulletPool.ReturnBullet(bullet);
```

#### Реестр объектов
```csharp
// Создание реестра для управления объектами по ключу
public class ItemRegistry : BaseRegistry<string, ItemData>
{
    // Методы регистрации уже реализованы в базовом классе
}

// Использование
var registry = new ItemRegistry();
registry.Register("sword", new ItemData { /* данные */ });
var item = registry.Get("sword");
```

### Расширения

#### Расширения для строк
```csharp
// Проверка строки на пустоту
if (username.IsNullOrEmpty())
{
    // Обработка
}

// Хеширование строки
string passwordHash = password.GetMD5();

// Преобразование в Base64
string encoded = text.ToBase64();
string decoded = encoded.FromBase64();

// Извлечение последнего сегмента пути
string filename = filePath.GetLastSegment('\\');

// Безопасное разделение строки
string[] parts = path.SplitSafe('/');
```

#### Расширения для массивов байтов
```csharp
// Преобразование в строку
byte[] data = GetData();
string text = data.ToString(StringFormat.Utf8);

// Преобразование в Base64
string base64 = data.ToBase64();

// Создание подмассива
byte[] header = data.SubArray(0, 10);

// Слияние массивов
byte[] fullPacket = headerBytes.CombineWith(bodyBytes);

// Проверка на равенство
if (hash1.BytesEqual(hash2))
{
    // Обработка
}
```

#### Расширения для Transform
```csharp
// Сброс локальных трансформаций
transform.ResetLocal();

// Масштабирование всех дочерних объектов
transform.ScaleAllChildren(0.5f);

// Уничтожение всех дочерних объектов
transform.DestroyChildren();

// Поиск в глубину
var target = transform.FindRecursively("Player/Weapon/Barrel");

// Установка локальной позиции по отдельным осям
transform.SetLocalX(10f);
transform.SetLocalY(5f);
transform.SetLocalZ(0f);
```

### Вспомогательные классы

#### ScenesLoader
```csharp
// Асинхронная загрузка сцены с прогрессом
ScenesLoader.LoadSceneAsync("Level1", (progress) => {
    // Обновление индикатора загрузки
    loadingBar.value = progress;
}, () => {
    // Вызывается по завершении загрузки
    Debug.Log("Load complete");
});

// Загрузка сцены с затемнением
ScenesLoader.LoadSceneWithFade("MainMenu", Color.black, 1.5f);

// Перезагрузка текущей сцены
ScenesLoader.ReloadCurrentScene();
```

#### ScreenshotMaker
```csharp
// Захват скриншота экрана
ScreenshotMaker.TakeScreenshot((texture) => {
    // Использование полученной текстуры
    screenshotImage.texture = texture;
});

// Сохранение скриншота в файл
ScreenshotMaker.SaveScreenshot("Screenshots/screenshot.png");

// Захват скриншота определенной камеры
ScreenshotMaker.TakeScreenshot(myCamera, 1920, 1080, (texture) => {
    // Обработка
});
```

#### SimpleNameGenerator
```csharp
// Генерация случайных имен
string randomName = SimpleNameGenerator.Generate(length: 6);

// Генерация имени с заданным префиксом
string playerName = SimpleNameGenerator.Generate("Player_", 4);

// Генерация имени из слогов
string nameFromSyllables = SimpleNameGenerator.GenerateFromSyllables(3);

// Создание уникального идентификатора
string uniqueId = SimpleNameGenerator.GenerateUniqueName();
```

#### MstWebBrowser
```csharp
// Открытие URL во внешнем браузере
MstWebBrowser.OpenURL("https://example.com");

// Открытие URL с проверкой поддержки
if (MstWebBrowser.CanOpenURL)
{
    MstWebBrowser.OpenURL("https://example.com");
}

// Открытие локального HTML-файла
MstWebBrowser.OpenLocalFile("Documentation.html");
```

#### NetWebRequests
```csharp
// Отправка GET-запроса
NetWebRequests.Get("https://api.example.com/data", (success, response) => {
    if (success)
    {
        // Обработка ответа
        Debug.Log(response);
    }
});

// Отправка POST-запроса
var data = new Dictionary<string, string>
{
    { "username", "player1" },
    { "score", "100" }
};

NetWebRequests.Post("https://api.example.com/scores", data, (success, response) => {
    if (success)
    {
        Debug.Log("Score submitted");
    }
});

// Загрузка текстуры
NetWebRequests.GetTexture("https://example.com/image.jpg", (success, texture) => {
    if (success)
    {
        // Использование текстуры
        profileImage.texture = texture;
    }
});
```

### Сериализуемые структуры

#### SerializedKeyValuePair
```csharp
// Сериализуемая пара ключ-значение для использования в инспекторе Unity
[Serializable]
public class StringIntPair : SerializedKeyValuePair<string, int> { }

public class InventoryManager : MonoBehaviour
{
    [SerializeField] 
    private List<StringIntPair> startingItems = new List<StringIntPair>();
    
    private void Start()
    {
        foreach (var item in startingItems)
        {
            AddItem(item.Key, item.Value);
        }
    }
    
    private void AddItem(string itemId, int count)
    {
        // Реализация
    }
}
```

## Примеры использования

### Создание игрового менеджера
```csharp
public class GameManager : GlobalDynamicSingletonBehaviour<GameManager>
{
    // Состояние игры
    public GameState CurrentState { get; private set; }
    
    // События
    public event Action<GameState> OnGameStateChanged;
    
    // Изменение состояния игры
    public void ChangeState(GameState newState)
    {
        CurrentState = newState;
        OnGameStateChanged?.Invoke(newState);
    }
    
    // Загрузка нового уровня
    public void LoadLevel(int levelIndex)
    {
        ChangeState(GameState.Loading);
        
        ScenesLoader.LoadSceneAsync($"Level_{levelIndex}", (progress) => {
            // Обновление прогресса
        }, () => {
            ChangeState(GameState.Playing);
        });
    }
}

// Использование
GameManager.Instance.LoadLevel(1);
```

### Реализация системы пулинга
```csharp
public class EffectsPool : SingletonBehaviour<EffectsPool>
{
    [Serializable]
    public class EffectPoolData
    {
        public string effectId;
        public GameObject prefab;
        public int initialSize;
    }
    
    [SerializeField] private List<EffectPoolData> effectsData;
    
    private Dictionary<string, GenericPool<GameObject>> effectPools = new Dictionary<string, GenericPool<GameObject>>();
    
    protected override void Awake()
    {
        base.Awake();
        
        // Инициализация пулов
        foreach (var data in effectsData)
        {
            var pool = new GenericPool<GameObject>(() => Instantiate(data.prefab), data.initialSize);
            effectPools.Add(data.effectId, pool);
        }
    }
    
    public GameObject SpawnEffect(string effectId, Vector3 position, Quaternion rotation)
    {
        if (effectPools.TryGetValue(effectId, out var pool))
        {
            var effect = pool.Get();
            effect.transform.position = position;
            effect.transform.rotation = rotation;
            effect.SetActive(true);
            
            return effect;
        }
        
        Debug.LogWarning($"Effect {effectId} not found in pools");
        return null;
    }
    
    public void ReturnEffect(string effectId, GameObject effect)
    {
        if (effectPools.TryGetValue(effectId, out var pool))
        {
            effect.SetActive(false);
            pool.Return(effect);
        }
    }
}

// Использование
void PlayExplosion(Vector3 position)
{
    var effect = EffectsPool.Instance.SpawnEffect("explosion", position, Quaternion.identity);
    
    // Автоматический возврат в пул через 2 секунды
    StartCoroutine(ReturnAfterDelay("explosion", effect, 2f));
}

IEnumerator ReturnAfterDelay(string effectId, GameObject effect, float delay)
{
    yield return new WaitForSeconds(delay);
    EffectsPool.Instance.ReturnEffect(effectId, effect);
}
```

### Создание менеджера настроек
```csharp
public class SettingsManager : SingletonBehaviour<SettingsManager>
{
    // Настройки
    public float MasterVolume { get; private set; } = 1f;
    public float MusicVolume { get; private set; } = 0.8f;
    public float SfxVolume { get; private set; } = 1f;
    public int QualityLevel { get; private set; } = 2;
    public bool Fullscreen { get; private set; } = true;
    
    // События
    public event Action OnSettingsChanged;
    
    private void Start()
    {
        LoadSettings();
    }
    
    public void SetMasterVolume(float volume)
    {
        MasterVolume = Mathf.Clamp01(volume);
        OnSettingsChanged?.Invoke();
        SaveSettings();
    }
    
    // Аналогичные методы для других настроек
    
    private void SaveSettings()
    {
        PlayerPrefs.SetFloat("MasterVolume", MasterVolume);
        PlayerPrefs.SetFloat("MusicVolume", MusicVolume);
        PlayerPrefs.SetFloat("SfxVolume", SfxVolume);
        PlayerPrefs.SetInt("QualityLevel", QualityLevel);
        PlayerPrefs.SetInt("Fullscreen", Fullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }
    
    private void LoadSettings()
    {
        MasterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        MusicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.8f);
        SfxVolume = PlayerPrefs.GetFloat("SfxVolume", 1f);
        QualityLevel = PlayerPrefs.GetInt("QualityLevel", 2);
        Fullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
        
        OnSettingsChanged?.Invoke();
    }
}

// Использование
SettingsManager.Instance.SetMasterVolume(0.5f);
```

## Лучшие практики

1. **Используйте синглтоны с осторожностью** — они упрощают код, но могут создать проблемы с зависимостями
2. **Предпочитайте пулинг для часто создаваемых/уничтожаемых объектов** — это значительно улучшит производительность
3. **Используйте расширения методов для повышения читаемости кода**
4. **Помните о статусе экспериментальных функций** — некоторые утилиты могут работать не на всех платформах
5. **Избегайте излишнего усложнения** — использование утилит должно упрощать код, а не усложнять его
6. **Максимально применяйте типизацию** для предотвращения ошибок в рантайме
7. **Документируйте собственные расширения** этих утилит для поддержки кода в будущем
