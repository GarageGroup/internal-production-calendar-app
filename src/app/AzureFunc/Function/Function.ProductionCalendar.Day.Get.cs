using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace GarageGroup.Internal.ProductionCalendar;

partial class Function
{
    [Function("GetProductionCalendarDay")]
    public static async Task<HttpResponseData> GetProductionCalendarDayAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "production-calendar/{country}/{date}")] HttpRequestData request,
        string country, string date, CancellationToken cancellationToken)
    {
        if (TryParseDate(date, out var calendarDate) is false)
        {
            return await WriteErrorAsync(request, HttpStatusCode.BadRequest, "Date must have yyyy-MM-dd format.", cancellationToken)
                .ConfigureAwait(false);
        }

        var handler = Application.UseProductionCalendarDayGetHandler().Resolve(request.FunctionContext.InstanceServices);
        var result = await handler.HandleAsync(new() { Country = country, Date = calendarDate }, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            var day = result.SuccessOrThrow();
            return await WriteJsonAsync(request, HttpStatusCode.OK, new ProductionCalendarDayJson
            {
                Date = day.Date,
                Country = day.Country,
                IsWorkingDay = day.IsWorkingDay,
                DayType = day.DayType,
                Comment = day.Comment
            }, cancellationToken).ConfigureAwait(false);
        }

        var failure = result.FailureOrThrow();
        var statusCode = failure.FailureCode switch
        {
            ProductionCalendarDayGetFailureCode.Invalid => HttpStatusCode.BadRequest,
            ProductionCalendarDayGetFailureCode.NotFound => HttpStatusCode.NotFound,
            _ => HttpStatusCode.InternalServerError
        };
        var message = failure.FailureCode is ProductionCalendarDayGetFailureCode.Unknown
            ? "Failed to get production calendar day."
            : failure.FailureMessage;
        
        return await WriteErrorAsync(request, statusCode, message, cancellationToken).ConfigureAwait(false);
    }
}
