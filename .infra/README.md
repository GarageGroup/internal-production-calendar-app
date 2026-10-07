# Инфраструктура Production Calendar

Инкремент 7: Bicep и скрипты реализованы, локальные проверки пройдены. По прямому указанию пользователя Azure deployments и workflows не запускались; параметры GitHub/Azure задаёт пользователь. Источник подхода — .infra проекта internal-exchange-rates-app.

## Ресурсы каждой среды

- Resource Group rg-<root>-<postfix>, North Europe.
- Linux Flex Consumption FC1: asp-<root>-<postfix>, Function App func-<root>-<postfix>, dotnet-isolated 10.0.
- StorageV2 Standard_LRS: одна таблица ProductionCalendar и собственный deployment Blob-контейнер function-packages.
- System Assigned Managed Identity Function App. User Assigned MI не создаётся.
- Application Insights и Log Analytics, retention 30 дней по образцу.
- RBAC: Storage Table Data Contributor и Storage Blob Data Owner для identity приложения на его Storage Account, по модулю образца.

Существующее общее хранилище версионных ZIP используется для release lifecycle. По уточнению пользователя 2026-10-07 оно отличается от runtime deployment storage: function-packages создаётся отдельно в Storage Account каждого приложения/среды, как в образце. Общий release-контейнер не создаётся этим Bicep. При продвижении в Prod используется тот же сохранённый ZIP, без сборки заново.

## Файлы

| Файл | Назначение |
| --- | --- |
| main.bicep | Ресурсы, таблица, runtime, deployment storage, settings и outputs |
| modules/storage-access.bicep | Роли System Assigned MI |
| apim/main.bicep | Ссылка на существующий APIM и его resource ID, без изменений |
| scripts/install-azure-resources.sh | Проверка переменных/providers/APIM, Resource Group и Incremental deployment |
| scripts/set-function-app-settings.sh | Общий набор настроек для ручного и автоматического deploy |
| scripts/prepare-apim.sh | Проверка существующего APIM через az apim show |
| scripts/artifact.sh | Upload/download/delete версионного ZIP в существующем хранилище |

APIM_RESOURCE_GROUP/APIM_SERVICE_NAME обязательны для install и обычного deploy. APIM не создаётся; API, backend, policies, операции и function keys в APIM не настраиваются. Подключение пока означает проверку существующего APIM, как в placeholder проекта-образца. Публикация методов будет отдельным будущим изменением.

## App Registrations

Для CI/CD нужны две Azure deployment App Registration: одна для Test, другая для Prod. Например, internal-production-calendar-deploy-test и internal-production-calendar-deploy-prod. Они выполняют Azure login через GitHub OIDC, установку инфраструктуры, deployment Function App и проверку существующего APIM.

Создать их в Microsoft Entra ID → App registrations, тип Supported account types — Accounts in this organizational directory only. Application (client) ID записать в DEPLOY_CLIENT_ID соответствующего GitHub Environment; Directory (tenant) ID — в DEPLOY_TENANT_ID, Subscription ID — в DEPLOY_SUBSCRIPTION_ID. У приложения должен существовать Service Principal (Enterprise application); shell-скрипт ниже создаёт его при отсутствии.

Для самой Function App App Registration создавать не нужно: она работает через System Assigned Managed Identity, созданную Bicep. Dataverse deployment App Registration, Microsoft Graph application permissions и Graph admin consent этому проекту не нужны. Client secrets не создаются: deployment использует federated credentials GitHub OIDC.

## Shell-скрипт выдачи прав

Администратор должен заполнить и выполнить этот Bash-скрипт отдельно для Test и Prod. Требуются Azure CLI и jq; перед запуском выполнить az login в нужном tenant. Скрипт только опубликован здесь, агент его не выполнял.

Он заранее создаёт Resource Group, чтобы deployment principal не требовался Contributor на всю subscription. Имя группы вычисляется так же, как в install workflow. Администратору нужны права управлять выбранной App Registration/federated credentials и Service Principal в Entra, создавать Resource Group/регистрировать providers, а также назначать роли на Resource Group и APIM (Owner либо соответствующее сочетание Contributor и Role Based Access Control Administrator/User Access Administrator).

