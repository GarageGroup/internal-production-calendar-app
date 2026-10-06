# Архитектура и исследование проекта-образца

## Что изучено

Образец: `C:/Users/Egor/Desktop/Работа/internal-exchange-rates-app`.
Исследование выполнено по локальным исходникам 6 октября 2026 года.

| Область | Файлы образца | Наблюдаемый подход |
| --- | --- | --- |
| Solution | `Internal.ExchangeRates.slnx`, все `.csproj` | Папки solution `service`, `endpoint`, `app`; отдельные сборки контракта и реализации; .NET 10 |
| Бизнес-сервис | `service/ExchangeRate/Api/ExchangeRateApi/Api.*.Get.cs` | AsyncPipeline, Result/Failure, отдельные partial-файлы операций |
| Storage | `service/Storage/Api/StorageApi/Api.Daily.Get.cs`, `Api.Daily.Set.cs`, `StorageApi.cs` | Table REST через `IHttpApi`, адрес entity по двум ключам, HTTP PUT для записи |
| Entity | `service/Storage/Api/Internal.Table/DailyExchangeRateTableEntity.cs` | Отдельная JSON-модель Azure Table и явный маппинг бизнес-полей |
| Dependency | `StorageApiDependency.cs`, `ExchangeRateApiDependency.cs`, `*HandlerDependency.cs` | Extension Use*, Map/Fold, узкие suppliers, проверки аргументов фабрики |
| Handler | `endpoint/CurrentExchangeRate.Get/Handler/Handler/Handler.Handle.cs` | Pipeline преобразует вход, вызывает storage и маппит обе ветки Result |
| Инициализация/обновление | `endpoint/DailyExchangeRate.Update/Handler` | Несколько зависимостей через Dependency; пакет AsyncPipeline.Extensions |
| Composition root | `app/AzureFunc/Application/Application.cs`, `App.*.cs` | HTTP middleware, token credential, Polly, настройка options из IConfiguration |
| Functions | `Program.cs`, `Function/Function.*.cs` | Isolated Worker, FunctionHost, Resolve из InstanceServices, тонкий HTTP-адаптер |
| CI/CD | `.github/workflows/{build,publish,deploy,install,delete}.yml` | Проверка кода/infra, ZIP в Blob, Test/Prod, Azure OIDC, release lifecycle |
| Infra | `.infra/main.bicep`, `modules/storage-access.bicep`, `apim/main.bicep`, `scripts/*.sh` | Flex Consumption, North Europe, Managed Identity, RBAC, существующий APIM placeholder |

В образце есть исключения из pipeline-стиля: поиск исторического курса циклом и HTTP-ответы через обычный async-код. Для календаря сохраняем предпочтение AsyncPipeline, не переносим логику поиска ближайшего предыдущего дня.

## Сопоставление слоёв

| Образец | Production Calendar | Ответственность |
| --- | --- | --- |
| `service/ExchangeRate` | `service/ProductionCalendar` | Чистое построение полного календаря и бизнес-модель |
| `service/Storage` | `service/Storage` | Получение/replace-upsert дней через Table REST |
| `endpoint/CurrentExchangeRate.Get` | `endpoint/ProductionCalendarDay.Get` | Получение ровно запрошенной даты и IsWorkingDay |
| `endpoint/DailyExchangeRate.Update` | `endpoint/ProductionCalendar.Initialize` | Валидация модели, построение года, сохранение всех дней |
| `app/AzureFunc` | `app/AzureFunc` | HTTP transport, конфигурация, composition root |

Для двух действий нужны девять проектов вместо тринадцати в образце. Внешний поставщик данных не требуется: сервис календаря получает overrides из входа.

## Граф зависимостей

