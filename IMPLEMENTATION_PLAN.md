# План реализации Production Calendar

Выполнять последовательно, по одному инкременту. Каждый этап заканчивается проверкой, обновлением статуса и остановкой для ревью без коммита. После одобрения пользователя или «продолжай» закоммитить завершённый инкремент и перейти к следующему.

По решению пользователя от 2026-10-07 тесты для этого проекта не пишутся. Ранее созданный тестовый проект удалён; автоматические тесты и dotnet test исключены из всех будущих этапов и CI/CD. Проверки поведения ниже выполняются вручную без создания тестового кода.

Для каждого инкремента актуализировать связанные Markdown-документы и вести [журнал](DEVELOPMENT_LOG.md): дата, выполненное, решения и их источник, проверки, ревью и коммит. Чекбокс выполнения означает готовность реализации к ревью, а не одобрение пользователем.

## Этап 0. Исследование, каркас и инструкции

- [x] Изучить solution, исходники, dependencies, Storage REST, Functions, workflows и Bicep образца.
- [x] Создать девять пустых проектов с соответствующим деревом и ссылками.
- [x] Описать спецификацию, стиль, архитектуру и дальнейшие этапы.
- [x] Выполнить restore/build Release и записать результат в конце документа.

## Этап 1. Контракты и бизнес-модель

- [x] `shared/DayType/DayType.cs` и `DayTypeExtensions.cs`: общий независимый проект для enum и правила IsWorkingDay (перенос по ревью 2026-10-07).
- [x] Модель `ProductionCalendarDay`: Country, DateOnly Date, DayType, Comment. Правило IsWorkingDay находится в Shared и используется GetOut; неиспользуемое свойство сервисной модели удалено по ревью 2026-10-07.
- [x] Замечание ревью 2026-10-06: Comment объявлен nullable без инициализатора во всех четырёх моделях; правила required/nullable, OrEmpty, запрета null-forgiving и предпочтения is/is not закреплены в AGENTS.md.
- [x] `ProductionCalendar.Build/`: input построения Country/Year/Days, output полного календаря, typed failure codes, supplier `IProductionCalendarBuildSupplier`.
- [x] `IProductionCalendarApi` объединяет supplier построения.
- [x] `Storage/Contract/ProductionCalendarDay.Get|Set`: inputs/outputs, supplier-интерфейсы, `IStorageApi`, `StorageFailureCode` (Invalid/NotFound/Unknown).
- [x] `endpoint/ProductionCalendarDay.Get/Contract`: GetIn/GetOut, failure codes, `IProductionCalendarDayGetHandler`.
- [x] `endpoint/ProductionCalendar.Initialize/Contract`: InitializeIn с Country/Year/Days, InitializeOut с Country/Year/DaysCount, failure codes, `IProductionCalendarInitializeHandler`.
- [x] Возвращать `ValueTask<Result<..., Failure<...>>>`, принимать CancellationToken; исключить зависимости контрактов от HTTP/Storage реализации.

Готовность: solution собирается; enum общий для всех слоёв; контракты покрывают оба действия и все обязательные поля. Перед кодом уточнить имена типов по единому стилю образца, затем закрепить в архитектуре.

Проверка инкремента 1 (2026-10-06): restore и Release build всего решения прошли успешно, 0 предупреждений и 0 ошибок. Реализации операций добавляются следующими этапами; написание тестов впоследствии исключено пользователем. Инкремент подготовлен для ревью, коммит не выполнен. Подробности и решения — в DEVELOPMENT_LOG.md.

## Этап 2. Чистое построение календаря

- [x] `ProductionCalendarApi/ProductionCalendarApi.cs`, `Api.Build.cs` и `ProductionCalendarApiDependency.cs`.
- [x] 2026-10-07: заменить Json типизированным Days в контрактах построения/инициализации; добавить общую ProductionCalendarDayOverride (Date, Type, Comment). Удалить ненужные JSON DTO сервиса; разбор JSON перенести на будущую HTTP-границу.
- [x] Замечание ревью 2026-10-06: Days использует FlatArray, генерация использует Builder/MoveToFlatArray. Различие null/[] сохранено.
- [x] Pipeline: типизированный вход → нормализация/валидация → валидация всех overrides → базовый календарь → применение overrides → Result.
- [x] Реализовать правила ошибок из SPECIFICATION; ошибки модели становятся Invalid; отмена не становится Unknown. Malformed JSON обрабатывает будущий HTTP-адаптер.
- [x] 2026-10-07: убрать явные ThrowIfCancellationRequested; сохранить токен в AsyncPipeline и асинхронных контрактах, чистые стадии вызывать группами методов.
- [x] Генерировать все дни года, в том числе 29 февраля; корректно обрабатывать годы 1 и 9999.
- [x] Независимость от Azure и HTTP, определённый порядок дней по дате.
- [x] 2026-10-07: удалить ранее написанные тесты и тестовый проект по решению пользователя. В следующих инкрементах тесты не создавать.

