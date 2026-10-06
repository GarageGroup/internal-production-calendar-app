using System;
using GarageGroup.Infra;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PrimeFuncPack;

[assembly: RefreshableTokenCredential("0 */30 * * * *", IsDisabled = false)]

namespace GarageGroup.Internal.ProductionCalendar;

internal static partial class Application
{
    private static Dependency<IStorageApi> UseStorageApi()
        =>
        PrimaryHandler.UseStandardSocketsHttpHandler()
        .UseLogging("StorageApi")
        .UseTokenCredentialStandard("https://storage.azure.com/.default")
        .UsePollyStandard()
        .UseHttpApi("StorageApi")
        .With(
            ResolveStorageOption)
        .UseStorageApi();

    private static StorageOption ResolveStorageOption(IServiceProvider serviceProvider)
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        return new()
        {
            TableName = configuration.GetRequiredSection("ProductionCalendar:Storage:TableName").Value.OrEmpty()
        };
    }
}
