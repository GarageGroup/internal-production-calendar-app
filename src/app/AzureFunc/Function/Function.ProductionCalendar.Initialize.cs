using System;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace GarageGroup.Internal.ProductionCalendar;

partial class Function
{
    [Function("InitializeProductionCalendar")]
    public static async Task<HttpResponseData> InitializeProductionCalendarAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "production-calendar/initialize")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var input = await ReadInitializeInAsync(request, cancellationToken).ConfigureAwait(false);
        if (input.IsFailure)
        {
            return await WriteErrorAsync(request, HttpStatusCode.BadRequest, input.FailureOrThrow().FailureMessage, cancellationToken).ConfigureAwait(false);
        }

        var handler = Application.UseProductionCalendarInitializeHandler().Resolve(request.FunctionContext.InstanceServices);
        var result = await handler.HandleAsync(input.SuccessOrThrow(), cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            var calendar = result.SuccessOrThrow();
            return await WriteJsonAsync(request, HttpStatusCode.OK, new ProductionCalendarInitializedJson
            {
                Country = calendar.Country,
                Year = calendar.Year,
                DaysCount = calendar.DaysCount
            }, cancellationToken).ConfigureAwait(false);
        }

        var failure = result.FailureOrThrow();
        var isInvalid = failure.FailureCode is ProductionCalendarInitializeFailureCode.Invalid;
        
        return await WriteErrorAsync(
            request,
            isInvalid ? HttpStatusCode.BadRequest : HttpStatusCode.InternalServerError,
            isInvalid ? failure.FailureMessage : "Failed to initialize production calendar.",
            cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<Result<ProductionCalendarInitializeIn, Failure<ProductionCalendarInitializeFailureCode>>> ReadInitializeInAsync(
        HttpRequestData request, CancellationToken cancellationToken)
    {
        try
        {
            var calendar = await JsonSerializer.DeserializeAsync<ProductionCalendarInitializeJson>(
                request.Body, JsonOptions, cancellationToken).ConfigureAwait(false);
            if (calendar is null || calendar.Country is null)
            {
                return Failure.Create(ProductionCalendarInitializeFailureCode.Invalid, "Country, year and days are required; days must be an array.");
            }

            var sourceDays = calendar.Days;
            var days = FlatArray<ProductionCalendarInitializeDay>.Builder.OfLength(sourceDays.Length);
            for (var index = 0; index < sourceDays.Length; index++)
            {
                var day = sourceDays[index];
                if (day is null || TryParseDate(day.Date, out var date) is false)
                {
                    return Failure.Create(ProductionCalendarInitializeFailureCode.Invalid, "Every day must contain a date in yyyy-MM-dd format.");
                }

                var type = day.Type switch
                {
                    nameof(DayType.WorkingDay) => DayType.WorkingDay,
                    nameof(DayType.Weekend) => DayType.Weekend,
                    nameof(DayType.Holiday) => DayType.Holiday,
                    nameof(DayType.ShortenedDay) => DayType.ShortenedDay,
                    _ => (DayType?)null
                };
                if (type is null)
                {
                    return Failure.Create(ProductionCalendarInitializeFailureCode.Invalid, "Day type must be WorkingDay, Weekend, Holiday or ShortenedDay.");
                }

                days[index] = new() { Date = date, Type = type.Value, Comment = day.Comment };
            }

            return new ProductionCalendarInitializeIn
            {
                Country = calendar.Country,
                Year = calendar.Year,
                Days = days.MoveToFlatArray()
            };
        }
        catch (JsonException)
        {
            return Failure.Create(ProductionCalendarInitializeFailureCode.Invalid, "Invalid calendar JSON or missing required fields.");
        }
    }
}
