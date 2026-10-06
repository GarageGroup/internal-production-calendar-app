namespace GarageGroup.Internal.ProductionCalendar;

internal sealed record class ProductionCalendarInitializeDayJson
{
    public required string Date { get; init; }

    public required string Type { get; init; }

    public string? Comment { get; init; }
}
