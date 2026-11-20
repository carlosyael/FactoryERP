namespace FactoryERP.Infrastructure.Services;

internal static class InventoryConcurrencyGate
{
    public static readonly SemaphoreSlim FinishedBatchGate = new(1, 1);
    public static readonly SemaphoreSlim RawMaterialGate = new(1, 1);
}
