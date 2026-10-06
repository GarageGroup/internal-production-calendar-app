namespace GarageGroup.Internal.ProductionCalendar;

internal sealed partial class ProductionCalendarInitializeHandler : IProductionCalendarInitializeHandler
{
    private static readonly PipelineParallelOption SaveOption
        =
        new()
        {
            DegreeOfParallelism = 1,
            FailureAction = PipelineParallelFailureAction.Stop
        };

    private readonly IProductionCalendarBuildSupplier calendarApi;

    private readonly IProductionCalendarDayStorageSetSupplier storageApi;

    internal ProductionCalendarInitializeHandler(
        IProductionCalendarBuildSupplier calendarApi, IProductionCalendarDayStorageSetSupplier storageApi)
    {
        this.calendarApi = calendarApi;
        this.storageApi = storageApi;
    }
}
