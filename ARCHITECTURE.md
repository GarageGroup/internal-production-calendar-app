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
