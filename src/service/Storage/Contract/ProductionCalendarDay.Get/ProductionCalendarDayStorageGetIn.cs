using System;

namespace GarageGroup.Internal.ProductionCalendar;

public readonly record struct ProductionCalendarDayStorageGetIn
{
    public required string Country { get; init; }

    public DateOnly Date { get; init; }
}
