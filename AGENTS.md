# Инструкции для разработки Production Calendar

## Объём текущей работы

Корневой `README.md` зарезервирован пользователем для другой информации. Не заполнять его инструкциями или описанием проекта без отдельного запроса. Обзор каркаса, дерево каталогов и навигация по документации находятся в `PROJECT_GUIDE.md`.

Сейчас создан только каркас. Реализовывать следующие этапы по запросу пользователя, ориентируясь на `IMPLEMENTATION_PLAN.md`. Не трактовать этот план как команду немедленно реализовать всё приложение. По завершении этапа обновлять его чекбокс и описывать результат проверки.

## Проект-образец

`C:/Users/Egor/Desktop/Работа/internal-exchange-rates-app` — источник структуры и стиля. Читать нужные аналоги перед реализацией нового слоя; не изменять проект-образец. Результаты исследования и конкретные файлы описаны в `ARCHITECTURE.md`.

## Структура и стиль

- Сохранять `src/app/AzureFunc`, `src/endpoint/<Operation>/Contract|Handler`, `src/service/<Service>/Contract|Api` и `.infra`, `.github/workflows`.
- Использовать .NET 10, отключённые implicit usings, nullable, warnings as errors, `IsPackable=false`, `InvariantGlobalization=false`. Не вводить `Directory.Build.props` и центральное управление пакетами: в образце настройки находятся в каждом `.csproj`.
- Общий file-scoped namespace: `GarageGroup.Internal.ProductionCalendar`. `AssemblyName` содержит слой и операцию.
- Явные `using`, отступ четыре пробела, фигурные скобки на отдельной строке. Повторять расположение переносов expression-bodied методов и цепочек из образца.
- Публичные контракты и DTO размещать в `Contract`; DTO обычно `sealed record class` с `required`/`init`. Реализации — `internal sealed partial class`.
- Основной класс и операции разделять: `StorageApi.cs` + `Api.Day.Get.cs`/`Api.Day.Set.cs`, `ProductionCalendarApi.cs` + `Api.Build.cs`, `<Operation>Handler.cs` + `Handler.Handle.cs`.
- Enum `DayType` живёт в `service/ProductionCalendar/Contract`. Azure Table entity живёт только в `Storage/Api/Internal.Table`; не отдавать её наружу.

## AsyncPipeline и ошибки

- Использовать `AsyncPipeline` везде, где он выражает последовательность валидации, маппинга, вызова зависимости и преобразования результата.
- Ориентиры: `AsyncPipeline.Pipe(input, cancellationToken)`, `Pipe`, `PipeValue`, `ForwardValue`, `Forward`, `Map`, `MapSuccess`, `MapFailure` в проекте-образце. Проверять сигнатуры в фактически установленном пакете.
- Контракты операций: `ValueTask<Result<TOut, Failure<TFailureCode>>>` и обязательный `CancellationToken`. Для операций без бизнес-ответа использовать `Unit`, если это соответствует согласованному контракту.
- Бизнес-валидация и ожидаемые ошибки возвращаются как `Failure`. Не превращать отмену в `Unknown`; пробрасывать её и передавать токен до HTTP-вызова. В ручном async-коде применять `ConfigureAwait(false)` по образцу.
- Чистые вычисления дат и перечисление 365/366 дней могут использовать обычные методы и циклы внутри стадии pipeline. Не писать искусственную асинхронность для чистых функций.
- В HTTP-адаптере допустим обычный async-код для чтения тела и создания ответа, как в образце. Бизнес-логику оставлять в сервисах и handlers.

## Dependency

- Композиция через `PrimeFuncPack.Dependency.Core`, а не новый слой DI-регистраций для каждого бизнес-компонента.
- Публичные extension-классы `<Name>Dependency`; методы `Use<Name>`. Один аргумент — `Map`, несколько — `Fold`; композиция — `Dependency.Pipe(...).With(...).Use...`.
- Проверять dependency и аргументы фабрики через `ArgumentNullException.ThrowIfNull`. Handler зависит от узкого supplier-интерфейса, а не конкретной реализации.
- Composition root — `app/AzureFunc/Application`, операции — отдельные `App.*.cs`. Разрешение dependency — через `InstanceServices` контекста функции.
- Storage: стандартный HTTP handler → logging → credential для `https://storage.azure.com/.default` → Polly → `IHttpApi` → `StorageOption` → `UseStorageApi`.

## Данные и проверка

- Единственная бизнес-таблица `ProductionCalendar`. Ключи `Country|Year` и `yyyyMMdd`, бизнес-поле `Date` — строка `yyyy-MM-dd`; `DayType` в Storage — строка, в коде — enum.
- Инициализация полностью пересоздаёт модель года, применяет overrides, затем выполняет replace-upsert каждого дня. Не обновлять только overrides: это оставляет старые исключения.
- Проверять весь JSON до первой записи. Не подменять отсутствующий день вычисленным ответом при чтении.
- Не добавлять секреты или `local.settings.json` в репозиторий. Не копировать инфраструктурные идентификаторы и значения среды образца.
- Проверять сборку и содержательные тесты текущего этапа. Не считать вызов `dotnet test` без тестовых проектов проверкой поведения.
- CI/CD реализовать после приложения по `.infra/README.md`. Azure deployment и настройки реальных GitHub environments — отдельный этап с предоставленными параметрами среды.
