# Master Server Toolkit - Документация

## Описание
Master Server Toolkit - это фреймворк для создания многопользовательских игр с архитектурой клиент-сервер. Он предоставляет готовые модули для аутентификации, профилей, комнат, лобби, чата и многих других аспектов многопользовательской игры.

## Основные модули

### Ядро системы
- [Authentication](Modules/Authentication.md) - Аутентификация и управление пользователями
- [Profiles](Modules/Profiles.md) - Профили пользователей и управление данными
- [Rooms](Modules/Rooms.md) - Система комнат и игровых сессий

### Игровые модули
- [Achievements](Modules/Achievements.md) - Система достижений
- [Censor](Modules/Censor.md) - Фильтрация нежелательного контента
- [Chat](Modules/Chat.md) - Система чата и обмена сообщениями
- [Lobbies](Modules/Lobbies.md) - Система лобби перед игрой
- [Matchmaker](Modules/Matchmaker.md) - Подбор игр и фильтрация по критериям
- [Notification](Modules/Notification.md) - Система уведомлений
- [Ping](Modules/Ping.md) - Проверка соединения и замер задержки
- [QuestsModule](Modules/QuestsModule.md) - Система квестов и заданий
- [WorldRooms](Modules/WorldRooms.md) - Система постоянных игровых зон

### Инфраструктура
- [Spawner](Modules/Spawner.md) - Запуск игровых серверов 
- [WebServer](Modules/WebServer.md) - Встроенный веб-сервер для API и админ-панели

### Аналитика и Мониторинг
- [AnalyticsModule](Modules/AnalyticsModule.md) - Сбор и анализ игровых событий

### Инструменты
- [Tools](Tools/README.md) - Набор вспомогательных инструментов для разработки
  - [UI Framework](Tools/UI/README.md) - Система пользовательского интерфейса
  - [Attributes](Tools/Attributes.md) - Расширения для инспектора Unity
  - [Terminal](Tools/Terminal.md) - Отладочный терминал
  - [Tweener](Tools/Tweener.md) - Инструменты анимации
  - [Utilities](Tools/Utilities.md) - Вспомогательные утилиты

## Структура модулей

Каждый модуль обычно состоит из следующих компонентов:
1. **Серверный модуль** (`*Module.cs`) - серверная логика модуля
2. **Серверная реализация** (`*ModuleServer.cs`) - реализация API сервера
3. **Клиентская часть** (`*ModuleClient.cs`) - клиентское API для взаимодействия с сервером
4. **Пакеты** (`Packets/*.cs`) - структуры данных для обмена между клиентом и сервером
5. **Интерфейсы и модели** - определение контрактов и объектов данных

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

1. **Модульная архитектура** - добавляйте только те модули, которые вам нужны
2. **Использование интерфейсов** - создавайте собственные реализации для интеграции с вашей системой
3. **Безопасность** - всегда проверяйте права доступа на стороне сервера
4. **Масштабирование** - используйте систему спаунеров для балансировки нагрузки
5. **Планирование** - продумайте взаимодействие модулей заранее

## Дополнительная информация

Для более подробной информации обратитесь к документации соответствующих модулей.
