using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GarageGroup.Internal.ProductionCalendar;

partial class ProductionCalendarApi
{
    public ValueTask<Result<ProductionCalendarBuildOut, Failure<ProductionCalendarBuildFailureCode>>> BuildAsync(
        ProductionCalendarBuildIn input, CancellationToken cancellationToken)
        =>
        AsyncPipeline.Pipe(
            input, cancellationToken)
        .Pipe(
            ValidateInput)
        .Forward(
            ValidateOverrides)
        .MapSuccess(
            BuildCalendar);

    private static Result<ProductionCalendarBuildIn, Failure<ProductionCalendarBuildFailureCode>> ValidateInput(
        ProductionCalendarBuildIn input)
    {
        var country = NormalizeCountry(input.Country);
        if (IsInvalidCountry(country))
        {
            return Failure.Create(ProductionCalendarBuildFailureCode.Invalid, "Country must contain two ASCII letters.");
        }

        if (input.Year is < 1 or > 9999)
        {
            return Failure.Create(ProductionCalendarBuildFailureCode.Invalid, "Year must be between 1 and 9999.");
        }

        return input with { Country = country };
    }

    private static Result<ValidatedCalendar, Failure<ProductionCalendarBuildFailureCode>> ValidateOverrides(
        ProductionCalendarBuildIn input)
    {
        var overrides = new Dictionary<DateOnly, ProductionCalendarDay>();
        foreach (var day in input.Days)
        {
            if (day is null || day.Date.Year.Equals(input.Year) is false)
            {
                return Failure.Create(ProductionCalendarBuildFailureCode.Invalid, "Every override must contain a date within the calendar year.");
            }

            if (Enum.IsDefined(day.Type) is false)
            {
                return Failure.Create(ProductionCalendarBuildFailureCode.Invalid, $"Override '{day.Date}' has an invalid day type.");
            }

            if (overrides.TryAdd(day.Date, new()
            {
                Country = input.Country,
                Date = day.Date,
                DayType = day.Type,
                Comment = day.Comment
            }) is false)
            {
                return Failure.Create(ProductionCalendarBuildFailureCode.Invalid, $"Override date '{day.Date}' is duplicated.");
            }
        }

        return new ValidatedCalendar
        {
            Country = input.Country,
            Year = input.Year,
            Overrides = overrides
        };
    }

    private static ProductionCalendarBuildOut BuildCalendar(ValidatedCalendar calendar)
    {
        var firstDate = new DateOnly(calendar.Year, 1, 1);
        var daysCount = DateTime.IsLeapYear(calendar.Year) ? 366 : 365;
        var days = FlatArray<ProductionCalendarDay>.Builder.OfLength(daysCount);

        for (var index = 0; index < days.Length; index++)
        {
            var date = firstDate.AddDays(index);
            days[index] = calendar.Overrides.TryGetValue(date, out var day)
                ? day
                : new()
                {
                    Country = calendar.Country,
                    Date = date,
                    DayType = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? DayType.Weekend : DayType.WorkingDay
                };
        }

        return new()
        {
            Country = calendar.Country,
            Year = calendar.Year,
            Days = days.MoveToFlatArray()
        };
    }
}
