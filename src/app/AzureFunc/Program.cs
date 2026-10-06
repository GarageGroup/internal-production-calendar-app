using System.Threading.Tasks;
using GarageGroup.Infra;
using Microsoft.Extensions.Hosting;

namespace GarageGroup.Internal.ProductionCalendar;

internal static class Program
{
    private static Task Main()
        =>
        FunctionHost.CreateFunctionsWorkerBuilderStandard().Build().RunAsync();
}
