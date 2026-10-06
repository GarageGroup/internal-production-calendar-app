using System;
using System.Collections.Generic;

namespace GarageGroup.Internal.ProductionCalendar;

internal sealed partial class ProductionCalendarApi : IProductionCalendarApi
{
    private static string NormalizeCountry(string? country)
        =>
        country.OrEmpty().Trim().ToUpperInvariant();

    private static bool IsInvalidCountry(string country)
        =>
        country.Length is not 2
        || char.IsAsciiLetter(country[0]) is false
        || char.IsAsciiLetter(country[1]) is false;

    private sealed record class ValidatedCalendar
    {
        public required string Country { get; init; }

        public int Year { get; init; }

        public required Dictionary<DateOnly, ProductionCalendarDay> Overrides { get; init; }
    }
}
