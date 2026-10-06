# План инфраструктуры и CI/CD

Текущий этап: только инструкции и каталоги. Bicep и скрипты появятся после реализации приложения. Исходник подходов — `.infra` и `.github/workflows` в `internal-exchange-rates-app`.

## План файлов

| Файл | Действие при реализации |
| --- | --- |
| `main.bicep` | Перенести ресурсы образца; заменить две таблицы на ProductionCalendar и settings на календарные |
| `modules/storage-access.bicep` | Сохранить Managed Identity role assignments для Table и host/deployment Blob storage |
| `apim/main.bicep` | Существующий APIM; сначала проверка placeholder, затем операции только по согласованному HTTP-контракту |
| `scripts/install-azure-resources.sh` | Валидация параметров, resource group, providers, Incremental deployment, outputs |
| `scripts/set-function-app-settings.sh` | Установка host settings и ProductionCalendar__Storage__ServiceUri/TableName |
| `scripts/prepare-apim.sh` | Проверка существующего APIM и согласованной конфигурации |

Сохранить базовую инфраструктуру образца: North Europe, Linux Flex Consumption FC1, dotnet-isolated 10.0, StorageV2, Application Insights и Log Analytics, deployment Blob container, SystemAssigned Managed Identity.

Единственная бизнес-таблица — ProductionCalendar. Blob-контейнеры для Functions host и релизных артефактов не являются дополнительными календарными таблицами.

## Отличия от образца

- Namespace settings: `ProductionCalendar:Storage:ServiceUri` и `ProductionCalendar:Storage:TableName`. Azure environment variables используют двойное подчёркивание.
- Storage API проверяет TableName=ProductionCalendar при создании; имя таблицы в Bicep и app settings должно точно совпадать. ServiceUri для Azure — абсолютный HTTPS endpoint без query/fragment.
- Нет CurrencyPairs, CurrentRates/DailyRates, schedule обновления курсов и бизнес-таймеров.
- В требованиях календаря нет Dataverse. Не копировать dataverseServiceUrl, Application User и grant-dataverse script/jobs. Если интеграция потребуется отдельно, добавить её отдельным согласованным этапом.
- Не наследовать конкретные имена ресурсов, subscription IDs, секреты и values из образца. Имя приложения по умолчанию в примерах: `internal-production-calendar`.
- APIM в образце лишь placeholder: это не готовая публикация API. Планировать реальную конфигурацию только после закрепления маршрутов.

## Порядок работ

1. Проверить актуальную официальную документацию Azure Functions, Table REST, Bicep и Actions на этапе реализации. Версии образца — отправная точка.
2. Адаптировать Bicep, settings и outputs. Единственную таблицу создавать инфраструктурой, а не при каждом HTTP-запросе.
3. Назначить Function Managed Identity `Storage Table Data Contributor` и необходимые роли host/deployment storage по модулю образца; проверить работоспособность host в Test.
4. Адаптировать shell scripts без ссылок на обменные курсы/Dataverse; валидировать inputs и не выводить credentials.
5. Создать workflows по соседнему README; проверить Bicep build, Bash syntax и .NET Release publish.
6. Подготовить инструкцию OIDC и RBAC с конкретными scopes предоставленной среды, без выполнения команд над неизвестными ресурсами.
7. Настроить GitHub Environments Test/Prod, сначала установить Test, затем publish/redeploy и ручную проверку. Тестовый код и test-стадии не добавлять по решению пользователя от 2026-10-07.
8. Обновить этот документ реальными командами, списком обязательных параметров и проверенным порядком действий.

## Параметры GitHub

По модели образца repository variables: `AZURE_ARTIFACT_NAME`, `AZURE_ARTIFACT_CONTAINER_NAME`, `AZURE_ARTIFACT_ACCOUNT_NAME`; repository secret: `AZURE_ACCOUNT_KEY_ARTIFACT`. Последний используется только CI/CD Blob artifact lifecycle, не приложением для Table.

Environment variables в Test/Prod:

| Имя | Назначение |
| --- | --- |
| DEPLOY_CLIENT_ID | Client ID deployment principal с GitHub OIDC |
| DEPLOY_TENANT_ID | Tenant для Azure login |
| DEPLOY_SUBSCRIPTION_ID | Subscription среды |
| AZURE_NAME_ROOT | Основа имён ресурсов |
| AZURE_NAME_POSTFIX | test/prod |
| STORAGE_ACCOUNT_NAME | Storage приложения |
| FUNC_INSTANCE_MEMORY | Опциональный размер Flex instance |
| FUNC_MAX_INSTANCE_COUNT | Опциональный предел instances |
| APIM_RESOURCE_GROUP | При использовании существующего APIM |
| APIM_SERVICE_NAME | Вместе с APIM_RESOURCE_GROUP |

Имена ресурсов: `rg-<root>-<postfix>`, `func-<root>-<postfix>`, `asp-<root>-<postfix>`. Storage name задаётся отдельно по ограничениям Azure, а не собирается из имени с дефисами.

Deployment principal: права создания ресурсов и role assignments в целевой resource group; права на существующий APIM только при его использовании. OIDC subject привязан к repository и точному GitHub Environment. Реальные scope и federated credentials документировать перед установкой среды.

## Проверки и эксплуатация

- Bicep compile основного и APIM шаблона; syntax-check всех scripts.
- Проверка обязательных variables/secret, существования артефакта перед deploy, согласованных имён ресурсов.
- Проверка credentials Table в Test без ключа Storage; проверка metadata и endpoints после deploy.
- Publish создаёт ZIP с Info.ApiVersion/BuildDateTime; Test и Prod получают один и тот же ZIP, без повторной сборки при продвижении.
- Возврат на предыдущую версию — deploy существующего ZIP; он не отменяет изменения календарных данных, внесённые вызовами инициализации.
- Delete удаляет релизный ZIP/tag по образцу, а не таблицу или календарь. Перед переносом проверить права checkout/push и семантику удаления релиза.
