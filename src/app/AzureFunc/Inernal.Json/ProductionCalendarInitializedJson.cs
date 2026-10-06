namespace GarageGroup.Internal.ProductionCalendar;

internal sealed record class ProductionCalendarInitializedJson
{
    public required string Country { get; init; }

    public required int Year { get; init; }

    public required int DaysCount { get; init; }
}
