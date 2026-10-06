using System;
using System.Threading;
using System.Threading.Tasks;

namespace GarageGroup.Internal.ProductionCalendar;

partial class ProductionCalendarDayGetHandler
{
    public ValueTask<Result<ProductionCalendarDayGetOut, Failure<ProductionCalendarDayGetFailureCode>>> HandleAsync(
        ProductionCalendarDayGetIn input, CancellationToken cancellationToken)
        =>
        AsyncPipeline.Pipe(
            input, cancellationToken)
        .Pipe(
            BuildStorageGetIn)
        .ForwardValue(
            storageApi.GetDayAsync,
            static failure => failure.MapFailureCode(MapFailureCode))
        .MapSuccess(
            static day => new ProductionCalendarDayGetOut
            {
                Country = day.Country,
                Date = day.Date,
                DayType = day.DayType,
                Comment = day.Comment
            });

    private static Result<ProductionCalendarDayStorageGetIn, Failure<ProductionCalendarDayGetFailureCode>> BuildStorageGetIn(
        ProductionCalendarDayGetIn input)
    {
        var country = input.Country.OrEmpty().Trim().ToUpperInvariant();
        if (country.Length is not 2
            || char.IsAsciiLetter(country[0]) is false
            || char.IsAsciiLetter(country[1]) is false)
        {
            return Failure.Create(ProductionCalendarDayGetFailureCode.Invalid, "Country must contain two ASCII letters.");
        }

        return new ProductionCalendarDayStorageGetIn
        {
            Country = country,
            Date = input.Date
        };
    }

    private static ProductionCalendarDayGetFailureCode MapFailureCode(StorageFailureCode failureCode)
        =>
        failureCode switch
        {
            StorageFailureCode.Invalid => ProductionCalendarDayGetFailureCode.Invalid,
            StorageFailureCode.NotFound => ProductionCalendarDayGetFailureCode.NotFound,
            _ => ProductionCalendarDayGetFailureCode.Unknown
        };
}
