# План реализации Production Calendar

Выполнять последовательно. Каждый этап заканчивается проверкой и обновлением статуса. План предназначен для дальнейших совместных итераций; текущий запрос ограничен этапом 0.

## Этап 0. Исследование, каркас и инструкции

- [x] Изучить solution, исходники, dependencies, Storage REST, Functions, workflows и Bicep образца.
- [x] Создать девять пустых проектов с соответствующим деревом и ссылками.
- [x] Описать спецификацию, стиль, архитектуру и дальнейшие этапы.
- [x] Выполнить restore/build Release и записать результат в конце документа.

## Этап 1. Контракты и бизнес-модель

- [ ] `service/ProductionCalendar/Contract/DayType.cs`: четыре значения enum из спецификации.
- [ ] Модель `ProductionCalendarDay`: Country, DateOnly Date, DayType, Comment; единое чистое правило IsWorkingDay.
- [ ] `ProductionCalendar.Build/`: input построения Country/Year/JSON, output полного календаря, typed failure codes, supplier `IProductionCalendarBuildSupplier`.
- [ ] `IProductionCalendarApi` объединяет supplier построения.
- [ ] `Storage/Contract/ProductionCalendarDay.Get|Set`: inputs/outputs, supplier-интерфейсы, `IStorageApi`, `StorageFailureCode` (Invalid/NotFound/Unknown).
- [ ] `endpoint/ProductionCalendarDay.Get/Contract`: GetIn/GetOut, failure codes, `IProductionCalendarDayGetHandler`.
- [ ] `endpoint/ProductionCalendar.Initialize/Contract`: InitializeIn с Country/Year/JSON, InitializeOut с Country/Year/DaysCount, failure codes, `IProductionCalendarInitializeHandler`.
- [ ] Возвращать `ValueTask<Result<..., Failure<...>>>`, принимать CancellationToken; исключить зависимости контрактов от HTTP/Storage реализации.

Готовность: solution собирается; enum общий для всех слоёв; контракты покрывают оба действия и все обязательные поля. Перед кодом уточнить имена типов по единому стилю образца, затем закрепить в архитектуре.

## Этап 2. Чистое построение календаря

- [ ] `ProductionCalendarApi/ProductionCalendarApi.cs`, `Api.Build.cs` и `ProductionCalendarApiDependency.cs`.
- [ ] JSON DTO внутри реализации, явный маппинг строкового type и строгий разбор Date.
- [ ] Pipeline: вход → нормализация/валидация → чтение JSON → валидация всех overrides → базовый календарь → применение overrides → Result.
- [ ] Реализовать правила ошибок из SPECIFICATION; malformed JSON становится Invalid; отмена не становится Unknown.
- [ ] Генерировать все дни года, в том числе 29 февраля; корректно обрабатывать годы 1 и 9999.
- [ ] Независимость от Azure и HTTP, определённый порядок дней по дате.
- [ ] Добавить содержательные тесты генерации и валидации. Тестовые сборки разместить под `test/service/ProductionCalendar/Api`, повторяя разбиение src; в образце тестовых проектов нет, поэтому выбрать и закрепить тестовый стек на этом этапе.

Готовность: 365/366 уникальных дат; weekdays/weekends верны; рабочая суббота, перенесённый выходной, Holiday и ShortenedDay заменяют базу; пустой массив создаёт базовый год; все невалидные варианты покрыты.

## Этап 3. Azure Table Storage API

- [ ] `Option/StorageOption.cs`: ServiceUri и TableName `ProductionCalendar`.
- [ ] `Internal.Table/ProductionCalendarDayTableEntity.cs`: string PartitionKey, RowKey, Date, DayType, Comment; явные JSON имена, соответствующие Storage.
- [ ] `StorageApi/StorageApi.cs`: константы REST, headers, форматирование ключей и URL, общие проверки.
- [ ] `Api.Day.Get.cs`: AsyncPipeline → validation/point URL → IHttpApi.SendAsync → entity decode/validation → business output.
- [ ] `Api.Day.Set.cs`: AsyncPipeline → validation/entity mapping → PUT insert-or-replace → Unit; тело содержит все бизнес-поля, включая пустой Comment.
- [ ] `StorageApiDependency.cs`: `Dependency<IHttpApi, StorageOption>.Fold<IStorageApi>` с проверками фабрики, как в образце.
- [ ] Передавать CancellationToken, маппить 404 чтения в NotFound; сбои/повреждённые данные в Unknown.
- [ ] Проверить фактическую семантику Table REST insert-or-replace, headers и кодов ответа по официальной документации при реализации.
- [ ] Тесты через fake IHttpApi: ровно point lookup; правильные ключи, строки enum/date и полный PUT; очистка комментария; неверный вход до HTTP; 404; malformed entity; отмена.

Готовность: Storage не сканируется; данные в JSON видны как строковые даты и типы; replace-upsert поддерживает повторные записи. Вызовы проверены тестами без реального Azure.

## Этап 4. Получение дня

- [ ] `Handler/ProductionCalendarDayGetHandler.cs` и `Handler/Handler.Handle.cs`.
- [ ] Dependency extension с generic supplier constraint по примеру `CurrentExchangeRateGetHandlerDependency`.
- [ ] Pipeline: validation → StorageGetIn → storage supplier → GetOut и маппинг FailureCode.
- [ ] IsWorkingDay вычисляется только по DayType; Country/Date/Comment входят в ответ.
- [ ] Тесты четырёх DayType, нормализации страны, NotFound/Invalid/Unknown и отмены.

