# Production Calendar

Сервис производственного календаря: получение конкретного дня и инициализация полного календаря страны на год из JSON с исключениями.

## Текущий этап

Подготовлен каркас из девяти пустых проектов .NET 10. Бизнес-код, HTTP-функции, инфраструктура и workflows будут реализованы последовательно по [плану](IMPLEMENTATION_PLAN.md). Пакеты и настройки проектов перенесены из `internal-exchange-rates-app` с адаптацией названий и ссылок.

`AzureFunc` пока собирается как библиотека (`OutputType=Library`), поскольку у пустого проекта нет точки входа. На этапе хоста добавить `Program.cs` и переключить `OutputType` на `Exe`. Сейчас это не запускаемое приложение Azure Functions.

## Документация

- [Правила работы и код-стиль](AGENTS.md).
- [Архитектура и сопоставление с образцом](ARCHITECTURE.md).
- [Требования, формат данных и поведение](SPECIFICATION.md).
- [Пошаговый план и критерии готовности](IMPLEMENTATION_PLAN.md).
- [План инфраструктуры и CI/CD](.infra/README.md).
- [Будущие GitHub Actions](.github/workflows/README.md).

## Структура

```text
Internal.ProductionCalendar.slnx
src/
  app/AzureFunc/
    Application/
    Function/
  endpoint/
    ProductionCalendarDay.Get/
      Contract/
      Handler/Handler/
    ProductionCalendar.Initialize/
      Contract/
      Handler/Handler/
  service/
    ProductionCalendar/
      Contract/ProductionCalendar.Build/
      Api/ProductionCalendarApi/
    Storage/
      Contract/
        ProductionCalendarDay.Get/
        ProductionCalendarDay.Set/
      Api/
        StorageApi/
        Internal.Table/
        Option/
.infra/
  modules/
  apim/
  scripts/
.github/workflows/
```

Пустые каталоги сохраняются через `.gitkeep`; удалить соответствующий маркер при добавлении файлов. Имя проекта (`Contract`, `Api`, `Handler`) повторяет образец; полные уникальные имена заданы в `AssemblyName`.

## Проверка каркаса

```shell
dotnet restore Internal.ProductionCalendar.slnx
dotnet build Internal.ProductionCalendar.slnx --no-restore -c Release
```

Тестовые проекты появятся вместе с бизнес-реализацией. Успешная сборка каркаса проверяет связи и зависимости, но не поведение календаря.

Следующий шаг: этап 1 плана — контракты и enum `DayType`.
