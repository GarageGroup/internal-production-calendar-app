using System;

namespace GarageGroup.Internal.ProductionCalendar;

public sealed record class ProductionCalendarBuildOut
{
    public required string Country { get; init; }

    public int Year { get; init; }

    public required FlatArray<ProductionCalendarDay> Days { get; init; }
}
