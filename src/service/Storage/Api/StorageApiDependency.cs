using System;
using GarageGroup.Infra;
using PrimeFuncPack;

namespace GarageGroup.Internal.ProductionCalendar;

public static class StorageApiDependency
{
    public static Dependency<IStorageApi> UseStorageApi(
        this Dependency<IHttpApi, StorageOption> dependency)
    {
        ArgumentNullException.ThrowIfNull(dependency);
        return dependency.Fold<IStorageApi>(CreateApi);

        static StorageApi CreateApi(IHttpApi httpApi, StorageOption option)
        {
            ArgumentNullException.ThrowIfNull(httpApi);
            ArgumentNullException.ThrowIfNull(option);

            return new(httpApi, option);
        }
    }
}
