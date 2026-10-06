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
| `endpoint/DailyExchangeRate.Update` | `endpoint/ProductionCalendar.Initialize` | Валидация JSON, построение года, сохранение всех дней |
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
- Построение: `ProductionCalendarBuildIn` (Country, Year, Json), `ProductionCalendarBuildOut` (Country, Year, FlatArray дней), `IProductionCalendarBuildSupplier.BuildAsync` и `IProductionCalendarApi`.
- Storage: `ProductionCalendarDayStorageGetIn/GetOut`, `ProductionCalendarDayStorageSetIn`, `GetDayAsync` и `SetDayAsync`. Запись возвращает Unit; ключи и Table entity скрыты в будущей реализации.
- Endpoints: `ProductionCalendarDayGetIn/GetOut` и `ProductionCalendarInitializeIn/Out`, отдельные интерфейсы handlers с HandleAsync.
- Входы чтения/построения/инициализации — readonly record struct, модели и outputs — sealed record class, как в образце. Comment необязателен и объявлен `string?` без инициализатора; Country и Json обязательны при конструировании, runtime-валидация появится в следующих этапах. При записи строки в Storage использовать Comment.OrEmpty(), включая очистку старого комментария.
- Коды ошибок: Unknown/Invalid для построения и инициализации, дополнительно NotFound для Storage и получения дня. Все операции принимают CancellationToken и возвращают ValueTask<Result<..., Failure<...>>>.
- FlatArray 1.5.1 подключён напрямую, версия совпадает с зависимостью update-handler образца. Контракт инициализации использует Failure/Result напрямую вместо прежнего пакета Handler.Core для Unit timer-handler.

## Пакеты каркаса

Версии взяты из образца, а не выбраны как новейшие: `EarlyFuncPack.Core.AsyncPipeline 0.3.0`, `GarageGroup.Core.AsyncPipeline.Extensions 0.4.1`, `PrimeFuncPack.Dependency.Core 2.2.0`, `PrimeFuncPack.Core.Failure 2.2.0`, `PrimeFuncPack.Core.Result 2.0.2`, GarageGroup HTTP/hosting и Microsoft Worker в тех же версиях.

Инициализация использует пакет Extensions, как update-handler образца; Storage и остальные pipeline-компоненты — Core. У чистого сервиса календаря удалена прямая зависимость от HTTP Contract. Worker/hosting-пакеты заранее присутствуют в проекте хоста; вспомогательные пакеты образца уточнить при подключении функций, не вводить другой стек без причины.
