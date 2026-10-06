using PrimeFuncPack;

namespace GarageGroup.Internal.ProductionCalendar;

partial class Application
{
    internal static Dependency<IProductionCalendarInitializeHandler> UseProductionCalendarInitializeHandler()
        =>
        Dependency.Pipe(
            ProductionCalendarApiDependency.UseProductionCalendarApi())
        .With(
            UseStorageApi())
        .UseProductionCalendarInitializeHandler();
}
