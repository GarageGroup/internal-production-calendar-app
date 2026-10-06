using System;

namespace GarageGroup.Internal.ProductionCalendar;

internal sealed record class ProductionCalendarInitializeJson
{
    public required string Country { get; init; }

    public required int Year { get; init; }

    public required FlatArray<ProductionCalendarInitializeDayJson> Days { get; init; }
}