```text
ProductionCalendar.Contract
  <- ProductionCalendar.Api
  <- Storage.Contract <- Storage.Api
  <- ProductionCalendarDay.Get.Contract
  <- ProductionCalendar.Initialize.Contract

ProductionCalendarDay.Get.Handler
  -> собственный Contract, Storage.Contract, ProductionCalendar.Contract
ProductionCalendar.Initialize.Handler
  -> собственный Contract, ProductionCalendar.Contract, Storage.Contract
AzureFunc
  -> оба Handler, ProductionCalendar.Api, Storage.Api
```

Контракты не ссылаются на реализации, сервис календаря не зависит от Azure, storage не зависит от endpoint. Общий enum объявляется один раз в `ProductionCalendar.Contract`.

## Будущие компоненты

- `ProductionCalendarApi`: `IProductionCalendarApi` объединяет supplier построения календаря. Чистая генерация, без HTTP и Storage.
- `StorageApi`: `IStorageApi` объединяет `IProductionCalendarDayStorageGetSupplier` и `IProductionCalendarDayStorageSetSupplier`.
- `ProductionCalendarDayGetHandler`: получает один день и маппит его в бизнес-ответ.
- `ProductionCalendarInitializeHandler`: получает календарь от build supplier, сохраняет каждую запись через set supplier, возвращает успешный итог только после всех записей.
- `Application`: композиция конкретных реализаций. Внедрение сервиса календаря через `Dependency.From(...)`/фабрику по фактическому API библиотеки; остальные компоненты повторяют Map/Fold из образца.

## Контракты первого инкремента

- Общие `DayType`, `DayTypeExtensions.IsWorkingDay()` и `ProductionCalendarDay` находятся в `ProductionCalendar.Contract`. `IsWorkingDay` — вычисляемое свойство модели и GetOut, без независимо задаваемого флага. Неизвестный enum вызывает ArgumentOutOfRangeException; вход и данные Storage должны проверяться до построения успешного результата.
- Построение: `ProductionCalendarBuildIn` (Country, Year, Days), `ProductionCalendarBuildOut` (Country, Year, FlatArray дней), `IProductionCalendarBuildSupplier.BuildAsync` и `IProductionCalendarApi`.
- Storage: `ProductionCalendarDayStorageGetIn/GetOut`, `ProductionCalendarDayStorageSetIn`, `GetDayAsync` и `SetDayAsync`. Запись возвращает Unit; ключи и Table entity скрыты в будущей реализации.
- Endpoints: `ProductionCalendarDayGetIn/GetOut` и `ProductionCalendarInitializeIn/Out`, отдельные интерфейсы handlers с HandleAsync.
- Входы чтения/построения/инициализации — readonly record struct, модели и outputs — sealed record class, как в образце. Comment необязателен и объявлен `string?` без инициализатора; Country и Days обязательны при конструировании; runtime-валидация страны, года и overrides реализована в сервисе построения. При записи строки в Storage использовать Comment.OrEmpty(), включая очистку старого комментария.
- Коды ошибок: Unknown/Invalid для построения и инициализации, дополнительно NotFound для Storage и получения дня. Все операции принимают CancellationToken и возвращают ValueTask<Result<..., Failure<...>>>.
- FlatArray 1.5.1 подключён напрямую, версия совпадает с зависимостью update-handler образца. Контракт инициализации использует Failure/Result напрямую вместо прежнего пакета Handler.Core для Unit timer-handler.

## Пакеты каркаса

Версии взяты из образца, а не выбраны как новейшие: `EarlyFuncPack.Core.AsyncPipeline 0.3.0`, `GarageGroup.Core.AsyncPipeline.Extensions 0.4.1`, `PrimeFuncPack.Dependency.Core 2.2.0`, `PrimeFuncPack.Core.Failure 2.2.0`, `PrimeFuncPack.Core.Result 2.0.2`, GarageGroup HTTP/hosting и Microsoft Worker в тех же версиях.

Инициализация использует пакет Extensions, как update-handler образца; Storage и остальные pipeline-компоненты — Core. У чистого сервиса календаря удалена прямая зависимость от HTTP Contract. Worker/hosting-пакеты заранее присутствуют в проекте хоста; вспомогательные пакеты образца уточнить при подключении функций, не вводить другой стек без причины.