Готовность: 365/366 уникальных дат; weekdays/weekends верны; рабочая суббота, перенесённый выходной, Holiday и ShortenedDay заменяют базу; пустой массив создаёт базовый год; правила невалидного входа реализованы.

Проверка инкремента 2 (2026-10-06): restore/build Release успешны, 0 ошибок/предупреждений; тесты — 74 пройдено, 0 ошибок, 0 пропущено. Подробности решений — в DEVELOPMENT_LOG.md и ARCHITECTURE.md. Инкремент ожидает ревью, коммит не выполнен. Инкремент 1 одобрен и закоммичен: 40c016b.

Актуализация 2026-10-07: тесты удалены по решению пользователя, их написание исключено во всём проекте. Restore и Release build девяти проектов успешны: 0 ошибок/предупреждений. Прежний результат тестов выше — исторический; текущая проверка выполняется без тестовых стадий.

## Этап 3. Azure Table Storage API

- [x] `Option/StorageOption.cs`: только TableName `ProductionCalendar`; адрес/таймаут вынесены в IHttpApi на ревью этапа 6.
- [x] `Internal.Table/ProductionCalendarDayTableEntity.cs`: string PartitionKey, RowKey, Date, DayType, Comment; явные JSON имена, соответствующие Storage.
- [x] `StorageApi/StorageApi.cs`: константы REST, headers, форматирование ключей и URL, общие проверки.
- [x] `Api.Day.Get.cs`: AsyncPipeline → validation/point URL → IHttpApi.SendAsync → entity decode/validation → business output.
- [x] Замечание ревью 2026-10-07: entity объявляется через var внутри try; проверки и маппинг в том же блоке, catch JsonException и проверка null сохранены.
- [x] `Api.Day.Set.cs`: AsyncPipeline → validation/entity mapping → PUT insert-or-replace → Unit; тело содержит все бизнес-поля, включая пустой Comment.
- [x] `StorageApiDependency.cs`: `Dependency<IHttpApi, StorageOption>.Fold<IStorageApi>` с проверками фабрики, как в образце.
- [x] Передавать CancellationToken, маппить 404 чтения в NotFound; сбои/повреждённые данные в Unknown.
- [x] Проверить фактическую семантику Table REST insert-or-replace, headers и кодов ответа по официальной документации при реализации.
- [x] Проверка кода формирования запросов и ошибок без написания тестов: ровно point lookup; правильные ключи, строки enum/date и полный PUT; очистка комментария; неверный вход до HTTP; 404; malformed entity; отмена.

Готовность: Storage не сканируется; данные в JSON видны как строковые даты и типы; replace-upsert поддерживает повторные записи. Проверены структура запросов и маппинг; ручные вызовы выполнить при доступности среды.

Проверка инкремента 3 (2026-10-07): Release build девяти проектов успешен, 0 ошибок/предупреждений; выполнена проверка формирования HTTP-запросов и маппинга по исходникам и документации Azure. Тесты не писались. Реальные GET/PUT Azure пока не выполнялись, ручная интеграционная проверка остаётся на этапе 7. Инкремент 3 ожидает ревью без коммита; инкремент 2 одобрен и закоммичен: c206593.

## Этап 4. Получение дня

- [x] `Handler/ProductionCalendarDayGetHandler.cs` и `Handler/Handler.Handle.cs`.
- [x] Dependency extension с generic supplier constraint по примеру `CurrentExchangeRateGetHandlerDependency`.
- [x] Pipeline: validation → StorageGetIn → storage supplier → GetOut и маппинг FailureCode.
- [x] IsWorkingDay вычисляется только по DayType; Country/Date/Comment входят в ответ.
- [x] Проверка обработки четырёх DayType, нормализации страны, NotFound/Invalid/Unknown и отмены.

Готовность: единственный вызов storage на точную дату, включая выходной; нет fallback на предыдущий день или вычисления дня без записи.

Проверка инкремента 4 (2026-10-07): restore --disable-parallel и Release build успешны, 0 ошибок/предупреждений. Проверены ветки и маппинг по коду; runtime-вызовы и HTTP пока не выполнялись, тесты не писались. Инкремент ожидает ревью без коммита; инкремент 3 одобрен и закоммичен: da90587.

