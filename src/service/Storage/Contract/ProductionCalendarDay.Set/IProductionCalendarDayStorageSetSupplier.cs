using System;
using System.Threading;
using System.Threading.Tasks;

namespace GarageGroup.Internal.ProductionCalendar;

public interface IProductionCalendarDayStorageSetSupplier
{
    ValueTask<Result<Unit, Failure<StorageFailureCode>>> SetDayAsync(
        ProductionCalendarDayStorageSetIn input, CancellationToken cancellationToken);
}
