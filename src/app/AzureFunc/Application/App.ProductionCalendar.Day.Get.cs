using PrimeFuncPack;

namespace GarageGroup.Internal.ProductionCalendar;

partial class Application
{
    internal static Dependency<IProductionCalendarDayGetHandler> UseProductionCalendarDayGetHandler()
        =>
        UseStorageApi().UseProductionCalendarDayGetHandler();
}
