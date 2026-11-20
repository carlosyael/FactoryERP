using FactoryERP.Core.Domain;
using FactoryERP.Core.Entities;
using FactoryERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FactoryERP.Infrastructure.Services;

internal static class FormulaValidationHelper
{
    public static bool HasStructuralChanges(ProductFormula existing, ProductFormula updated)
    {
        if (existing.ProductId != updated.ProductId
            || !string.Equals(existing.Version.Trim(), (updated.Version ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase)
            || existing.BaseYieldQuantity != updated.BaseYieldQuantity
            || existing.StandardLossMarginPercent != updated.StandardLossMarginPercent
            || existing.OverheadCostPerBatch != updated.OverheadCostPerBatch)
        {
            return true;
        }

        var oldItems = existing.Items.OrderBy(i => i.RawMaterialId)
            .Select(i => (i.RawMaterialId, i.QuantityRequired, i.UnitOfMeasure)).ToList();
        var newItems = (updated.Items ?? []).OrderBy(i => i.RawMaterialId)
            .Select(i => (i.RawMaterialId, i.QuantityRequired, i.UnitOfMeasure)).ToList();
        return !oldItems.SequenceEqual(newItems);
    }

    public static string ComputeNextVersion(string currentVersion, HashSet<string> existingVersions)
    {
        string trimmed = string.IsNullOrWhiteSpace(currentVersion) ? "1.0" : currentVersion.Trim();
        var parts = trimmed.Split('.');
        if (parts.Length == 2 && int.TryParse(parts[0], out int major) && int.TryParse(parts[1], out int minor))
        {
            do { minor++; } while (existingVersions.Contains($"{major}.{minor}"));
            return $"{major}.{minor}";
        }

        if (parts.Length == 1 && int.TryParse(parts[0], out int single))
        {
            do { single++; } while (existingVersions.Contains($"{single}"));
            return $"{single}";
        }

        int suffix = 2;
        while (existingVersions.Contains($"{trimmed}-v{suffix}")) suffix++;
        return $"{trimmed}-v{suffix}";
    }

    public static async Task ValidateFormulaAsync(AppDbContext context, ProductFormula formula)
    {
        if (string.IsNullOrWhiteSpace(formula.Name))
            throw new ArgumentException("El nombre de la fórmula es obligatorio.");
        if (formula.BaseYieldQuantity <= 0)
            throw new ArgumentException("El rendimiento base debe ser mayor a cero.");
        if (formula.StandardLossMarginPercent < 0m || formula.StandardLossMarginPercent >= 100m)
            throw new ArgumentException("El margen de pérdida debe estar entre 0% y 99.99%.");
        if (formula.OverheadCostPerBatch < 0m)
            throw new ArgumentException("El costo indirecto por lote no puede ser negativo.");
        if (formula.Items == null || formula.Items.Count == 0)
            throw new InvalidOperationException("La fórmula debe contener al menos una materia prima.");
        if (!await context.Products.AnyAsync(p => p.Id == formula.ProductId))
            throw new InvalidOperationException("Producto terminado no encontrado.");
        if (formula.Items.GroupBy(i => i.RawMaterialId).Any(g => g.Count() > 1))
            throw new InvalidOperationException("La fórmula no debe repetir la misma materia prima.");

        var ids = formula.Items.Select(i => i.RawMaterialId).Distinct().ToList();
        var materials = await context.RawMaterials.Where(m => ids.Contains(m.Id)).ToDictionaryAsync(m => m.Id);

        foreach (var item in formula.Items)
        {
            if (item.QuantityRequired <= 0m)
                throw new ArgumentException("La cantidad requerida de cada material debe ser mayor a cero.");
            if (!materials.TryGetValue(item.RawMaterialId, out var mat))
                throw new InvalidOperationException($"Materia prima ID {item.RawMaterialId} no encontrada.");
            if (!UnitConverter.AreCompatible(item.UnitOfMeasure, mat.UnitOfMeasure))
                throw new InvalidOperationException(
                    $"La unidad {item.UnitOfMeasure} es incompatible con '{mat.Name}' ({mat.UnitOfMeasure}).");
        }
    }
}
