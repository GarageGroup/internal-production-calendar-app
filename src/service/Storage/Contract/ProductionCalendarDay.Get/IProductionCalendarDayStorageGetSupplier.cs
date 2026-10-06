using System;
using System.Threading;
using System.Threading.Tasks;

namespace GarageGroup.Internal.ProductionCalendar;

public interface IProductionCalendarDayStorageGetSupplier
{
    ValueTask<Result<ProductionCalendarDayStorageGetOut, Failure<StorageFailureCode>>> GetDayAsync(
        ProductionCalendarDayStorageGetIn input, CancellationToken cancellationToken);
}