## Реализация построения календаря — инкремент 2

`ProductionCalendarApi` не содержит состояния отдельных запросов и не использует Azure/HTTP. Публичный entry point композиции — `ProductionCalendarApiDependency.UseProductionCalendarApi()`, возвращающий `Dependency<IProductionCalendarApi>` через `Dependency.From` с фабрикой без зависимостей.

С 2026-10-07 контракт построения и инициализации содержит Country, Year и Days: required FlatArray<ProductionCalendarDayOverride>. Общая модель override объявлена в ProductionCalendar.Contract: required DateOnly Date, required DayType Type, nullable string Comment. Строковое поле Json удалено из обоих входов. Внутренние CalendarJson/CalendarDayJson удалены: сервис больше не зависит от JSON-десериализации.

BuildAsync использует AsyncPipeline: ValidateInput → ValidateOverrides → BuildCalendar. Страна нормализуется через OrEmpty/Trim/ToUpperInvariant; год проверяется на диапазон 1–9999. Все overrides валидируются до генерации: дата внутри года, определённый enum, отсутствие null-элементов и повторяющихся дат. Dictionary<DateOnly, ProductionCalendarDay> хранит уникальные overrides. Type напрямую маппится из enum в бизнес-модель.

Базовые дни заполняются по индексам от 1 января через FlatArray<ProductionCalendarDay>.Builder.OfLength и возвращаются посредством MoveToFlatArray; после 9999-12-31 нет шага AddDays за пределы DateOnly. Comment остаётся nullable, его нормализация для Storage относится к следующему слою. CancellationToken передаётся в AsyncPipeline; явных ThrowIfCancellationRequested нет по решению пользователя от 2026-10-07. Чистые синхронные стадии не принимают токен; цепочка использует группы методов без захватывающих лямбд.

Будущий HTTP-адаптер принимает единое JSON-тело country/year/days, проверяет обязательность полей, null коллекции, строковые даты и точные имена enum, затем передаёт типизированный InitializeIn. JSON-модели при необходимости размещать по одной в Inernal.Json соответствующего транспортного проекта. Предложенный маршрут инициализации обновлён на POST /api/production-calendar/initialize: Country/Year больше не дублируются в маршруте и теле. Это предварительный маршрут, функции пока не реализованы.

В Api нет HTTP Contract или JSON DTO; PrimeFuncPack.Primitives.Strings 3.0.0 используется для OrEmpty. FlatArray остаётся основным типом последовательностей; использованные возможности Builder/MoveToFlatArray сверены с исходниками установленной версии 1.5.1.

### Проверка проекта

По решению пользователя от 2026-10-07 тесты в проекте не пишутся. Тестовый проект, его исходники и ссылка из solution удалены; xUnit/VSTest больше не являются частью проекта. Проверки — restore/build, необходимый publish и ручная проверка поведения без написания тестового кода. История ранее выполненных тестов сохранена только в DEVELOPMENT_LOG.md и датированной записи плана.

## Storage API — инкремент 3

Добавлены StorageApi (основной partial-класс и Api.Day.Get.cs/Api.Day.Set.cs), StorageApiDependency, StorageOption и отдельная ProductionCalendarDayTableEntity в Internal.Table. Новые пакеты не требуются: IHttpApi, AsyncPipeline и Dependency используют зависимости каркаса.

StorageOption содержит required ServiceUri и TableName; фабрика Dependency<IHttpApi, StorageOption>.Fold проверяет аргументы. При создании Api проверяются абсолютный HTTP/HTTPS URI без query/fragment и фиксированное имя ProductionCalendar. Ошибки конфигурации — ArgumentException при композиции; ошибки бизнес-входа — StorageFailureCode.Invalid до HTTP. HTTP разрешён для будущего локального emulator, production-настройки используют HTTPS.

