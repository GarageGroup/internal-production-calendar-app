namespace GarageGroup.Internal.ProductionCalendar;

internal sealed partial class ProductionCalendarDayGetHandler : IProductionCalendarDayGetHandler
{
    private readonly IProductionCalendarDayStorageGetSupplier storageApi;

    internal ProductionCalendarDayGetHandler(IProductionCalendarDayStorageGetSupplier storageApi)
        =>
        this.storageApi = storageApi;
}
