using System.Text.Json.Serialization;

namespace GarageGroup.Internal.ProductionCalendar;

internal sealed record class ProductionCalendarDayTableEntity
{
    [JsonPropertyName("PartitionKey")]
    public required string PartitionKey { get; init; }

    [JsonPropertyName("RowKey")]
    public required string RowKey { get; init; }

    [JsonPropertyName("Date")]
    public required string Date { get; init; }

    [JsonPropertyName("DayType")]
    public required string DayType { get; init; }

    [JsonPropertyName("Comment")]
    public string? Comment { get; init; }
}
