namespace FactoryERP.Infrastructure.Services;

internal static class InventoryConcurrencyGate
{
    public static readonly SemaphoreSlim FinishedBatchGate = new(1, 1);
}
