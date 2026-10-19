# GitHub Actions Production Calendar

Инкремент 7: пять workflows реализованы и локально проверены. Реальные workflows и Azure deployment не запускались по указанию пользователя. Variables/secret и OIDC описаны в [.infra/README.md](../../.infra/README.md).

| Workflow | Триггер | Действия |
| --- | --- | --- |
| build.yml | Push во все ветки, pull request | .NET 10 restore/build, Bicep compile, Bash syntax |
| install.yml | workflow_dispatch: Test/Prod | Azure OIDC login, проверка APIM, Incremental Bicep deployment, MI/RBAC |
| publish.yml | release created | Release linux-x64 publish, Info metadata, ZIP upload в существующее хранилище, deploy Test |
| deploy.yml | workflow_dispatch или workflow_call | Существующий ZIP, общие settings, Flex OneDeploy, configure-apim (API/Product/backend/key/policy), update-swagger (operations/schemas) |
| delete.yml | release deleted | Удаление ZIP этого приложения и соответствующего Git tag |

Tests/dotnet test отсутствуют по требованию пользователя. Dataverse, валютные пары и бизнес-таймеры не переносятся из образца. Файлы .sh/.yml/.bicep закреплены с LF в .gitattributes для Linux runner.

## Публикация и deploy

Solution Internal.ProductionCalendar.slnx, publish project src/app/AzureFunc/AzureFunc.csproj, runtime linux-x64, framework-dependent .NET 10. Имя ZIP: AZURE_ARTIFACT_NAME-VERSION.zip. Version и artifact prefix проверяются; значения GitHub передаются в env, не вставляются в shell-код. В ZIP Info.ApiVersion/BuildDateTime; Azure settings применяет один общий скрипт.

publish.yml вызывает deploy.yml как reusable workflow с environment=Test. Publish job использует repository artifact variables/secret, Azure login нужен только environment job. Один и тот же deploy выполняется при ручном продвижении в Prod; remote-build=false, sku=flexconsumption. Настройки применяются перед загрузкой пакета. Environment concurrency общая для install/deploy, чтобы операции одной среды не пересекались.

Upload overwrite=false сохраняет неизменность существующей версии. При уже загруженном ZIP повторять deploy, а для нового кода выпускать новый release. Удаление и публикация одной version сериализованы общей release concurrency. Delete checkout использует default branch, чтобы удалённый tag не мешал получить cleanup script.

## Ручной deploy

Входы:

- environment: Test или Prod.
- version: существующая версия, например v1.0.0.
- skip_deploy: пропустить Function App deployment.
- skip_apim: пропустить настройку APIM.

APIM job выполняется также при skip_deploy=true (исправлен сценарий skipped dependency из образца). Без skip_apim обязательны APIM_RESOURCE_GROUP/APIM_SERVICE_NAME. configure-apim запускает prepare-apim.sh deploy: создаёт отдельный API/Product production-calendar-<env>, backend и policy; операции GET /{country}/{date}, POST /initialize и схемы импортирует следующий update-swagger; Function key читает ARM/Bicep и сохраняет в secret Named Value. Reader заменён на API Management Service Contributor на RG APIM — администратору нужно обновить права по .infra/README.md. Frontend требует APIM subscription key, backend получает x-functions-key. При skip_deploy=true используется уже опубликованная Function и ZIP не скачивается; после ротации ключа достаточно повторить этот APIM-only запуск. При ошибке Function deployment APIM не меняется. Дополнительных GitHub variables/secrets нет.

## Проверки и статус

actionlint 1.7.12 проверил пять workflows без ошибок. Bicep compile, bash -n, Release linux-x64 publish успешны. Реальные GitHub Environments, OIDC login, upload/download/delete и Azure deploy этими workflows не выполнялись. Их запуск оставлен пользователю; после deployment в Test будет инкремент 8 с ручной проверкой сервиса.

Обновление 2026-10-19: APIM configuration реализована локально; deploy/workflows агент не запускал. Имя job — configure-apim, проверка готовности ключа ограничена 12 попытками с интервалом 10 секунд. Результаты локальных проверок — DEVELOPMENT_LOG.md.


## update-swagger — 2026-10-19

Аналог Timesheet: отдельный job после configure-apim импортирует OpenAPI из развёрнутой Function через .infra/scripts/update-swagger.sh. Input update_swagger (default true) добавлен в workflow_dispatch и workflow_call; publish-to-Test наследует default. При skip_apim либо ошибке configure-apim импорт пропускается; при skip_deploy работает с текущей Function. После первого release с GET /api/swagger/swagger.json можно отдельно обновлять API без повторного Function deployment. До такого release APIM-only импорт старого ZIP не сработает.

Операции/схемы поддерживаются в Swagger/swagger.json приложения, не дублируются в Bicep. Bicep публикует новый Product среды, добавляет API и группу Developers; подписка требует одобрения администратора. Пользовательские subscription keys и сайт Developer Portal workflow не создаёт. Backend Function key не меняет способ авторизации клиентов. Новые variables/secrets не нужны. Проверки build/publish/OpenAPI/Bicep/Bash/actionlint записаны в DEVELOPMENT_LOG.md; Azure deployment не выполнялся.
