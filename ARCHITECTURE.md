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

- Общие `DayType` и `DayTypeExtensions.IsWorkingDay()` находятся в `Shared.DayType`; `ProductionCalendarDay` принадлежит контракту календарного сервиса. `IsWorkingDay` — вычисляемое свойство только GetOut, без независимо задаваемого флага. Неиспользуемое свойство сервисной модели удалено по ревью 2026-10-07. Неизвестный enum вызывает ArgumentOutOfRangeException; вход и данные Storage должны проверяться до построения успешного результата.
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

С 2026-10-07 контракты построения и инициализации содержат Country, Year и Days. BuildIn использует required FlatArray<ProductionCalendarDayOverride> из ProductionCalendar.Contract, InitializeIn — required FlatArray<ProductionCalendarInitializeDay> из собственного endpoint Contract. Обе модели содержат required DateOnly Date, required DayType Type, nullable string Comment; handler явно маппит модели. Строковое поле Json удалено из обоих входов. Внутренние CalendarJson/CalendarDayJson удалены: сервис больше не зависит от JSON-десериализации.

BuildAsync использует AsyncPipeline: ValidateInput → ValidateOverrides → BuildCalendar. Страна нормализуется через OrEmpty/Trim/ToUpperInvariant; год проверяется на диапазон 1–9999. Все overrides валидируются до генерации: дата внутри года, определённый enum, отсутствие null-элементов и повторяющихся дат. Dictionary<DateOnly, ProductionCalendarDay> хранит уникальные overrides. Type напрямую маппится из enum в бизнес-модель.

Базовые дни заполняются по индексам от 1 января через FlatArray<ProductionCalendarDay>.Builder.OfLength и возвращаются посредством MoveToFlatArray; после 9999-12-31 нет шага AddDays за пределы DateOnly. Comment остаётся nullable, его нормализация для Storage относится к следующему слою. CancellationToken передаётся в AsyncPipeline; явных ThrowIfCancellationRequested нет по решению пользователя от 2026-10-07. Чистые синхронные стадии не принимают токен; цепочка использует группы методов без захватывающих лямбд.

HTTP-адаптер принимает единое JSON-тело country/year/days, проверяет обязательность полей, null коллекции, строковые даты и точные имена enum, затем передаёт типизированный InitializeIn. JSON-модели при необходимости размещать по одной в Inernal.Json соответствующего транспортного проекта. Предложенный маршрут инициализации обновлён на POST /api/production-calendar/initialize: Country/Year больше не дублируются в маршруте и теле. Маршрут реализован на этапе 6.

В Api нет HTTP Contract или JSON DTO; PrimeFuncPack.Primitives.Strings 3.0.0 используется для OrEmpty. FlatArray остаётся основным типом последовательностей; использованные возможности Builder/MoveToFlatArray сверены с исходниками установленной версии 1.5.1.

### Проверка проекта

По решению пользователя от 2026-10-07 тесты в проекте не пишутся. Тестовый проект, его исходники и ссылка из solution удалены; xUnit/VSTest больше не являются частью проекта. Проверки — restore/build, необходимый publish и ручная проверка поведения без написания тестового кода. История ранее выполненных тестов сохранена только в DEVELOPMENT_LOG.md и датированной записи плана.

## Storage API — инкремент 3

Добавлены StorageApi (основной partial-класс и Api.Day.Get.cs/Api.Day.Set.cs), StorageApiDependency, StorageOption и отдельная ProductionCalendarDayTableEntity в Internal.Table. Новые пакеты не требуются: IHttpApi, AsyncPipeline и Dependency используют зависимости каркаса.

StorageOption содержит только required TableName; фабрика Fold проверяет аргументы, StorageApi проверяет фиксированное имя ProductionCalendar. BaseAddress/Timeout принадлежат IHttpApi, подключаются через UseHttpApi("StorageApi"). Сервис строит относительный entity URL. Production BaseAddress — HTTPS endpoint таблиц с завершающим /.