```shell
#!/usr/bin/env bash
set -euo pipefail

# Заполнить для выбранной среды; имена должны совпадать с GitHub variables.
AZURE_DEPLOY_APP_ID="<deployment-app-registration-client-id>"
SUB_ID="<subscription-id>"
AZURE_NAME_ROOT="internal-production-calendar"
AZURE_NAME_POSTFIX="test" # test или prod
GITHUB_ORG="GarageGroup"
GITHUB_REPO="internal-production-calendar-app"
GITHUB_ENVIRONMENT="Test" # Test или Prod; регистр важен

# Существующий APIM, возможно в другой resource group/subscription того же tenant.
APIM_SUB_ID="$SUB_ID"
APIM_RESOURCE_GROUP="<existing-apim-resource-group>"
APIM_SERVICE_NAME="<existing-apim-name>"

case "$GITHUB_ENVIRONMENT:$AZURE_NAME_POSTFIX" in
  Test:test|Prod:prod) ;;
  *) echo 'GitHub Environment and postfix do not match' >&2; exit 1 ;;
esac
[[ "$AZURE_NAME_ROOT" =~ ^[a-z0-9][a-z0-9-]{1,31}$ ]] || { echo 'Invalid AZURE_NAME_ROOT' >&2; exit 1; }
az account set --subscription "$SUB_ID"

# 1. Определить application object ID и Service Principal object ID.
# Client ID из App Registration не совпадает с этими object IDs.
APP_OBJECT_ID="$(az ad app show --id "$AZURE_DEPLOY_APP_ID" --query id -o tsv --only-show-errors)"
if ! SP_OBJECT_ID="$(az ad sp show --id "$AZURE_DEPLOY_APP_ID" --query id -o tsv --only-show-errors 2>/dev/null)"; then
  SP_OBJECT_ID="$(az ad sp create --id "$AZURE_DEPLOY_APP_ID" --query id -o tsv --only-show-errors)"
fi
[[ -n "$APP_OBJECT_ID" && -n "$SP_OBJECT_ID" ]] || { echo 'Application or Service Principal was not found' >&2; exit 1; }

# 2. Создать или обновить GitHub OIDC credential выбранной среды.
credential_name="github-${GITHUB_REPO}-${AZURE_NAME_POSTFIX}"
credential_file="$(mktemp)"
trap 'rm -f "$credential_file"' EXIT
jq -n \
  --arg name "$credential_name" \
  --arg subject "repo:${GITHUB_ORG}/${GITHUB_REPO}:environment:${GITHUB_ENVIRONMENT}" \
  '{name:$name, issuer:"https://token.actions.githubusercontent.com", subject:$subject, audiences:["api://AzureADTokenExchange"]}' \
  > "$credential_file"
credential_id="$(az ad app federated-credential list --id "$APP_OBJECT_ID" \
  --query "[?name=='$credential_name'].id | [0]" -o tsv --only-show-errors)"
if [[ -n "$credential_id" ]]; then
  az ad app federated-credential update --id "$APP_OBJECT_ID" --federated-credential-id "$credential_id" \
    --parameters "@$credential_file" --output none --only-show-errors
else
  az ad app federated-credential create --id "$APP_OBJECT_ID" \
    --parameters "@$credential_file" --output none --only-show-errors
fi

# 3. Администратор заранее создаёт Resource Group и регистрирует providers.
RG="rg-${AZURE_NAME_ROOT}-${AZURE_NAME_POSTFIX}"
if [[ "$(az group exists --name "$RG" -o tsv --only-show-errors)" == false ]]; then
  az group create --name "$RG" --location northeurope \
    --tags "application=$AZURE_NAME_ROOT" "environment=$AZURE_NAME_POSTFIX" --output none --only-show-errors
fi
[[ "$(az group show --name "$RG" --query location -o tsv --only-show-errors)" == northeurope ]] || { echo 'Resource group must be in North Europe' >&2; exit 1; }
for provider in Microsoft.Web Microsoft.Storage Microsoft.Insights Microsoft.OperationalInsights; do
  if [[ "$(az provider show --namespace "$provider" --query registrationState -o tsv --only-show-errors)" != Registered ]]; then
    az provider register --namespace "$provider" --wait --output none --only-show-errors
  fi
done

# 4. Права deployment principal только на Resource Group приложения.
APP_RG_SCOPE="/subscriptions/$SUB_ID/resourceGroups/$RG"
for role in 'Contributor' 'Role Based Access Control Administrator'; do
  az role assignment create --assignee-object-id "$SP_OBJECT_ID" --assignee-principal-type ServicePrincipal \
    --role "$role" --scope "$APP_RG_SCOPE" --output none --only-show-errors
done

# RBAC Administrator нужен Bicep для назначения Function Managed Identity
# ролей Storage Blob Data Owner и Storage Table Data Contributor.
# Роли самой Function Managed Identity назначит modules/storage-access.bicep.

# 5. Для текущего placeholder APIM достаточно Reader.
APIM_SCOPE="/subscriptions/$APIM_SUB_ID/resourceGroups/$APIM_RESOURCE_GROUP/providers/Microsoft.ApiManagement/service/$APIM_SERVICE_NAME"
az resource show --ids "$APIM_SCOPE" --output none --only-show-errors
az role assignment create --assignee-object-id "$SP_OBJECT_ID" --assignee-principal-type ServicePrincipal \
  --role Reader --scope "$APIM_SCOPE" --output none --only-show-errors

# Когда начнём создавать API/backend/policies/operations в APIM,
# права нужно будет расширить отдельным согласованным изменением:
# az role assignment create --assignee-object-id "$SP_OBJECT_ID" --assignee-principal-type ServicePrincipal \
#   --role 'API Management Service Contributor' --scope "$APIM_SCOPE" --output none --only-show-errors

echo "Configured deployment access for $GITHUB_ENVIRONMENT in $RG"
```

