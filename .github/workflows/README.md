# План GitHub Actions

Workflows будут добавлены на этапе 8, после работающего приложения. Сейчас здесь нет активного CI/CD.

| Workflow | Триггер по образцу | Планируемые шаги |
| --- | --- | --- |
| build.yml | Push во все ветки, pull request | Checkout, .NET 10, Bicep compile, bash -n, restore/build Release |
| publish.yml | Release created | Restore/build/publish linux-x64, Info metadata, ZIP, Blob upload, deploy в Test, app settings |
| deploy.yml | workflow_dispatch: environment/version | Скачать существующий ZIP, Azure OIDC login, Flex deploy, settings, отдельная стадия APIM при необходимости |
| install.yml | workflow_dispatch: environment | OIDC login, Incremental Bicep deployment, Managed Identity RBAC, проверка APIM |
| delete.yml | Release deleted | Удалить соответствующий ZIP из artifact storage и Git tag с нужными contents permissions |

## Требования адаптации

1. Solution — `Internal.ProductionCalendar.slnx`, publish project — `src/app/AzureFunc/AzureFunc.csproj`.
2. Environments — `Test`/`Prod`, OIDC `id-token: write` только нужным workflows, минимальные contents permissions.
3. Deployment сохраняет `sku: flexconsumption`, `remote-build: false` по образцу; проверить поддержку при реализации.
4. Перед запуском валидировать release version и artifact name. Не вставлять внешние значения в исполняемый shell-код: передавать через env и корректно цитировать.
5. Ошибки build/publish блокируют публикацию. По решению пользователя от 2026-10-07 тесты не пишутся и dotnet test в workflows не включается.
6. Settings должны быть идентичны в publish-to-Test и ручном deploy; вызывать общий script.
7. Убрать Dataverse jobs и settings, курсовые schedules и currency pairs.
8. В deploy образца APIM job зависит от application job и может быть skipped при skip_deploy. При адаптации явно обработать сценарий самостоятельной APIM-стадии.
9. Не копировать placeholder APIM как будто он публикует методы. Разделять проверку существующего сервиса и создание API operations.
10. Проверить release deletion/tag push: правильные credentials checkout и contents: write; удаление тега не должно удалять данные приложения.

Готовность: workflows проверены, ссылки на solution/scripts существуют, в среде Test выполнена ручная проверка; перед реальным deploy предоставлены параметры среды из `.infra/README.md`.
