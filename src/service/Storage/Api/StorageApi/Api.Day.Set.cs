using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using GarageGroup.Infra;

namespace GarageGroup.Internal.ProductionCalendar;

partial class StorageApi
{
    public ValueTask<Result<Unit, Failure<StorageFailureCode>>> SetDayAsync(
        ProductionCalendarDayStorageSetIn input, CancellationToken cancellationToken)
        =>
        AsyncPipeline.Pipe(
            input, cancellationToken)
        .Pipe(
            BuildHttpSendIn)
        .ForwardValue(
            httpApi.SendAsync,
            MapSetFailure)
        .MapSuccess(
            Unit.From);

    private Result<HttpSendIn, Failure<StorageFailureCode>> BuildHttpSendIn(ProductionCalendarDayStorageSetIn input)
    {
        if (input is null)
        {
            return Failure.Create(StorageFailureCode.Invalid, "Production calendar day must be specified.");
        }

        var country = NormalizeCountry(input.Country);
        if (IsInvalidCountry(country) || Enum.IsDefined(input.DayType) is false)
        {
            return Failure.Create(StorageFailureCode.Invalid, "Production calendar day country or day type is invalid.");
        }

        var entity = new ProductionCalendarDayTableEntity
        {
            PartitionKey = BuildPartitionKey(country, input.Date),
            RowKey = BuildRowKey(input.Date),
            Date = input.Date.ToString(DateFormat, CultureInfo.InvariantCulture),
            DayType = input.DayType.ToString(),
            Comment = input.Comment.OrEmpty()
        };

        return new HttpSendIn(HttpVerb.Put, BuildEntityUrl(entity.PartitionKey, entity.RowKey))
        {
            Headers = BuildHeaders(),
            Body = HttpBody.SerializeAsJson(entity),
            SuccessType = HttpSuccessType.OnlyStatusCode
        };
    }

    private static Failure<StorageFailureCode> MapSetFailure(HttpSendFailure failure)
        =>
        failure.ToStandardFailure("An unexpected HTTP failure occurred when setting a production calendar day:")
        .WithFailureCode(StorageFailureCode.Unknown);
}
