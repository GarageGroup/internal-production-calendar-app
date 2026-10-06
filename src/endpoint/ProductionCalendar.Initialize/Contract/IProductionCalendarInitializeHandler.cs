using System;
using System.Threading;
using System.Threading.Tasks;

namespace GarageGroup.Internal.ProductionCalendar;

public interface IProductionCalendarInitializeHandler
{
    ValueTask<Result<ProductionCalendarInitializeOut, Failure<ProductionCalendarInitializeFailureCode>>> HandleAsync(
        ProductionCalendarInitializeIn input, CancellationToken cancellationToken);
}
