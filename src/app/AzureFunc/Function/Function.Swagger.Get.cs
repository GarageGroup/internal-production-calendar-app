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
    [Function("GetProductionCalendarSwagger")]
    public static async Task<HttpResponseData> GetProductionCalendarSwaggerAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "swagger/swagger.json")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        using var stream = typeof(Function).Assembly.GetManifestResourceStream("ProductionCalendar.OpenApi.json");
        if (stream is null)
        {
            throw new InvalidOperationException("Production Calendar OpenAPI resource was not found.");
        }

        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        return await WriteJsonAsync(request, HttpStatusCode.OK, document.RootElement, cancellationToken).ConfigureAwait(false);
    }
}
