using System;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GarageGroup.Infra;

namespace GarageGroup.Internal.ProductionCalendar;

partial class StorageApi
{
    public ValueTask<Result<ProductionCalendarDayStorageGetOut, Failure<StorageFailureCode>>> GetDayAsync(
        ProductionCalendarDayStorageGetIn input, CancellationToken cancellationToken)
        =>
        AsyncPipeline.Pipe(
            input, cancellationToken)
        .Pipe(
            BuildHttpSendIn)
        .ForwardValue(
            httpApi.SendAsync,
            MapGetFailure)
        .Forward(
            response => MapDayOrFailure(response, input));

    private Result<HttpSendIn, Failure<StorageFailureCode>> BuildHttpSendIn(ProductionCalendarDayStorageGetIn input)
    {
        var country = NormalizeCountry(input.Country);
        if (IsInvalidCountry(country))
        {
            return Failure.Create(StorageFailureCode.Invalid, "Country must contain two ASCII letters.");
        }

        return new HttpSendIn(HttpVerb.Get, BuildEntityUrl(BuildPartitionKey(country, input.Date), BuildRowKey(input.Date)))
        {
            Headers = BuildHeaders()
        };
    }

    private static Failure<StorageFailureCode> MapGetFailure(HttpSendFailure failure)
        =>
        failure.StatusCode is HttpFailureCode.NotFound
            ? failure.ToStandardFailure("Production calendar day was not found:").WithFailureCode(StorageFailureCode.NotFound)
            : failure.ToStandardFailure("An unexpected HTTP failure occurred when getting a production calendar day:")
                .WithFailureCode(StorageFailureCode.Unknown);

    private static Result<ProductionCalendarDayStorageGetOut, Failure<StorageFailureCode>> MapDayOrFailure(
        HttpSendOut response, ProductionCalendarDayStorageGetIn input)
    {
        try
        {
            var entity = response.Body.DeserializeFromJson<ProductionCalendarDayTableEntity>();

            var country = NormalizeCountry(input.Country);
            if (entity is null
                || string.Equals(entity.PartitionKey, BuildPartitionKey(country, input.Date), StringComparison.Ordinal) is false
                || string.Equals(entity.RowKey, BuildRowKey(input.Date), StringComparison.Ordinal) is false
                || DateOnly.TryParseExact(entity.Date, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) is false
                || date.Equals(input.Date) is false)
            {
                return Failure.Create(StorageFailureCode.Unknown, "Production calendar day response contains invalid keys or date.");
            }

            var dayType = MapDayType(entity.DayType);
            if (dayType is null)
            {
                return Failure.Create(StorageFailureCode.Unknown, "Production calendar day response contains an invalid day type.");
            }

            return new ProductionCalendarDayStorageGetOut
            {
                Country = country,
                Date = date,
                DayType = dayType.Value,
                Comment = entity.Comment
            };
        }
        catch (JsonException exception)
        {
            return Failure.Create(StorageFailureCode.Unknown, "Production calendar day response is invalid.", exception);
        }
    }
}
