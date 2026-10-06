using PrimeFuncPack;

namespace GarageGroup.Internal.ProductionCalendar;

public static class ProductionCalendarApiDependency
{
    public static Dependency<IProductionCalendarApi> UseProductionCalendarApi()
        =>
        Dependency.From<IProductionCalendarApi>(static () => new ProductionCalendarApi());
}
