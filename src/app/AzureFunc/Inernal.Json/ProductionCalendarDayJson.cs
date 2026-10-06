using System;

namespace GarageGroup.Internal.ProductionCalendar;

internal sealed record class ProductionCalendarDayJson
{
    public required DateOnly Date { get; init; }

    public required string Country { get; init; }

    public required bool IsWorkingDay { get; init; }

    public required DayType DayType { get; init; }

    public string? Comment { get; init; }
}
