using System;
using PrimeFuncPack;

namespace GarageGroup.Internal.ProductionCalendar;

public static class ProductionCalendarInitializeHandlerDependency
{
    public static Dependency<IProductionCalendarInitializeHandler> UseProductionCalendarInitializeHandler<TCalendarApi, TStorageApi>(
        this Dependency<TCalendarApi, TStorageApi> dependency)
        where TCalendarApi : IProductionCalendarBuildSupplier
        where TStorageApi : IProductionCalendarDayStorageSetSupplier
    {
        ArgumentNullException.ThrowIfNull(dependency);
        return dependency.Fold<IProductionCalendarInitializeHandler>(CreateHandler);

        static ProductionCalendarInitializeHandler CreateHandler(TCalendarApi calendarApi, TStorageApi storageApi)
        {
            ArgumentNullException.ThrowIfNull(calendarApi);
            ArgumentNullException.ThrowIfNull(storageApi);

            return new(calendarApi, storageApi);
        }
    }
}
