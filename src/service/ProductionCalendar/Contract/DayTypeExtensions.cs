using System;

namespace GarageGroup.Internal.ProductionCalendar;

public static class DayTypeExtensions
{
    public static bool IsWorkingDay(this DayType dayType)
        =>
        dayType switch
        {
            DayType.WorkingDay or DayType.ShortenedDay => true,
            DayType.Weekend or DayType.Holiday => false,
            _ => throw new ArgumentOutOfRangeException(nameof(dayType), dayType, "Day type is invalid.")
        };
}
