namespace GarageGroup.Internal.ProductionCalendar;

public interface IStorageApi :
    IProductionCalendarDayStorageGetSupplier,
    IProductionCalendarDayStorageSetSupplier;
