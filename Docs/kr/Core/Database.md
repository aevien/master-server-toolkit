# Master Server Toolkit - 데이터베이스

## 설명
데이터베이스와 API에 접근하기 위한 시스템으로, 다양한 저장소 타입을 추상화하여 제공합니다.

## IDatabaseAccessor
모든 데이터베이스 액세서가 구현해야 하는 기본 인터페이스입니다.
```csharp
public interface IDatabaseAccessor : IDisposable
{
    MstProperties CustomProperties { get; }
    Logger Logger { get; set; }
}
```

### 사용자 정의 액세서 구현 예
```csharp
public class MySQLAccessor : IDatabaseAccessor
{
    public MstProperties CustomProperties { get; } = new MstProperties();
    public Logger Logger { get; set; }

    // MySQL 관련 메서드 구현
    public async Task<User> GetUserById(string userId)
    {
        // 사용자 조회 로직
    }

    public void Dispose()
    {
        // 리소스 해제
    }
}
```

## MstDbAccessor
여러 데이터베이스 액세서를 관리하는 매니저입니다.

### 주요 메서드
```csharp
// 액세서 추가
AddAccessor(IDatabaseAccessor access);

// 타입별 액세서 가져오기
T GetAccessor<T>() where T : class, IDatabaseAccessor;
```

### 사용 예
```csharp
// 중앙 DB 매니저 생성
var dbManager = new MstDbAccessor();

// 다양한 액세서 추가
dbManager.AddAccessor(new MongoDbAccessor());
dbManager.AddAccessor(new RedisAccessor());
dbManager.AddAccessor(new MySQLAccessor());

// 필요한 액세서 얻기
var mongoDb = dbManager.GetAccessor<MongoDbAccessor>();
var redis = dbManager.GetAccessor<RedisAccessor>();

// 액세서 사용
var user = await mongoDb.GetUserById("user123");
await redis.SetCache("key", data, TimeSpan.FromMinutes(30));
```

## DatabaseAccessorFactory
데이터베이스 액세서 생성을 위한 추상 팩토리입니다.

### 구현 예시
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

### 인스펙터 설정 방법
1. GameObject를 생성합니다.
2. DatabaseAccessorFactory를 상속한 컴포넌트를 추가합니다.
3. 연결 문자열과 DB 타입을 설정합니다.
4. 실행 시 액세서가 자동으로 생성됩니다.

## 액세서 예시

### Redis 액세서
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

### MongoDB 액세서
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
        // MongoDB 드라이버가 자동으로 처리
    }
}
```

## 모범 사례
1. **하나의 액세서, 하나의 역할**: 각 액세서는 하나의 저장소 타입만 담당합니다.
2. **팩토리 사용**: 액세서 생성은 DatabaseAccessorFactory를 통해 수행합니다.
3. **로깅 활용**: 액세서에 Logger를 항상 설정합니다.
4. **리소스 해제**: Dispose 구현을 정확히 합니다.
5. **비동기 사용**: 데이터베이스 작업에는 async/await를 사용합니다.

## 확장 구조 예시
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
