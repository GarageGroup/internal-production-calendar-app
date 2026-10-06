using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace GarageGroup.Internal.ProductionCalendar;

partial class ProductionCalendarInitializeHandler
{
    public ValueTask<Result<ProductionCalendarInitializeOut, Failure<ProductionCalendarInitializeFailureCode>>> HandleAsync(
        ProductionCalendarInitializeIn input, CancellationToken cancellationToken)
        =>
        AsyncPipeline.Pipe(
            input, cancellationToken)
        .Pipe(
            BuildCalendarIn)
        .ForwardValue(
            calendarApi.BuildAsync,
            static failure => failure.MapFailureCode(MapBuildFailureCode))
        .ForwardValue(
            SaveCalendarAsync);

    private static Result<ProductionCalendarBuildIn, Failure<ProductionCalendarInitializeFailureCode>> BuildCalendarIn(
        ProductionCalendarInitializeIn input)
    {
        var days = FlatArray<ProductionCalendarDayOverride>.Builder.OfLength(input.Days.Length);
        for (var index = 0; index < input.Days.Length; index++)
        {
            var day = input.Days[index];
            if (day is null)
            {
                return Failure.Create(ProductionCalendarInitializeFailureCode.Invalid, "Calendar days must not contain null elements.");
            }

            days[index] = new()
            {
                Date = day.Date,
                Type = day.Type,
                Comment = day.Comment
            };
        }

        return new ProductionCalendarBuildIn
        {
            Country = input.Country,
            Year = input.Year,
            Days = days.MoveToFlatArray()
        };
    }

    private ValueTask<Result<ProductionCalendarInitializeOut, Failure<ProductionCalendarInitializeFailureCode>>> SaveCalendarAsync(
        ProductionCalendarBuildOut calendar, CancellationToken cancellationToken)
        =>
        AsyncPipeline.Pipe(
            calendar.Days, cancellationToken)
        .PipeParallelValue(
            SaveDayAsync,
            SaveOption)
        .MapSuccess(
            _ => new ProductionCalendarInitializeOut
            {
                Country = calendar.Country,
                Year = calendar.Year,
                DaysCount = calendar.Days.Length
            });

    private ValueTask<Result<Unit, Failure<ProductionCalendarInitializeFailureCode>>> SaveDayAsync(
        ProductionCalendarDay day, CancellationToken cancellationToken)
        =>
        AsyncPipeline.Pipe(
            day, cancellationToken)
        .Pipe(
            static day => new ProductionCalendarDayStorageSetIn
            {
                Country = day.Country,
                Date = day.Date,
                DayType = day.DayType,
                Comment = day.Comment
            })
        .PipeValue(
            storageApi.SetDayAsync)
        .MapFailure(
            failure => Failure.Create(
                ProductionCalendarInitializeFailureCode.Unknown,
                $"Failed to save production calendar {day.Country}|{day.Date.Year.ToString("D4", CultureInfo.InvariantCulture)} " +
                $"day {day.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}: {failure.FailureMessage}",
                failure.SourceException));

    private static ProductionCalendarInitializeFailureCode MapBuildFailureCode(ProductionCalendarBuildFailureCode failureCode)
        =>
        failureCode switch
        {
            ProductionCalendarBuildFailureCode.Invalid => ProductionCalendarInitializeFailureCode.Invalid,
            _ => ProductionCalendarInitializeFailureCode.Unknown
        };
}