Роли назначаются Service Principal object ID, а DEPLOY_CLIENT_ID в GitHub остаётся Application (client) ID. Contributor обеспечивает deployment ресурсов, RBAC Administrator — назначения ролей; Contributor сам по себе не позволяет выдавать права. См. [Azure role assignments](https://learn.microsoft.com/en-us/azure/role-based-access-control/role-assignments-steps) и [federated credentials](https://learn.microsoft.com/en-us/entra/workload-id/workload-identity-federation-create-trust?pivots=identity-wif-apps-methods-azcli).

Для общего artifact Storage deployment App Registration дополнительных Azure RBAC ролей не требуется в текущем подходе: upload/download/delete используют repository secret AZURE_ACCOUNT_KEY_ARTIFACT, как в образце. Администратор с доступом к ключам существующего Storage Account должен отдельно установить этот GitHub secret, не публикуя ключ в коде или логах.

## GitHub variables и secret

Имена сохранены из проекта-образца. Чувствительные значения не записывать в репозиторий.

Repository variables:

| Имя | Назначение |
| --- | --- |
| AZURE_ARTIFACT_NAME | Префикс ZIP только этого приложения, например internal-production-calendar |
| AZURE_ARTIFACT_CONTAINER_NAME | Существующий общий контейнер версионных ZIP |
| AZURE_ARTIFACT_ACCOUNT_NAME | Существующий Storage Account артефактов |

Repository secret AZURE_ACCOUNT_KEY_ARTIFACT используется только для release Blob lifecycle, не для календаря, host или runtime deployment access.

Variables GitHub Environments Test/Prod:

| Имя | Назначение |
| --- | --- |
| DEPLOY_CLIENT_ID | Deployment principal для GitHub OIDC |
| DEPLOY_TENANT_ID | Azure tenant |
| DEPLOY_SUBSCRIPTION_ID | Subscription среды |
| AZURE_NAME_ROOT | Основа имён: 2–32 строчных буквы/цифры/дефисы, первый символ буква или цифра |
| AZURE_NAME_POSTFIX | test в Test, prod в Prod |
| STORAGE_ACCOUNT_NAME | Уникальный Storage Account приложения: 3–24 строчных буквы/цифры |
| FUNC_INSTANCE_MEMORY | Опционально: 512, 2048 или 4096 MB, default 2048 |
| FUNC_MAX_INSTANCE_COUNT | Опционально: 40–1000, default 100 |
| APIM_RESOURCE_GROUP | Resource Group существующего APIM |
| APIM_SERVICE_NAME | Имя существующего APIM |

Не переопределять repository artifact variables разными значениями в Test/Prod: обе среды скачивают один и тот же release ZIP. Конкретные имена/ID/секреты из образца не копировать. Сеть существующего artifact Storage должна разрешать доступ GitHub runner.

## OIDC и права deployment principal

Для каждого GitHub Environment добавить federated credential:

- issuer: https://token.actions.githubusercontent.com
- audience: api://AzureADTokenExchange
- subject: repo:GarageGroup/internal-production-calendar-app:environment:Test либо :environment:Prod.

Principal должен иметь Contributor на целевой resource group и право назначения ролей (например Role Based Access Control Administrator) для её Storage Account. Если Resource Group создаёт install script, principal дополнительно должен иметь право создавать её в subscription; либо группу заранее создаёт администратор и выдаёт права на неё. Для проверки APIM достаточно Reader на существующем APIM. Для deployment нужны права Function App, предоставляемые Contributor. В subscription заранее должны быть зарегистрированы Microsoft.Web, Microsoft.Storage, Microsoft.Insights, Microsoft.OperationalInsights; скрипт проверяет регистрацию, но не регистрирует providers автоматически.

Установка использует Incremental mode; она не удаляет неописанные ресурсы. Bicep создаёт пустую таблицу, календарь затем заполняется HTTP-инициализацией. Runtime Table доступ — managed identity с https://storage.azure.com/.default; storage account key в приложении не используется.

## Настройки Function App

```ini
AzureWebJobsStorage__accountName=<Storage Account приложения>
AzureWebJobsStorage__credential=managedidentity
StorageApi__BaseAddress=https://<Storage Account приложения>.table.core.windows.net/
StorageApi__Timeout=01:00:00
ProductionCalendar__Storage__TableName=ProductionCalendar
APPLICATIONINSIGHTS_CONNECTION_STRING=<output Application Insights>
```

Settings присутствуют в Bicep; set-function-app-settings.sh обновляет одинаковый набор при publish/deploy и добавляет Info__DeployDateTime. Table endpoint читается из primaryEndpoints.table, без ручного конструирования адреса. Info.ApiVersion/BuildDateTime в ZIP устанавливает publish workflow. Timer extension нужен для инфраструктурного RefreshAzureTokens каждые 30 минут; бизнес-таймеров нет.

## Порядок запуска пользователем

1. Настроить repository variables/secret, environments Test/Prod и OIDC/RBAC. Для Prod можно включить approval в GitHub Environment.
2. Запустить Install Infrastructure с environment=Test. До создания ресурсов проверяются обязательные параметры, providers и существующий APIM.
3. Создать release с допустимым tag, например v1.0.0. Publish Release сохраняет ZIP и вызывает Deploy Application для Test.
4. Проверить список функций и логи host. Ручная проверка бизнес-поведения — инкремент 8.
5. Для Prod запустить Install Infrastructure с environment=Prod, затем Deploy Application с выбранной проверенной version. Реальный запуск выполняет пользователь.

При сбое deploy уже сохранённого ZIP повторить deploy job или Deploy Application с той же version, не пересобирать релиз. Upload использует overwrite=false: перезапись существующей версии запрещена; для изменённого кода создать новую version. Откат приложения — Deploy Application предыдущего ZIP; данные календаря при этом не откатываются. Delete Artifact удаляет ZIP этого приложения и release tag, а не Azure-ресурсы или таблицу.

## Локальные проверки 2026-10-07

Bicep CLI 0.43.8: main/apim compile успешны. Git Bash: bash -n всех четырёх скриптов успешен. actionlint 1.7.12: пять workflows проверены, ошибок нет (ShellCheck/Pyflakes отключены; Bash syntax проверен отдельно). dotnet publish Release linux-x64 --self-contained false успешен, включая restore/build. Тесты и автоматический тестовый harness не создавались. Реальные ресурсы/CI runs/deployment не выполнялись по указанию пользователя.

Источники: [Flex deployment storage](https://learn.microsoft.com/en-us/azure/azure-functions/flex-consumption-how-to#configure-deployment-settings), [Functions Action](https://github.com/Azure/functions-action).