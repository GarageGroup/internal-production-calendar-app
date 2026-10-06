using System;

namespace GarageGroup.Internal.ProductionCalendar;

public readonly record struct ProductionCalendarBuildIn
{
    public required string Country { get; init; }

    public int Year { get; init; }

    public required FlatArray<ProductionCalendarDayOverride> Days { get; init; }
}
