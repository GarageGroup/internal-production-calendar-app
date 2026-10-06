using System;

namespace GarageGroup.Internal.ProductionCalendar;

public sealed record class StorageOption
{
    public required Uri ServiceUri { get; init; }

    public required string TableName { get; init; }
}
