# Master Server Toolkit - Ядро

## Описание
Master Server Toolkit (версия 4.20.0) - это система для создания выделенных серверов и мультиплеерных игр. Ядро состоит из двух основных компонентов: класса `Mst` и `MasterServerBehaviour`.

## Класс Mst
Центральный класс фреймворка, предоставляющий доступ ко всем основным системам.

### Основные свойства:
```csharp
// Версия и название фреймворка
Mst.Version     // "4.20.0"
Mst.Name        // "Master Server Toolkit"

// Основное подключение к master серверу  
Mst.Connection  // IClientSocket

// Расширенные настройки фреймворка
Mst.Settings    // MstAdvancedSettings
```

### Основные компоненты:
```csharp
// Клиентские методы
Mst.Client      // MstClient - для игровых клиентов
Mst.Server      // MstServer - для игровых серверов

// Утилиты и хелперы
Mst.Helper      // Вспомогательные методы
Mst.Security    // Безопасность и шифрование
Mst.Create      // Создание сокетов и сообщений
Mst.Concurrency // Работа с потоками

// Системы
Mst.Events      // Канал событий
Mst.Runtime     // Работа с данными времени выполнения
Mst.Args        // Аргументы командной строки
```

## Класс MasterServerBehaviour

Синглтон для управления работой master сервера в Unity.

### Пример использования:
```csharp
// Получение экземпляра
var masterServer = MasterServerBehaviour.Instance;

// События
MasterServerBehaviour.OnMasterStartedEvent += (server) => {
    Debug.Log("Master Server запущен!");
};

MasterServerBehaviour.OnMasterStoppedEvent += (server) => {
    Debug.Log("Master Server остановлен!");
};
```

### Ключевые особенности:
- Автоматический запуск при старте сцены
- Singleton pattern для единственного экземпляра
- Поддержка аргументов командной строки для IP и порта
- События для отслеживания состояния сервера

### Аргументы командной строки:
```csharp
// IP адрес master сервера
Mst.Args.AsString(Mst.Args.Names.MasterIp, defaultIP);

// Порт master сервера  
Mst.Args.AsInt(Mst.Args.Names.MasterPort, defaultPort);
```

## Быстрый старт

1. Добавить MasterServerBehaviour на сцену
2. Настроить IP и порт
3. Запустить сцену или установить аргументы командной строки

```bash
# Пример запуска с аргументами
./MyGameServer.exe -masterip 127.0.0.1 -masterport 5000
```

## Важные замечания
- Каждый master сервер использует единственный экземпляр MasterServerBehaviour
- Все компоненты инициализируются автоматически при первом использовании Mst класса
- События OnMasterStarted/Stopped должны быть подписаны до запуска сервера
