using System;

namespace GarageGroup.Internal.ProductionCalendar;

public readonly record struct ProductionCalendarInitializeIn
{
    public required string Country { get; init; }

    public int Year { get; init; }

    public required FlatArray<ProductionCalendarInitializeDay> Days { get; init; }
}
