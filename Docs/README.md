# Master Server Toolkit - Документация

## Описание
Master Server Toolkit - это фреймворк для создания многопользовательских игр с архитектурой клиент-сервер. Он предоставляет готовые модули для аутентификации, профилей, комнат, лобби, чата и многих других аспектов многопользовательской игры.

## Структура системы

### [Ядро системы](Core/README.md)
Базовые компоненты и инфраструктура фреймворка:
- [MasterServer](Core/MasterServer.md) - Центральный компонент системы
- [Client](Core/Client.md) - Клиентская часть системы
- [Server](Core/Server.md) - Серверная часть системы
- [Database](Core/Database.md) - Абстракция базы данных
- [Events](Core/Events.md) - Система событий
- [Keys](Core/Keys.md) - Система констант и ключей
- [Localization](Core/Localization.md) - Система локализации
- [Logger](Core/Logger.md) - Система логирования
- [Mail](Core/Mail.md) - Система отправки email

### [Networking](Networking.md)
Документация по сетевому взаимодействию в системе.

### [Модули](Modules/README.md)
Функциональные компоненты для создания многопользовательских игр:

#### Основные модули
- [Authentication](Modules/Authentication.md) - Аутентификация и управление пользователями
- [Profiles](Modules/Profiles.md) - Профили пользователей и управление данными
- [Rooms](Modules/Rooms.md) - Система комнат и игровых сессий

#### Игровые модули
- [Achievements](Modules/Achievements.md) - Система достижений
- [Censor](Modules/Censor.md) - Фильтрация нежелательного контента
- [Chat](Modules/Chat.md) - Система чата и обмена сообщениями
- [Lobbies](Modules/Lobbies.md) - Система лобби перед игрой
- [Matchmaker](Modules/Matchmaker.md) - Подбор игр и фильтрация по критериям
- [Notification](Modules/Notification.md) - Система уведомлений
- [Ping](Modules/Ping.md) - Проверка соединения и замер задержки
- [QuestsModule](Modules/QuestsModule.md) - Система квестов и заданий
- [WorldRooms](Modules/WorldRooms.md) - Система постоянных игровых зон

#### Инфраструктурные модули
- [Spawner](Modules/Spawner.md) - Запуск игровых серверов 
- [WebServer](Modules/WebServer.md) - Встроенный веб-сервер для API и админ-панели

#### Аналитика и мониторинг
- [AnalyticsModule](Modules/AnalyticsModule.md) - Сбор и анализ игровых событий

### [Инструменты](Tools/README.md)
Набор вспомогательных инструментов для разработки:
- [UI Framework](Tools/UI/README.md) - Система пользовательского интерфейса
  - [Views System](Tools/UI/Views.md) - Управление UI экранами
  - [UI Components](Tools/UI/Components.md) - Готовые компоненты UI
  - [Validation System](Tools/UI/Validation.md) - Валидация форм ввода
- [Attributes](Tools/Attributes.md) - Расширения для инспектора Unity
- [Terminal](Tools/Terminal.md) - Отладочный терминал
- [Tweener](Tools/Tweener.md) - Инструменты анимации
- [Utilities](Tools/Utilities.md) - Вспомогательные утилиты
- [DebounceThrottle](Tools/DebounceThrottle.md) - Ограничение частоты вызовов
- [WebGL](Tools/WebGL.md) - Инструменты для WebGL платформы

## Структура проекта

Базовая структура проекта Master Server Toolkit:

```
Assets/
└── MasterServerToolkit/
    ├── Core/               - Ядро системы
    ├── Modules/            - Модули
    │   ├── Authentication/ - Модуль аутентификации
    │   ├── Profiles/       - Модуль профилей
    │   ├── Rooms/          - Модуль комнат
    │   └── ...             - Другие модули
    ├── Tools/              - Инструменты
    │   ├── UI/             - UI фреймворк
    │   ├── Terminal/       - Терминал
    │   └── ...             - Другие инструменты
    ├── Examples/           - Примеры использования
    └── ThirdParty/         - Сторонние библиотеки
```

## Начало работы

1. **Настройка мастер-сервера**:
   ```csharp
   // Добавление необходимых модулей
   var authModule = gameObject.AddComponent<AuthModule>();
   var profilesModule = gameObject.AddComponent<ProfilesModule>();
   var roomsModule = gameObject.AddComponent<RoomsModule>();
   
   // Настройка подключения к базе данных
   gameObject.AddComponent<YourDatabaseFactory>();
   ```

2. **Настройка клиента**:
   ```csharp
   // Подключение к серверу
   Mst.Client.Connection.Connect("127.0.0.1", 5000, (successful, error) => {
       if (successful)
           Debug.Log("Подключение установлено");
   });
   
   // Использование модулей
   Mst.Client.Auth.SignIn(username, password, (successful, error) => {
       if (successful)
           Debug.Log("Аутентификация успешна");
   });
   ```

## Лучшие практики

1. **Модульная архитектура** - Добавляйте только те модули, которые вам нужны
2. **Использование интерфейсов** - Создавайте собственные реализации для интеграции с вашей системой
3. **Безопасность** - Всегда проверяйте права доступа на стороне сервера
4. **Масштабирование** - Используйте систему спаунеров для балансировки нагрузки
5. **Планирование** - Продумайте взаимодействие модулей заранее

## Дополнительная информация

Для более подробной информации обратитесь к документации соответствующих разделов.
