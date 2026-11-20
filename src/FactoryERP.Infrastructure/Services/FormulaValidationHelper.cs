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
            || existing.OutputRawMaterialId != updated.OutputRawMaterialId
            || !string.Equals(existing.Name.Trim(), (updated.Name ?? string.Empty).Trim(), StringComparison.Ordinal)
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
        if (string.IsNullOrWhiteSpace(formula.Version))
            throw new ArgumentException("La versión de la fórmula es obligatoria.");
        if (formula.BaseYieldQuantity <= 0)
            throw new ArgumentException("El rendimiento base debe ser mayor a cero.");
        if (formula.StandardLossMarginPercent < 0m || formula.StandardLossMarginPercent >= 100m)
            throw new ArgumentException("El margen de pérdida debe estar entre 0% y 99.99%.");
        if (formula.OverheadCostPerBatch < 0m)
            throw new ArgumentException("El costo indirecto por lote no puede ser negativo.");
        if (formula.Items == null || formula.Items.Count == 0)
            throw new InvalidOperationException("La fórmula debe contener al menos una materia prima.");

        bool hasProd = formula.ProductId is > 0;
        bool hasBulk = formula.OutputRawMaterialId is > 0;
        if (hasProd == hasBulk)
            throw new InvalidOperationException("La fórmula debe asociarse exclusivamente a un producto terminado o a un material semielaborado/granel.");

        if (hasProd)
        {
            formula.OutputRawMaterialId = null;
            if (!await context.Products.AnyAsync(p => p.Id == formula.ProductId!.Value))
                throw new InvalidOperationException("Producto terminado no encontrado.");
        }
        else
        {
            formula.ProductId = null;
            int outputMatId = formula.OutputRawMaterialId!.Value;
            if (!await context.RawMaterials.AnyAsync(m => m.Id == outputMatId))
                throw new InvalidOperationException("Material semielaborado/granel de destino no encontrado.");
            if (formula.Items.Any(i => i.RawMaterialId == outputMatId) || await CreatesCircularDependencyAsync(context, formula.Id, outputMatId, formula.Items.Select(i => i.RawMaterialId)))
                throw new InvalidOperationException("La fórmula genera una referencia circular sobre el mismo material semielaborado.");
        }

        string normalizedVersion = formula.Version.Trim();
        var existingVersions = await context.ProductFormulas
            .Where(f => f.Id != formula.Id && (hasBulk ? f.OutputRawMaterialId == formula.OutputRawMaterialId : f.ProductId == formula.ProductId))
            .Select(f => f.Version)
            .ToListAsync();
        if (existingVersions.Any(v => string.Equals(v.Trim(), normalizedVersion, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Ya existe la versión '{normalizedVersion}' para este {(hasBulk ? "semielaborado" : "producto")}.");

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

    private static async Task<bool> CreatesCircularDependencyAsync(
        AppDbContext context, int editingFormulaId, int targetRawMaterialId, IEnumerable<int> directIngredientIds)
    {
        var edges = await context.ProductFormulas.AsNoTracking()
            .Where(f => f.Id != editingFormulaId && f.OutputRawMaterialId.HasValue)
            .Select(f => new { OutId = f.OutputRawMaterialId!.Value, IngIds = f.Items.Select(i => i.RawMaterialId).ToList() })
            .ToListAsync();

        var depsByMaterial = edges
            .GroupBy(e => e.OutId)
            .ToDictionary(g => g.Key, g => g.SelectMany(x => x.IngIds).Distinct().ToList());

        var visited = new HashSet<int>();
        var queue = new Queue<int>(directIngredientIds.Distinct());
        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            if (current == targetRawMaterialId) return true;
            if (!visited.Add(current) || !depsByMaterial.TryGetValue(current, out var nextIds)) continue;
            foreach (int next in nextIds) queue.Enqueue(next);
        }

        return false;
    }
}
