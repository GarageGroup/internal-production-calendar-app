using System;

namespace GarageGroup.Internal.ProductionCalendar;

public sealed record class ProductionCalendarInitializeDay
{
    public required DateOnly Date { get; init; }

    public required DayType Type { get; init; }

    public string? Comment { get; init; }
}
