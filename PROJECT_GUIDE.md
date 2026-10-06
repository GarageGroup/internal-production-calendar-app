# Production Calendar

Сервис производственного календаря: получение конкретного дня и инициализация полного календаря страны на год из JSON с исключениями.

## Текущий этап

Созданы девять проектов приложения .NET 10. Инкремент 1 (контракты и бизнес-модель) одобрен и закоммичен: 40c016b. Инкремент 2 реализует чистое построение календаря из типизированной модели Country/Year/Days и строгую валидацию; ожидает ревью, коммит не выполнен. По решению пользователя от 2026-10-07 написанные тесты удалены, новые тесты не создаются. Storage API, handlers, HTTP-функции, инфраструктура и workflows будут добавляться последовательно по [плану](IMPLEMENTATION_PLAN.md).

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

После одобрения инкремента 2: коммит, затем этап 3 плана — Azure Table Storage API.

Замечания ревью от 2026-10-06 учтены: Comment nullable без инициализатора; модель построенного календаря сохраняет отсутствие комментария как null, будущая запись Storage применяет OrEmpty. Null-forgiving не используется; сравнения следуют правилам is/is not.

Коллекции по умолчанию — FlatArray, как в образце. Входные Days и генерация календаря используют FlatArray; Date — DateOnly, Type — DayType, Comment — nullable string. Отдельного поля Json нет; JSON обрабатывается будущим HTTP-адаптером. Инкремент 2 по-прежнему ожидает одобрения.

CancellationToken передаётся в AsyncPipeline и асинхронные зависимости. Явные ThrowIfCancellationRequested удалены по замечанию пользователя от 2026-10-07; чистые синхронные стадии не принимают токен.
