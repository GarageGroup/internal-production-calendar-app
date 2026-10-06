using System;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker.Http;

namespace GarageGroup.Internal.ProductionCalendar;

internal static partial class Function
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.NumberHandling = JsonNumberHandling.Strict;
        options.Converters.Add(new JsonStringEnumConverter<DayType>(allowIntegerValues: false));
        
        return options;
    }

    private static bool TryParseDate(string? value, out DateOnly date)
        =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    private static async Task<HttpResponseData> WriteJsonAsync<T>(
        HttpRequestData request, HttpStatusCode statusCode, T value, CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(statusCode);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await JsonSerializer.SerializeAsync(response.Body, value, JsonOptions, cancellationToken).ConfigureAwait(false);
        
        return response;
    }

    private static Task<HttpResponseData> WriteErrorAsync(
        HttpRequestData request, HttpStatusCode statusCode, string message, CancellationToken cancellationToken)
        =>
        WriteJsonAsync(request, statusCode, new ErrorJson { Error = message }, cancellationToken);
}
