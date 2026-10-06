using System;
using System.Threading;
using System.Threading.Tasks;

namespace GarageGroup.Internal.ProductionCalendar;

public interface IProductionCalendarDayGetHandler
{
    ValueTask<Result<ProductionCalendarDayGetOut, Failure<ProductionCalendarDayGetFailureCode>>> HandleAsync(
        ProductionCalendarDayGetIn input, CancellationToken cancellationToken);
}
