# Production Calendar

Сервис производственного календаря: получение конкретного дня и инициализация полного календаря страны на год из JSON с исключениями.

## Текущий этап

Созданы девять проектов приложения .NET 10. Инкременты 1–3 одобрены и закоммичены: 40c016b (контракты), c206593 (построение календаря), da90587 (Storage API). Инкремент 4 реализует handler получения дня: валидация страны, точная дата, один вызов storage supplier, GetOut/IsWorkingDay и маппинг ошибок; ожидает ревью без коммита. По решению пользователя от 2026-10-07 тесты не создаются. Handler инициализации, HTTP-функции, инфраструктура и workflows будут добавляться по [плану](IMPLEMENTATION_PLAN.md).

`AzureFunc` пока собирается как библиотека (`OutputType=Library`), поскольку у пустого проекта нет точки входа. На этапе хоста добавить `Program.cs` и переключить `OutputType` на `Exe`. Сейчас это не запускаемое приложение Azure Functions.

## Документация

- [Правила работы и код-стиль](AGENTS.md).
- [Архитектура и сопоставление с образцом](ARCHITECTURE.md).
- [Требования, формат данных и поведение](SPECIFICATION.md).
- [Пошаговый план и критерии готовности](IMPLEMENTATION_PLAN.md).
- [Журнал инкрементов и принятых решений](DEVELOPMENT_LOG.md).
- [План инфраструктуры и CI/CD](.infra/README.md).
- [Будущие GitHub Actions](.github/workflows/README.md).

## Структура

```text
Internal.ProductionCalendar.slnx
src/
  app/AzureFunc/
    Application/
    Function/
  endpoint/
    ProductionCalendarDay.Get/
      Contract/
      Handler/Handler/
    ProductionCalendar.Initialize/
      Contract/
      Handler/Handler/
  service/
    ProductionCalendar/
      Contract/ProductionCalendar.Build/
      Api/
        ProductionCalendarApi/
    Storage/
      Contract/
        ProductionCalendarDay.Get/
        ProductionCalendarDay.Set/
      Api/
        StorageApi/
        Internal.Table/
        Option/
.infra/
  modules/
  apim/
  scripts/
.github/workflows/
```

Пустые каталоги сохраняются через `.gitkeep`; удалить соответствующий маркер при добавлении файлов. Имя проекта (`Contract`, `Api`, `Handler`) повторяет образец; полные уникальные имена заданы в `AssemblyName`.

## Проверка решения

```shell
dotnet restore Internal.ProductionCalendar.slnx
dotnet build Internal.ProductionCalendar.slnx --no-restore -c Release
```

Тестовых проектов и зависимостей нет. Верификация проекта — сборка и применимые ручные проверки; написание тестов исключено пользователем. История ранее выполненных проверок сохранена в DEVELOPMENT_LOG.md.

После одобрения инкремента 4: коммит, затем этап 5 плана — handler инициализации года.

Замечания ревью от 2026-10-06 учтены: Comment nullable без инициализатора; модель построенного календаря сохраняет отсутствие комментария как null, будущая запись Storage применяет OrEmpty. Null-forgiving не используется; сравнения следуют правилам is/is not.

Коллекции по умолчанию — FlatArray, как в образце. Входные Days, генерация календаря и HTTP headers Storage используют FlatArray; Date — DateOnly, Type — DayType, Comment — nullable string. Отдельного поля Json нет; JSON входа обрабатывается будущим HTTP-адаптером.

StorageOption требует ServiceUri и TableName=ProductionCalendar. Release-сборка успешна; реальные обращения к Azure отложены до хоста и конфигурации доступа. Storage API не создаёт таблицу — это задача инфраструктуры.

CancellationToken передаётся в AsyncPipeline и асинхронные зависимости. Явные ThrowIfCancellationRequested удалены по замечанию пользователя от 2026-10-07; чистые синхронные стадии не принимают токен.
