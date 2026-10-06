using System;
using PrimeFuncPack;

namespace GarageGroup.Internal.ProductionCalendar;

public static class ProductionCalendarDayGetHandlerDependency
{
    public static Dependency<IProductionCalendarDayGetHandler> UseProductionCalendarDayGetHandler<TStorageApi>(
        this Dependency<TStorageApi> dependency)
        where TStorageApi : IProductionCalendarDayStorageGetSupplier
    {
        ArgumentNullException.ThrowIfNull(dependency);
        return dependency.Map<IProductionCalendarDayGetHandler>(CreateHandler);

        static ProductionCalendarDayGetHandler CreateHandler(TStorageApi storageApi)
        {
            ArgumentNullException.ThrowIfNull(storageApi);
            return new(storageApi);
        }
    }
}