## Этап 5. Инициализация года

- [x] `Handler/ProductionCalendarInitializeHandler.cs`, `Handler.Handle.cs` и `ProductionCalendarInitializeHandlerDependency.cs`.
- [x] Handler принимает build supplier и storage set supplier; композиция через `Dependency<...,...>.Fold`.
- [x] AsyncPipeline: вход → построение/полная валидация → сохранение всех дней → итоговый ответ.
- [x] Применить Extensions для последовательности записей, если сигнатуры пакета позволяют; иначе изолировать последовательный async-цикл в одной стадии с передачей токена.
- [x] При первом сбое записи вернуть Failure с контекстом страны/года/даты, без ложного успеха.
- [x] Сохранять весь год при каждом вызове, включая обычные дни, пустые комментарии и удалённые overrides.
- [ ] Ручная проверка при доступности среды (перенесена на этап 7): число записей 365/366, все ключи уникальны, новый JSON заменяет прошлые значения, повтор идентичного входа не создаёт новых ключей, другой год/страна не затронуты, invalid JSON даёт ноль записей, сбой/отмена прекращает обработку.

Готовность: повторная инициализация удовлетворяет полной replace-семантике; модель частичных сбоев явно документирована.

Проверка инкремента 5 (2026-10-07): Release build девяти проектов — 0 ошибок/предупреждений; просмотрены цепочка до первой записи, маппинг ошибок и полная запись года. PipeParallelValue использует DegreeOfParallelism=1 и FailureAction=Stop. Runtime/Azure проверка не выполнялась: HTTP-хост и среда пока отсутствуют. Тесты не добавлялись. Инкремент 4 принят и закоммичен: 277057c. Инкремент 5 готов к ревью без коммита.

### Исправления ревью инкремента 5 — 2026-10-07

- [x] Создать src/shared/DayType/DayType.csproj и перенести DayType/DayTypeExtensions; включить проект в solution.
- [x] Убрать зависимости между Contract-проектами, подключить общий Shared в четырёх контрактах.
- [x] Добавить ProductionCalendarInitializeDay в endpoint Contract и маппинг в ProductionCalendarDayOverride внутри handler с проверкой null-элементов.
- [x] Удалить лишнюю ссылку Get handler на ProductionCalendar.Contract и закрепить независимость DTO в AGENTS.md.
- [x] Restore --disable-parallel и Release build десяти проектов: 0 ошибок/предупреждений. Проверены ссылки всех Contract-проектов; только Shared, сторонние библиотеки и собственные DTO. Тесты не создавались. Изменения остаются частью инкремента 5 без коммита до ревью.

## Этап 6. Azure Functions и composition root

- [x] Переключить AzureFunc `OutputType` на Exe и добавить `Program.cs` с `FunctionHost.CreateFunctionsWorkerBuilderStandard().Build().RunAsync()`.
- [x] `Application/Application.cs`: UseStorageApi, UseProductionCalendarApi, ResolveStorageOption; конфигурация из IConfiguration.
- [x] `App.ProductionCalendar.Day.Get.cs` и `App.ProductionCalendar.Initialize.cs`: сборка handler dependencies.
- [x] HTTP pipeline Storage: StandardSocketsHttpHandler → logging → TokenCredentialStandard → PollyStandard → HttpApi → option → StorageApi.
- [x] Настройка обновления credential по образцу; проверить необходимость timer extension для инфраструктурного credential refresh, не добавлять бизнес-таймеры календаря.
- [x] `Function/Function.cs`, `Function.ProductionCalendar.Day.Get.cs`, `Function.ProductionCalendar.Initialize.cs`.
- [x] Строго разбирать route date чтения; для инициализации читать единое JSON-тело Country/Year/Days, проверять поля и маппить Date/Type в типизированную модель; передавать токен, маппить Result в HTTP; не выполнять бизнес-генерацию в Function.
- [x] `host.json`, `appsettings.json` с Info и ProductionCalendar:Storage; документировать локальные настройки без хранения секретов.
- [x] Закрепить camelCase и строковую JSON-сериализацию DayType с запретом числового input.
- [x] Проверить необходимый набор Worker/GarageGroup пакетов, удалить ненужные прямые зависимости образца.

Готовность: host запускается локально; оба маршрута работают; ошибки имеют согласованные коды; `dotnet publish` создаёт пакет Functions с metadata и host.json.

