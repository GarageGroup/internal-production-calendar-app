namespace GarageGroup.Internal.ProductionCalendar;

public sealed record class ProductionCalendarInitializeOut
{
    public required string Country { get; init; }

    public int Year { get; init; }

    public int DaysCount { get; init; }
}
