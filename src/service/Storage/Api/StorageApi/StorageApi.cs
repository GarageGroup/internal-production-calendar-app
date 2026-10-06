using System;
using System.Collections.Generic;
using System.Globalization;
using GarageGroup.Infra;

namespace GarageGroup.Internal.ProductionCalendar;

internal sealed partial class StorageApi : IStorageApi
{
    private const string StorageApiVersion = "2019-02-02";

    private const string JsonNoMetadataMediaType = "application/json;odata=nometadata";

    private const string PartitionYearFormat = "yyyy";

    private const string RowDateFormat = "yyyyMMdd";

    private const string DateFormat = "yyyy-MM-dd";

    private readonly IHttpApi httpApi;

    private readonly StorageOption option;

    internal StorageApi(IHttpApi httpApi, StorageOption option)
    {
        if (option.TableName is not "ProductionCalendar")
        {
            throw new ArgumentException("Storage table name must be ProductionCalendar.", nameof(option));
        }

        this.httpApi = httpApi;
        this.option = option;
    }

    private static string NormalizeCountry(string? country)
        =>
        country.OrEmpty().Trim().ToUpperInvariant();

    private static bool IsInvalidCountry(string country)
        =>
        country.Length is not 2
        || char.IsAsciiLetter(country[0]) is false
        || char.IsAsciiLetter(country[1]) is false;

    private static string BuildPartitionKey(string country, DateOnly date)
        =>
        $"{country}|{date.ToString(PartitionYearFormat, CultureInfo.InvariantCulture)}";

    private static string BuildRowKey(DateOnly date)
        =>
        date.ToString(RowDateFormat, CultureInfo.InvariantCulture);

    private string BuildEntityUrl(string partitionKey, string rowKey)
        =>
        $"{option.TableName}"
            + $"(PartitionKey='{Uri.EscapeDataString(partitionKey)}',RowKey='{Uri.EscapeDataString(rowKey)}')";

    private static FlatArray<KeyValuePair<string, string>> BuildHeaders()
        =>
        [
            new("x-ms-version", StorageApiVersion),
            new("x-ms-date", DateTimeOffset.UtcNow.ToString("R", CultureInfo.InvariantCulture)),
            new("Accept", JsonNoMetadataMediaType),
            new("DataServiceVersion", "3.0"),
            new("MaxDataServiceVersion", "3.0")
        ];

    private static DayType? MapDayType(string? value)
        =>
        value switch
        {
            nameof(DayType.WorkingDay) => DayType.WorkingDay,
            nameof(DayType.Weekend) => DayType.Weekend,
            nameof(DayType.Holiday) => DayType.Holiday,
            nameof(DayType.ShortenedDay) => DayType.ShortenedDay,
            _ => null
        };
}