GetDayAsync: AsyncPipeline → валидация страны/point URL → IHttpApi.SendAsync → проверка entity. PartitionKey — нормализованная страна и четырёхзначный год, RowKey — yyyyMMdd. Чтение выполняется через [адрес одной entity по обоим ключам](https://learn.microsoft.com/en-us/rest/api/storageservices/query-entities), без filter или сканирования. Код 404 маппится в NotFound, остальные HTTP failures — Unknown. Проверяются ожидаемые ключи, Date в формате yyyy-MM-dd и равенство запрошенной дате; DayType маппится switch только по четырём точным именам. JsonException и повреждённые значения возвращают Unknown; общий catch и ручные проверки отмены не применяются.

SetDayAsync: AsyncPipeline → валидация страны/enum → entity → [PUT Insert Or Replace](https://learn.microsoft.com/en-us/rest/api/storageservices/insert-or-replace-entity) → Unit. If-Match не отправляется. Имена PartitionKey, RowKey, Date, DayType и Comment закреплены через JsonPropertyName; Date и DayType — string. Nullable Comment преобразуется OrEmpty, поэтому отсутствие нового комментария очищает прежнее значение полной заменой. Ответ записи обрабатывается как OnlyStatusCode, штатный ответ Azure — 204.

Оба запроса используют FlatArray headers: x-ms-version 2019-02-02 по образцу, свежий x-ms-date в RFC1123, Accept application/json;odata=nometadata и [OData version headers 3.0](https://learn.microsoft.com/en-us/rest/api/storageservices/setting-the-odata-data-service-version-headers). Content-Type тела задаётся HttpBody.SerializeAsJson. Credential middleware подключается на этапе composition root; Bearer и выбранная версия сверены с [документацией авторизации](https://learn.microsoft.com/en-us/rest/api/storageservices/authorize-with-azure-active-directory).

Проверено: Release build и просмотр формирования запросов/маппинга. Тесты не создавались. Реальные GET/PUT в Azure не выполнялись; ручные проверки запланированы после хоста и настройки среды. Таблицу создаёт инфраструктура, Api не создаёт таблицы.

По замечанию ревью от 2026-10-07 entity объявляется через var внутри try; проверки и маппинг находятся там же. Catch JsonException сохраняет обработку ошибок десериализации, отдельная проверка entity is null сохраняет обработку JSON null. Предварительное nullable-объявление убрано.

## Handler получения дня — инкремент 4

ProductionCalendarDayGetHandler — internal sealed partial class с зависимостью от узкого IProductionCalendarDayStorageGetSupplier. Основной класс, Handler.Handle.cs и публичный ProductionCalendarDayGetHandlerDependency размещены по структуре CurrentExchangeRate.Get образца. Dependency.Map с generic supplier constraint создаёт handler; аргументы dependency и фабрики проверяются ThrowIfNull.

HandleAsync использует AsyncPipeline: BuildStorageGetIn (нормализация Country через OrEmpty/Trim/ToUpperInvariant и проверка двух ASCII-букв) → ForwardValue(storageApi.GetDayAsync, маппинг FailureCode) → MapSuccess(GetOut). Для OrEmpty добавлен прямой PrimeFuncPack.Primitives.Strings 3.0.0. Невалидная страна возвращает Invalid до вызова supplier; дата передаётся без изменения. Для валидного входа существует ровно один вызов чтения, без fallback или поиска предыдущего дня.

GetOut сохраняет Country, Date, DayType и nullable Comment из StorageGetOut. IsWorkingDay не задаётся отдельно: вычисляемое свойство контракта вызывает общий DayTypeExtensions, WorkingDay/ShortenedDay дают true, Weekend/Holiday — false. StorageFailureCode.Invalid/NotFound явно маппятся в соответствующие endpoint codes; остальные — Unknown, диагностический Failure сохраняется через MapFailureCode.

Токен передаётся AsyncPipeline и storage supplier; явных проверок отмены или catch нет. Проверены сборка и структура веток по коду, runtime-вызовы и HTTP-ответы пока не проверены: composition root и функции реализуются на этапе 6. Тесты не создаются.
