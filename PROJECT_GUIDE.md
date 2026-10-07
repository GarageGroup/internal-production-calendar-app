# Production Calendar

Сервис производственного календаря: получение конкретного дня и инициализация полного календаря страны на год из JSON с исключениями.

## Текущий этап

Созданы десять проектов приложения .NET 10. Инкременты 1–5 одобрены и закоммичены: 40c016b (контракты), c206593 (построение), da90587 (Storage), 277057c (Get handler), 6d2a63a (Initialize handler и исправления ревью). Инкремент 6 одобрен и закоммичен: 27dbcb8 — Functions host, HTTP API и новый порядок этапов: Functions host, composition root, HTTP-маршруты и отдельные JSON-модели. Тесты не создаются по решению пользователя.

AzureFunc теперь OutputType=Exe. Release build/publish успешны; Core Tools 4.8.0 запустил worker и зарегистрировал оба HTTP-маршрута. Вручную подтверждены ответы 400 для неверной даты и некорректного JSON. Успешные чтение/запись в Table Storage и credential refresh со Storage пока не проверены; это этап 8, после развёртывания Test через CI/CD на этапе 7.

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
  shared/DayType/
    DayType.csproj
    DayType.cs
    DayTypeExtensions.cs
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

Инкремент 7 одобрен и закоммичен: 556f33d — Bicep, четыре scripts, пять GitHub workflows, System Assigned MI и APIM placeholder. По уточнению пользователя подготовлены только файлы; запуск Azure/CI/CD выполняет пользователь. После deployment Test — инкремент 8, ручная интеграционная проверка.

Замечания ревью от 2026-10-06 учтены: Comment nullable без инициализатора; модель построенного календаря сохраняет отсутствие комментария как null, будущая запись Storage применяет OrEmpty. Null-forgiving не используется; сравнения следуют правилам is/is not.

Коллекции по умолчанию — FlatArray, как в образце. Входные Days, генерация календаря и HTTP headers Storage используют FlatArray; Date — DateOnly, Type — DayType, Comment — nullable string. Отдельного поля Json нет; JSON входа обрабатывается будущим HTTP-адаптером.

StorageOption содержит только TableName=ProductionCalendar; адрес и таймаут настраиваются в блоке StorageApi через UseHttpApi. Release-сборка успешна; реальные обращения к Azure отложены до хоста и конфигурации доступа. Storage API не создаёт таблицу — это задача инфраструктуры.

CancellationToken передаётся в AsyncPipeline и асинхронные зависимости. Явные ThrowIfCancellationRequested удалены по замечанию пользователя от 2026-10-07; чистые синхронные стадии не принимают токен.

## Запуск и HTTP-контракт

Сборка: `dotnet build Internal.ProductionCalendar.slnx -c Release`.
Публикация: `dotnet publish src/app/AzureFunc/AzureFunc.csproj -c Release -o src/app/AzureFunc/bin/publish`.
Локальный запуск из src/app/AzureFunc: `func start` после настройки среды.

Локальный local.settings.json исключён из Git. Values должны содержать FUNCTIONS_WORKER_RUNTIME=dotnet-isolated, AzureWebJobsStorage (настройка host Storage), StorageApi__BaseAddress (URI Table endpoint). TableName по умолчанию ProductionCalendar; при необходимости задать ProductionCalendar__Storage__TableName с тем же значением. Для Storage pipeline требуется доступная Azure credential и роль Storage Table Data Contributor. Production composition использует credential для https://storage.azure.com/.default; shared key эмулятора не подставляется автоматически. Отключение AzureWebJobs.RefreshAzureTokens.Disabled=true допустимо только для локальных проверок, не требующих refresh. Реальные секреты и идентификаторы среды в документацию не вставлять.

- GET /api/production-calendar/{country}/{date}, date строго yyyy-MM-dd. Ответ 200: date, country, isWorkingDay, dayType (строка), comment. Ошибки 400/404/500.
- POST /api/production-calendar/initialize. Тело, например: {"country":"RU","year":2026,"days":[{"date":"2026-04-30","type":"ShortenedDay","comment":"Предпраздничный день"}]}. Ответ 200: country, year, daysCount. Ошибки 400/500.

Country/year/days обязательны. Days=[] и Days=null допустимы и создают базовый календарь; отсутствие поля запрещено. Date/type каждого элемента обязательны, type — точное имя одного из четырёх DayType, числа запрещены. JSON-модели находятся по отдельным файлам Inernal.Json и маппятся в endpoint-модели. Days в транспортной модели — required FlatArray без nullable. Отсутствие отклоняет required; штатный конвертер FlatArray преобразует JSON null в пустую коллекцию, как согласовано с пользователем.

Обе функции AuthorizationLevel.Function. Локальный Core Tools обычно не требует function key; в Azure требуется ключ. Ошибки имеют форму {"error":"..."}; сообщения Unknown обобщены, подробности Storage не публикуются в HTTP.
## CI/CD — инкремент 7

Добавлены .infra/main.bicep, modules/storage-access.bicep, apim/main.bicep и scripts/install-azure-resources.sh, set-function-app-settings.sh, prepare-apim.sh, artifact.sh. Пять workflows: build/install/publish/deploy/delete. Подробные переменные, права и порядок запусков — в [.infra/README.md](.infra/README.md) и [workflows/README.md](.github/workflows/README.md).

Существующее общее хранилище используется для версионных ZIP. Runtime function-packages создаётся отдельно в Storage Account Test/Prod как в образце. APIM только проверяется, методы не публикуются. Имена переменных сохранены из образца. Реальные запуск/настройки CI/CD и Azure deployment оставлены пользователю; файлы одобрены и закоммичены: 556f33d. Локально пройдены Bicep compile, Bash syntax, actionlint и linux-x64 publish.