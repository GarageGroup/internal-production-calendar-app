namespace GarageGroup.Internal.ProductionCalendar;

internal sealed record class ErrorJson
{
    public required string Error { get; init; }
}
