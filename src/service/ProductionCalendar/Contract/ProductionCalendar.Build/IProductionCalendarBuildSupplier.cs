using System;
using System.Threading;
using System.Threading.Tasks;

namespace GarageGroup.Internal.ProductionCalendar;

public interface IProductionCalendarBuildSupplier
{
    ValueTask<Result<ProductionCalendarBuildOut, Failure<ProductionCalendarBuildFailureCode>>> BuildAsync(
        ProductionCalendarBuildIn input, CancellationToken cancellationToken);
}