Готовность: единственный вызов storage на точную дату, включая выходной; нет fallback на предыдущий день или вычисления дня без записи.

## Этап 5. Инициализация года

- [ ] `Handler/ProductionCalendarInitializeHandler.cs`, `Handler.Handle.cs` и `ProductionCalendarInitializeHandlerDependency.cs`.
- [ ] Handler принимает build supplier и storage set supplier; композиция через `Dependency<...,...>.Fold`.
- [ ] AsyncPipeline: вход → построение/полная валидация → сохранение всех дней → итоговый ответ.
- [ ] Применить Extensions для последовательности записей, если сигнатуры пакета позволяют; иначе изолировать последовательный async-цикл в одной стадии с передачей токена.
- [ ] При первом сбое записи вернуть Failure с контекстом страны/года/даты, без ложного успеха.
- [ ] Сохранять весь год при каждом вызове, включая обычные дни, пустые комментарии и удалённые overrides.
- [ ] Тесты: число вызовов 365/366, все ключи уникальны, новый JSON заменяет прошлые значения, повтор идентичного входа не создаёт новых ключей, другой год/страна не затронуты, invalid JSON даёт ноль записей, сбой/отмена прекращает обработку.

Готовность: повторная инициализация удовлетворяет полной replace-семантике; модель частичных сбоев явно документирована.

## Этап 6. Azure Functions и composition root

- [ ] Переключить AzureFunc `OutputType` на Exe и добавить `Program.cs` с `FunctionHost.CreateFunctionsWorkerBuilderStandard().Build().RunAsync()`.
- [ ] `Application/Application.cs`: UseStorageApi, UseProductionCalendarApi, ResolveStorageOption; конфигурация из IConfiguration.
- [ ] `App.ProductionCalendar.Day.Get.cs` и `App.ProductionCalendar.Initialize.cs`: сборка handler dependencies.
- [ ] HTTP pipeline Storage: StandardSocketsHttpHandler → logging → TokenCredentialStandard → PollyStandard → HttpApi → option → StorageApi.
- [ ] Настройка обновления credential по образцу; проверить необходимость timer extension для инфраструктурного credential refresh, не добавлять бизнес-таймеры календаря.
- [ ] `Function/Function.cs`, `Function.ProductionCalendar.Day.Get.cs`, `Function.ProductionCalendar.Initialize.cs`.
- [ ] Строго разбирать route date/year, читать JSON, передавать токен, маппить Result в HTTP; не выполнять бизнес-генерацию в Function.
- [ ] `host.json`, `appsettings.json` с Info и ProductionCalendar:Storage; документировать локальные настройки без хранения секретов.
- [ ] Закрепить camelCase и строковую JSON-сериализацию DayType с запретом числового input.
- [ ] Проверить необходимый набор Worker/GarageGroup пакетов, удалить ненужные прямые зависимости образца.

Готовность: host запускается локально; оба маршрута работают; ошибки имеют согласованные коды; `dotnet publish` создаёт пакет Functions с metadata и host.json.

## Этап 7. Интеграционная проверка

- [ ] Локальный совместимый Table Storage emulator или отдельная Test-среда; настройку авторизации для emulator изолировать от production composition.
- [ ] Инициализировать невисокосный и високосный годы, прочитать обычный рабочий день, Weekend, Holiday, рабочую субботу и ShortenedDay.
- [ ] Повторить с изменённым и пустым JSON, проверить восстановление базового типа и очистку Comment.
- [ ] Проверить изоляцию страны/года и отсутствие дубликатов; отрицательные HTTP сценарии.
- [ ] Проверить прямое представление entity: Date и DayType строковые и читаемые.
- [ ] Restore/build/test/publish Release; обновить PROJECT_GUIDE.md с рабочими командами и примерами. Корневой README.md зарезервирован пользователем для другой информации.

Готовность: тесты доказывают поведение и идемпотентность, а не только успешную сборку. Праздники в fixtures — явно тестовые примеры.

## Этап 8. Инфраструктура и CI/CD

- [ ] Выполнить подробный план `.infra/README.md` и `.github/workflows/README.md`.
- [ ] Адаптировать Bicep и scripts, создать одну таблицу ProductionCalendar; сохранить Flex Consumption, Managed Identity, RBAC, Test/Prod, OIDC и ZIP lifecycle.
- [ ] Создать build/publish/deploy/install/delete workflows по образцу; календарные settings вместо курсов/таймеров, без ненужной Dataverse-конфигурации.
- [ ] Проверить Bicep build, `bash -n`, restore/build/test/publish и схему workflows.
- [ ] Настроить предоставленные параметры GitHub/Azure, установить Test, выпустить версию и выполнить smoke test обоих действий.
- [ ] Проверить развёртывание выбранного существующего ZIP в Prod и сценарий возврата на предыдущую версию по согласованным параметрам среды.

Готовность: одинаковый артефакт продвигается между средами; credentials приложения — Managed Identity; CI действительно запускает содержательные тесты; инструкция развёртывания воспроизводима.

## Результат проверки каркаса

Проверено 6 октября 2026 года, SDK .NET 10.0.401:

- `dotnet restore Internal.ProductionCalendar.slnx` — успешно для всех девяти проектов.
- `dotnet build Internal.ProductionCalendar.slnx --no-restore -c Release` — успешно, 0 предупреждений, 0 ошибок.
- Проверены ProjectReference и отсутствие оставшихся имён ExchangeRates в `.csproj`.

Бизнес-логика и тесты поведения пока отсутствуют. `dotnet test` и запуск хоста на этом этапе не выполнялись. Следующий этап — 1, контракты и бизнес-модель.
