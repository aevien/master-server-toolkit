# Master Server Toolkit - Matchmaker

## Описание
Модуль для поиска игр, комнат и лобби по критериям, а также информации о доступных регионах серверов.

## Основные компоненты

### MatchmakerModule (Сервер)
```csharp
// Зависимости
AddOptionalDependency<LobbiesModule>();
AddOptionalDependency<RoomsModule>();
AddOptionalDependency<SpawnersModule>();

// Основные методы
public void AddProvider(IGamesProvider provider) // Добавить провайдер игр
```

### MstMatchmakerClient (Клиент)
```csharp
// Поиск игр (без фильтров)
matchmaker.FindGames((games) => {
    Debug.Log($"Found {games.Count} games");
});

// Поиск игр с фильтрами
var filters = new MstProperties();
filters.Set("minPlayers", 2);
matchmaker.FindGames(filters, (games) => { });

// Получение регионов
matchmaker.GetRegions((regions) => {
    // regions[0].Name, regions[0].Ip, regions[0].PingTime
});
```

## Ключевые структуры данных

### GameInfoPacket
```csharp
public class GameInfoPacket
{
    public int Id { get; set; }                    // ID игры
    public string Address { get; set; }            // Адрес подключения
    public GameInfoType Type { get; set; }         // Тип (Room, Lobby, Custom)
    public string Name { get; set; }               // Название
    public string Region { get; set; }             // Регион
    public bool IsPasswordProtected { get; set; }  // Требуется пароль
    public int MaxPlayers { get; set; }            // Максимум игроков
    public int OnlinePlayers { get; set; }         // Текущее число игроков
    public List<string> OnlinePlayersList { get; } // Список игроков
    public MstProperties Properties { get; set; }  // Доп. свойства
}
```

### RegionInfo
```csharp
public class RegionInfo
{
    public string Name { get; set; }  // Название региона
    public string Ip { get; set; }    // IP-адрес
    public int PingTime { get; set; } // Пинг (мс)
}
```

## Пользовательские провайдеры игр

### Интерфейс IGamesProvider
```csharp
public interface IGamesProvider
{
    IEnumerable<GameInfoPacket> GetPublicGames(IPeer peer, MstProperties filters);
}
```

### Пример минимальной реализации
```csharp
public class CustomGamesProvider : MonoBehaviour, IGamesProvider
{
    private List<GameInfoPacket> games = new List<GameInfoPacket>();
    
    public IEnumerable<GameInfoPacket> GetPublicGames(IPeer peer, MstProperties filters)
    {
        // Фильтрация по региону
        if (filters.Has("region"))
        {
            return games.Where(g => g.Region == filters.AsString("region"));
        }
        
        return games;
    }
    
    // API для добавления/удаления игр
    public void AddGame(GameInfoPacket game) => games.Add(game);
    public void RemoveGame(int gameId) => games.RemoveAll(g => g.Id == gameId);
}

// Регистрация
matchmaker.AddProvider(gameObject.AddComponent<CustomGamesProvider>());
```

## Пример использования

### Поиск игр с несколькими фильтрами
```csharp
var filters = new MstProperties();
filters.Set("region", "eu-west");     // Только европейский регион
filters.Set("minPlayers", 1);         // С минимум 1 игроком
filters.Set("gameMode", "deathmatch"); // Режим "deathmatch"

matchmaker.FindGames(filters, (games) =>
{
    // Найденные игры
    foreach (var game in games)
    {
        Debug.Log($"{game.Name} - {game.OnlinePlayers}/{game.MaxPlayers}");
        
        // Подключение к игре
        Mst.Client.Rooms.JoinRoom(game.Id, "", (successful, roomAccess) =>
        {
            if (successful)
                ConnectToGameServer(roomAccess);
        });
    }
});
```

### Выбор региона с лучшим пингом
```csharp
matchmaker.GetRegions((regions) =>
{
    if (regions.Count > 0)
    {
        // Сортировка по пингу
        var bestRegion = regions.OrderBy(r => r.PingTime).First();
        PlayerPrefs.SetString("SelectedRegion", bestRegion.Name);
        
        Debug.Log($"Using region: {bestRegion.Name} ({bestRegion.PingTime}ms)");
    }
});
```

## Лучшие практики

1. **Используйте осмысленные имена свойств** для фильтрации
2. **Добавляйте категории** в Properties для лучшей организации фильтров
3. **Обрабатывайте случай отсутствия игр** в клиентском коде
4. **Сортируйте результаты** для улучшения пользовательского опыта
5. **Кэшируйте список регионов** и результаты поиска при необходимости
6. **Обновляйте список регионов** при запуске игры
