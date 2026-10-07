# GitHub Actions Production Calendar

Инкремент 7: пять workflows реализованы и локально проверены. Реальные workflows и Azure deployment не запускались по указанию пользователя. Variables/secret и OIDC описаны в [.infra/README.md](../../.infra/README.md).

| Workflow | Триггер | Действия |
| --- | --- | --- |
| build.yml | Push во все ветки, pull request | .NET 10 restore/build, Bicep compile, Bash syntax |
| install.yml | workflow_dispatch: Test/Prod | Azure OIDC login, проверка APIM, Incremental Bicep deployment, MI/RBAC |
| publish.yml | release created | Release linux-x64 publish, Info metadata, ZIP upload в существующее хранилище, deploy Test |
| deploy.yml | workflow_dispatch или workflow_call | Существующий ZIP, общие settings, Flex OneDeploy, проверка APIM |
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
- skip_apim: пропустить проверку APIM.

APIM job выполняется также при skip_deploy=true (исправлен сценарий skipped dependency из образца). Без skip_apim обязательны APIM_RESOURCE_GROUP/APIM_SERVICE_NAME. Он только проверяет существующий сервис: endpoints/backend/policies пока не создаются.

## Проверки и статус

actionlint 1.7.12 проверил пять workflows без ошибок. Bicep compile, bash -n, Release linux-x64 publish успешны. Реальные GitHub Environments, OIDC login, upload/download/delete и Azure deploy этими workflows не выполнялись. Их запуск оставлен пользователю; после deployment в Test будет инкремент 8 с ручной проверкой сервиса.