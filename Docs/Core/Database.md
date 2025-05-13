# Master Server Toolkit - Database

## Описание
Система доступа к базам данных и API. Предоставляет абстракцию для работы с различными типами хранилищ данных.

## IDatabaseAccessor

Базовый интерфейс для всех аксессоров базы данных.

```csharp
public interface IDatabaseAccessor : IDisposable
{
    MstProperties CustomProperties { get; }
    Logger Logger { get; set; }
}
```

### Реализация собственного аксессора:
```csharp
public class MySQLAccessor : IDatabaseAccessor
{
    public MstProperties CustomProperties { get; } = new MstProperties();
    public Logger Logger { get; set; }
    
    // Реализация методов работы с MySQL
    public async Task<User> GetUserById(string userId)
    {
        // Логика получения пользователя
    }
    
    public void Dispose()
    {
        // Освобождение ресурсов
    }
}
```

## MstDbAccessor

Менеджер для работы с различными аксессорами базы данных.

### Основные методы:
```csharp
// Добавление аксессора
AddAccessor(IDatabaseAccessor access);

// Получение аксессора по типу
T GetAccessor<T>() where T : class, IDatabaseAccessor;
```

### Пример использования:
```csharp
// Создание центрального менеджера БД
var dbManager = new MstDbAccessor();

// Добавление различных аксессоров
dbManager.AddAccessor(new MongoDbAccessor());
dbManager.AddAccessor(new RedisAccessor());
dbManager.AddAccessor(new MySQLAccessor());

// Получение нужного аксессора
var mongoDb = dbManager.GetAccessor<MongoDbAccessor>();
var redis = dbManager.GetAccessor<RedisAccessor>();

// Использование аксессора
var user = await mongoDb.GetUserById("user123");
await redis.SetCache("key", data, TimeSpan.FromMinutes(30));
```

## DatabaseAccessorFactory

Абстрактная фабрика для создания аксессоров базы данных.

### Пример реализации:
```csharp
public class GameDatabaseFactory : DatabaseAccessorFactory
{
    [Header("Database Settings")]
    [SerializeField] private string connectionString;
    [SerializeField] private DatabaseType dbType;
    
    public override void CreateAccessors()
    {
        switch (dbType)
        {
            case DatabaseType.MongoDB:
                var mongoDb = new MongoDbAccessor(connectionString);
                mongoDb.Logger = logger;
                Mst.Server.DbAccessors.AddAccessor(mongoDb);
                break;
                
            case DatabaseType.MySQL:
                var mysql = new MySQLAccessor(connectionString);
                mysql.Logger = logger;
                Mst.Server.DbAccessors.AddAccessor(mysql);
                break;
                
            case DatabaseType.Redis:
                var redis = new RedisAccessor(connectionString);
                redis.Logger = logger;
                Mst.Server.DbAccessors.AddAccessor(redis);
                break;
        }
        
        logger.Info("Database accessors created successfully");
    }
}
```

### Настройка через Inspector:
1. Создать GameObject
2. Добавить компонент наследника DatabaseAccessorFactory
3. Настроить строку подключения и тип БД
4. Аксессоры будут созданы автоматически при запуске

## Примеры аксессоров

### Redis аксессор:
```csharp
public class RedisAccessor : IDatabaseAccessor
{
    private ConnectionMultiplexer _redis;
    private IDatabase _db;
    
    public Logger Logger { get; set; }
    public MstProperties CustomProperties { get; } = new MstProperties();
    
    public RedisAccessor(string connectionString)
    {
        _redis = ConnectionMultiplexer.Connect(connectionString);
        _db = _redis.GetDatabase();
    }
    
    public async Task SetCache(string key, string value, TimeSpan? expiry = null)
    {
        await _db.StringSetAsync(key, value, expiry);
    }
    
    public async Task<string> GetCache(string key)
    {
        return await _db.StringGetAsync(key);
    }
    
    public void Dispose()
    {
        _redis?.Dispose();
    }
}
```

### MongoDB аксессор:
```csharp
public class MongoDbAccessor : IDatabaseAccessor
{
    private IMongoClient _client;
    private IMongoDatabase _database;
    
    public Logger Logger { get; set; }
    public MstProperties CustomProperties { get; } = new MstProperties();
    
    public MongoDbAccessor(string connectionString)
    {
        _client = new MongoClient(connectionString);
        _database = _client.GetDatabase("gamedb");
    }
    
    public async Task<User> GetUserById(string userId)
    {
        var collection = _database.GetCollection<User>("users");
        return await collection.Find(u => u.Id == userId).FirstOrDefaultAsync();
    }
    
    public void Dispose()
    {
        // MongoDB driver handles disposal automatically
    }
}
```

## Лучшие практики

1. **Один аксессор - одна ответственность**: Каждый аксессор должен работать с одним типом хранилища
2. **Используйте фабрику**: Создавайте аксессоры через DatabaseAccessorFactory
3. **Логгирование**: Всегда устанавливайте Logger для аксессоров
4. **Освобождение ресурсов**: Реализуйте Dispose правильно
5. **Асинхронность**: Используйте async/await для операций с БД

## Структура для масштабирования

```
DatabaseAccessors/
├── SQL/
│   ├── MySQLAccessor.cs
│   └── PostgreSQLAccessor.cs
├── NoSQL/
│   ├── MongoDbAccessor.cs
│   └── CassandraAccessor.cs
├── Cache/
│   ├── RedisAccessor.cs
│   └── MemcachedAccessor.cs
└── Factories/
    ├── MainDatabaseFactory.cs
    └── CacheDatabaseFactory.cs
```