GetDayAsync: AsyncPipeline → валидация страны/point URL → IHttpApi.SendAsync → проверка entity. PartitionKey — нормализованная страна и четырёхзначный год, RowKey — yyyyMMdd. Чтение выполняется через [адрес одной entity по обоим ключам](https://learn.microsoft.com/en-us/rest/api/storageservices/query-entities), без filter или сканирования. Код 404 маппится в NotFound, остальные HTTP failures — Unknown. Проверяются ожидаемые ключи, Date в формате yyyy-MM-dd и равенство запрошенной дате; DayType маппится switch только по четырём точным именам. JsonException и повреждённые значения возвращают Unknown; общий catch и ручные проверки отмены не применяются.

SetDayAsync: AsyncPipeline → валидация страны/enum → entity → [PUT Insert Or Replace](https://learn.microsoft.com/en-us/rest/api/storageservices/insert-or-replace-entity) → Unit. If-Match не отправляется. Имена PartitionKey, RowKey, Date, DayType и Comment закреплены через JsonPropertyName; Date и DayType — string. Nullable Comment преобразуется OrEmpty, поэтому отсутствие нового комментария очищает прежнее значение полной заменой. Ответ записи обрабатывается как OnlyStatusCode, штатный ответ Azure — 204.

Оба запроса используют FlatArray headers: x-ms-version 2019-02-02 по образцу, свежий x-ms-date в RFC1123, Accept application/json;odata=nometadata и [OData version headers 3.0](https://learn.microsoft.com/en-us/rest/api/storageservices/setting-the-odata-data-service-version-headers). Content-Type тела задаётся HttpBody.SerializeAsJson. Credential middleware подключается на этапе composition root; Bearer и выбранная версия сверены с [документацией авторизации](https://learn.microsoft.com/en-us/rest/api/storageservices/authorize-with-azure-active-directory).

Проверено: Release build и просмотр формирования запросов/маппинга. Тесты не создавались. Реальные GET/PUT в Azure не выполнялись; ручные проверки запланированы после хоста и настройки среды. Таблицу создаёт инфраструктура, Api не создаёт таблицы.

По замечанию ревью от 2026-10-07 entity объявляется через var внутри try; проверки и маппинг находятся там же. Catch JsonException сохраняет обработку ошибок десериализации, отдельная проверка entity is null сохраняет обработку JSON null. Предварительное nullable-объявление убрано.

## Handler получения дня — инкремент 4

ProductionCalendarDayGetHandler — internal sealed partial class с зависимостью от узкого IProductionCalendarDayStorageGetSupplier. Основной класс, Handler.Handle.cs и публичный ProductionCalendarDayGetHandlerDependency размещены по структуре CurrentExchangeRate.Get образца. Dependency.Map с generic supplier constraint создаёт handler; аргументы dependency и фабрики проверяются ThrowIfNull.

HandleAsync использует AsyncPipeline: BuildStorageGetIn (нормализация Country через OrEmpty/Trim/ToUpperInvariant и проверка двух ASCII-букв) → ForwardValue(storageApi.GetDayAsync, маппинг FailureCode) → MapSuccess(GetOut). Для OrEmpty добавлен прямой PrimeFuncPack.Primitives.Strings 3.0.0. Невалидная страна возвращает Invalid до вызова supplier; дата передаётся без изменения. Для валидного входа существует ровно один вызов чтения, без fallback или поиска предыдущего дня.

GetOut сохраняет Country, Date, DayType и nullable Comment из StorageGetOut. IsWorkingDay не задаётся отдельно: вычисляемое свойство контракта вызывает общий DayTypeExtensions, WorkingDay/ShortenedDay дают true, Weekend/Holiday — false. StorageFailureCode.Invalid/NotFound явно маппятся в соответствующие endpoint codes; остальные — Unknown, диагностический Failure сохраняется через MapFailureCode.

Токен передаётся AsyncPipeline и storage supplier; явных проверок отмены или catch нет. Проверены сборка и структура веток по коду, runtime-вызовы и HTTP-ответы пока не проверены: composition root и функции реализованы на этапе 6; успешные чтение/запись требуют интеграционной проверки. Тесты не создаются.
## Handler инициализации — инкремент 5 (2026-10-07)

ProductionCalendarInitializeHandler зависит от IProductionCalendarBuildSupplier и IProductionCalendarDayStorageSetSupplier. Публичная generic-фабрика Dependency<TCalendarApi, TStorageApi>.UseProductionCalendarInitializeHandler использует Fold и проверяет dependency и оба supplier, по аналогии с DailyExchangeRateUpdateHandlerDependency образца. Основной partial-класс и Handler.Handle.cs разделены; заполненная папка больше не содержит .gitkeep. Новые пакеты не требуются.

HandleAsync через BuildCalendarIn маппит InitializeIn и каждый ProductionCalendarInitializeDay в BuildIn/ProductionCalendarDayOverride, отклоняет null-элементы как Invalid, затем вызывает BuildAsync, переводит Invalid/Unknown и только в успешной ветке передаёт полный BuildOut в SaveCalendarAsync. Проверки country/year/дат/типов/дубликатов выполняются сервисом календаря до первого вызова SetDayAsync; handler доверяет успешной модели build supplier. JSON остаётся ответственностью будущего HTTP-адаптера.

SaveCalendarAsync использует установленный AsyncPipeline.Extensions 0.4.1: PipeParallelValue, DegreeOfParallelism=1, FailureAction=Stop. Образец использует тот же механизм с четырьмя параллельными операциями; здесь выбран один одновременный вызов для последовательной записи и прекращения при ошибке. SaveDayAsync маппит каждый день в StorageSetIn, включая nullable Comment, и передаёт токен в Storage. Записываются все 365/366 дней; replace-upsert очищает старые overrides и комментарии.

Все failures Storage маппятся в InitializeFailureCode.Unknown: исходная модель уже прошла валидацию, поэтому отказ записи является сбоем сохранения. Диагностическое сообщение включает Country|Year, Date и исходный FailureMessage; SourceException сохраняется. BuildFailureCode.Invalid остаётся Invalid с исходной диагностикой. Отмена не перехватывается, явных проверок отмены нет.

Ответ Country/Year/DaysCount формируется после успешной записи всей коллекции. Атомарность года и сериализация конкурирующих инициализаций не добавлены: при ошибке часть записей может уже измениться, повтор полного вызова восстанавливает календарь. Выполнены Release build и просмотр веток по исходникам; проверки с Azure отложены до развёртывания Test на этапе 7 и проверки на этапе 8. Тесты не создаются по требованию пользователя.

## Независимые контракты и Shared — замечание ревью 2026-10-07

По требованию пользователя DTO каждого слоя принадлежат своему Contract. Contract-проекты не ссылаются друг на друга; разрешён общий проект src/shared/DayType/DayType.csproj, содержащий только DayType и DayTypeExtensions, без зависимостей от приложения, endpoints и сервисов. Namespace сохранён GarageGroup.Internal.ProductionCalendar; AssemblyName — GarageGroup.Internal.ProductionCalendar.Shared.DayType. Solution содержит десять production-проектов.

Устранены ссылки на ProductionCalendar.Contract из Storage.Contract и обоих endpoint Contract. В ProductionCalendar.Initialize.Contract добавлена собственная ProductionCalendarInitializeDay; FlatArray подключён напрямую пакетом версии 1.5.1. BuildCalendarIn создаёт сервисные overrides через Builder.OfLength/MoveToFlatArray и проверяет null до чтения полей. В Get handler удалена лишняя прямая ссылка на контракт календарного сервиса. Зависимости реализаций handlers от supplier-контрактов остаются необходимыми для вызова сервисов; их публичные endpoint-контракты теперь независимы.

## Azure Functions и HTTP — инкремент 6 (2026-10-07)

Program использует FunctionHost.CreateFunctionsWorkerBuilderStandard().Build().RunAsync(), как в образце; OutputType переключён на Exe. Application/Application.cs собирает StandardSocketsHttpHandler → logging → TokenCredentialStandard → PollyStandard → HttpApi → StorageOption → StorageApi. URI и фиксированное имя таблицы берутся из ProductionCalendar:Storage. App.ProductionCalendar.Day.Get.cs и App.ProductionCalendar.Initialize.cs собирают generic handler dependencies; функции Resolve через InstanceServices. Business DI-регистрации не добавлены.

Assembly RefreshableTokenCredential с расписанием 0 */30 * * * * генерирует инфраструктурную RefreshAzureTokens. Timer extension сохранён именно для credential refresh; бизнес-таймеров календаря нет. Существующие Worker/GarageGroup packages используются без новых пакетов. host.json и appsettings.json содержат стандартный host и Info/ProductionCalendar:Storage; StorageApi:BaseAddress пустой до конфигурации среды, Timeout=01:00:00. local.settings.json исключён из Git.

Каждая JSON-модель находится в отдельном файле Inernal.Json: InitializeJson, InitializeDayJson, DayJson, InitializedJson, ErrorJson. Входная Days — required nonnullable FlatArray без собственного конвертера; JSON null эквивалентен [], отсутствие поля запрещено; endpoint Days также required nonnullable со своей моделью. JSON strings Date/Type маппятся строго в DateOnly/DayType, Year — обязательное JSON-число, string-number не принимается. Пустая Days разрешена. Нет зависимости транспортных DTO от DTO сервисов.

Чтение тела и создание HttpResponseData используют обычный async, как в образце; бизнес pipeline остаётся в handlers. JsonOptions закрепляет camelCase, строгие числа и строковый enum в ответе; input enum проверяется switch по точным именам, чтобы исключить нечувствительность стандартного enum converter к регистру. SerializeAsync пишет непосредственно в response.Body и сохраняет явно заданный HTTP status, Content-Type application/json; charset=utf-8. Unknown HTTP errors обобщены, исходная инфраструктурная диагностика остаётся в failure/HTTP logging. JsonException → 400, отмена не перехватывается.

Build/publish и generated metadata проверены. Core Tools 4.8.0 запустил host, оба HTTP route зарегистрированы; ручные негативные вызовы вернули 400. Credential refresh был локально отключён для этих вызовов. Успешные GET/PUT, 404 и refresh со Storage пока не проверены. Повторный локальный запуск с URI эмулятора отклонён автоматической проверкой команд; среда Storage остаётся задачей этапа 7 (инфраструктура и CI/CD); бизнес-проверка выполняется на этапе 8. Тестовый код не создаётся.

Ревью 2026-10-07: самописный GetRequiredValue удалён. ResolveStorageOption использует библиотечный IConfiguration.GetRequiredSection(key).Value.OrEmpty(); фиксированный TableName проверяет StorageApi; адрес/таймаут читает IHttpApi. GetRequiredValue для IConfiguration не доступен в подключённых библиотеках (проверено сборкой).

### Ревью 2026-10-07 — конфигурация IHttpApi

Проверена установленная GarageGroup.Infra.Http.Api 1.1.0: перегрузка UseHttpApi("StorageApi") доступна и компилируется; сборка содержит HttpApiOption/BaseAddress/Timeout и конфигурационное разрешение sectionName. Application использует эту перегрузку. Блок StorageApi содержит BaseAddress и Timeout=01:00:00. StorageOption больше не содержит ServiceUri; StorageApi формирует относительный ProductionCalendar(PartitionKey=...,RowKey=...) без начального /. Адрес больше не передаётся в сервис и не валидируется им.

BaseAddress должен заканчиваться / для корректного разрешения относительного URL, особенно при наличии path prefix. Azure settings: StorageApi__BaseAddress и StorageApi__Timeout; TableName остаётся ProductionCalendar__Storage__TableName. Реальные HTTP-вызовы Storage с новым base address пока не проверены; интеграция остаётся на этапе 8. Новые пакеты не добавлены.

Порядок после решения пользователя 2026-10-07: сначала инкремент 7 — инфраструктура/CI/CD и развёртывание Azure Test, затем инкремент 8 — ручная интеграционная проверка приложения в развёрнутой Test-среде. Тестовые проекты и автоматические тесты не добавляются. Текущие изменения разрешено закоммитить; следующий инкремент ожидает отдельной команды.

## Инфраструктура и CI/CD — инкремент 7 (2026-10-07)

Реализованы Bicep/templates/scripts/workflows по образцу. Linux Flex Consumption FC1, dotnet-isolated 10.0, North Europe, StorageV2 Standard_LRS, одна ProductionCalendar, Application Insights/Log Analytics и System Assigned MI. На Storage приложения назначены Blob Data Owner и Table Data Contributor как в storage-access образца. Нет Dataverse или бизнес-таймеров.

Пользователь уточнил: повторно используется общее хранилище версионных ZIP, а не общий runtime deployment container. Runtime function-packages создаётся отдельно в Storage Account каждой среды, как в образце. APIM_RESOURCE_GROUP/APIM_SERVICE_NAME подключают проверку существующего APIM без API/backend/policies/operations; APIM template лишь объявляет existing reference. Публикация endpoints в APIM отложена.

Имена repository/environment variables совпадают с образцом. .gitattributes закрепляет LF для shell/YAML/Bicep. Artifact script валидирует version/name, использует существующий Blob container и ключ только для CI, передаваемый secret. Приложение/host/deployment используют System Assigned MI. Upload overwrite=false защищает существующую версию; повторный deploy скачивает прежний ZIP. Settings едины через общий script, base URI читается из primaryEndpoints.table.

publish вызывает deploy как reusable workflow; ручной deploy использует тот же путь и не пересобирает ZIP. Install/deploy сериализуются concurrency по environment; publish/delete — по version. APIM job выполняется при skip_deploy, если deploy был success/skipped и workflow не отменён. Release delete удаляет только именованный artifact и tag приложения, без удаления данных или Azure-ресурсов.

Bicep compile, bash -n, actionlint и Release linux-x64 publish успешны. По прямому указанию пользователя подготовлены только файлы: GitHub configuration/runs и Azure deployments не выполнялись. Runtime OIDC/RBAC/OneDeploy будут проверены при запуске пользователем, бизнес-поведение — на этапе 8.

Основа проверена по [Functions Action](https://github.com/Azure/functions-action) и [Flex deployment storage](https://learn.microsoft.com/en-us/azure/azure-functions/flex-consumption-how-to#configure-deployment-settings). Проверка existing resource сама по себе не публикует API в APIM.
Ревью 2026-10-07: подготовка доступа описана shell-блоком в .infra/README.md по образцу. Для каждой среды отдельная Azure deployment App Registration/OIDC, без client secret и Graph permissions. Администратор заранее создаёт RG, principal получает Contributor и RBAC Administrator только на неё; APIM Reader для placeholder. Права System Assigned MI на Storage назначаются Bicep.
