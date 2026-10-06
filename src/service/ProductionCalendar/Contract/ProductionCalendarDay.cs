using System;

namespace GarageGroup.Internal.ProductionCalendar;

public sealed record class ProductionCalendarDay
{
    public required string Country { get; init; }

    public DateOnly Date { get; init; }

    public DayType DayType { get; init; }

    public string? Comment { get; init; }

    public bool IsWorkingDay => DayType.IsWorkingDay();
}