Проверка 2026-10-07: Release build/publish успешны, 0 ошибок/предупреждений; metadata содержит два HTTP endpoint и RefreshAzureTokens, host.json/worker.config.json присутствуют. Core Tools запустил host; вручную подтверждены 400 для неверной даты, null/отсутствия days, неверного регистра type и числового type. Успешные Storage GET/PUT, 404 и refresh требуют среды и остаются на этапе 8. Повторный запуск host с URI эмулятора отклонён автоматической проверкой команд (blocked by policy, подробная причина отсутствует). Тесты не создавались. Инкремент 5 закоммичен: 6d2a63a; инкремент 6 ожидает ревью без коммита.

## Этап 7. Инфраструктура и CI/CD

- [ ] Выполнить подробный план `.infra/README.md` и `.github/workflows/README.md`.
- [ ] Адаптировать Bicep и scripts, создать одну таблицу ProductionCalendar; сохранить Flex Consumption, Managed Identity, RBAC, Test/Prod, OIDC и ZIP lifecycle.
- [ ] Создать build/publish/deploy/install/delete workflows по образцу; календарные settings вместо курсов/таймеров, без ненужной Dataverse-конфигурации.
- [ ] Проверить Bicep build, `bash -n`, restore/build/publish и схему workflows.
- [ ] Настроить предоставленные параметры GitHub/Azure, установить Test и развернуть опубликованный артефакт через CI/CD. Проверку бизнес-поведения выполнить следующим инкрементом 8 в этой Test-среде.
- [ ] Проверить развёртывание выбранного существующего ZIP в Prod и сценарий возврата на предыдущую версию по согласованным параметрам среды.

Готовность: одинаковый артефакт продвигается между средами; credentials приложения — Managed Identity; CI проверяет сборку и инфраструктуру без тестовых стадий; инструкция развёртывания воспроизводима.

## Этап 8. Интеграционная проверка

- [ ] Использовать Azure Test-среду, развёрнутую на этапе 7 через CI/CD; проверить доступ к Function App и ProductionCalendar через Managed Identity/RBAC.
- [ ] Инициализировать невисокосный и високосный годы, прочитать обычный рабочий день, Weekend, Holiday, рабочую субботу и ShortenedDay.
- [ ] Повторить с изменёнными days, days:[] и days:null, проверить восстановление базового типа и очистку Comment.
- [ ] Проверить изоляцию страны/года и отсутствие дубликатов; отрицательные HTTP сценарии.
- [ ] Проверить прямое представление entity: Date и DayType строковые и читаемые.
- [ ] Restore/build/publish Release; обновить PROJECT_GUIDE.md с рабочими командами и примерами. Корневой README.md зарезервирован пользователем для другой информации.

Готовность: выполнены ручные проверки поведения и идемпотентности в доступной среде. Автоматические тесты не создаются. Праздничные даты в примерах не считаются официальным календарём.

## Результат проверки каркаса

Проверено 6 октября 2026 года, SDK .NET 10.0.401:

- `dotnet restore Internal.ProductionCalendar.slnx` — успешно для всех девяти проектов.
- `dotnet build Internal.ProductionCalendar.slnx --no-restore -c Release` — успешно, 0 предупреждений, 0 ошибок.
- Проверены ProjectReference и отсутствие оставшихся имён ExchangeRates в `.csproj`.

Бизнес-логика и тесты поведения пока отсутствуют. `dotnet test` и запуск хоста на этом этапе не выполнялись. Следующий этап — 1, контракты и бизнес-модель.

Ревью инкремента 6 (2026-10-07): удалён собственный GetRequiredValue; чтение настроек через GetRequiredSection().Value.OrEmpty(). Release build — 0 ошибок/предупреждений, без коммита.

Ревью 2026-10-07: UseHttpApi("StorageApi") читает BaseAddress/Timeout; ServiceUri удалён из StorageOption, URL сервиса относительный. Release build успешен, 0 ошибок/предупреждений; интеграция с Storage остаётся этапом 8.
Ревью 2026-10-07: транспортный Days — required FlatArray без nullable. По последующему согласованию с пользователем собственный конвертер удалён: required проверяет наличие, null и [] допустимы. Release build успешен, 0 ошибок/предупреждений; без коммита.

Решение пользователя 2026-10-07: этапы переставлены — инкремент 7 инфраструктура/CI/CD и развёртывание Test; инкремент 8 ручная интеграционная проверка там же. Текущие изменения одобрены к коммиту. Следующий шаг не начинать до отдельной команды пользователя.
