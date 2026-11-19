using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace FactoryERP.Infrastructure.Data;

internal static class DbDemoSeeder
{
    public static async Task SeedAsync(AppDbContext context)
    {
        if (await context.RawMaterials.AnyAsync() || await context.Products.AnyAsync())
            return;

        var supplier = new Supplier
        {
            Name = "Distribuidora Química del Caribe",
            Rnc = "131000112",
            Phone = "809-555-0101",
            ContactPerson = "Departamento Comercial"
        };
        context.Suppliers.Add(supplier);

        var materials = new List<RawMaterial>
        {
            new() { Sku = "MP-BASE-01", Name = "Base Cremosa Capilar Neutra", Category = RawMaterialCategory.Base, UnitOfMeasure = UnitOfMeasure.Gram, CurrentStock = 15000m, MinimumStockLevel = 3000m, AverageCost = 0.45m, LastPurchaseCost = 0.45m },
            new() { Sku = "MP-ACE-01", Name = "Aceite de Argán y Coco Virgen", Category = RawMaterialCategory.OilButter, UnitOfMeasure = UnitOfMeasure.Milliliter, CurrentStock = 8000m, MinimumStockLevel = 1500m, AverageCost = 1.20m, LastPurchaseCost = 1.20m },
            new() { Sku = "MP-ACT-01", Name = "Queratina Hidrolizada + Biotina", Category = RawMaterialCategory.ActiveIngredient, UnitOfMeasure = UnitOfMeasure.Milliliter, CurrentStock = 4000m, MinimumStockLevel = 800m, AverageCost = 2.50m, LastPurchaseCost = 2.50m },
            new() { Sku = "MP-FRA-01", Name = "Fragancia Botánica Frutal", Category = RawMaterialCategory.Fragrance, UnitOfMeasure = UnitOfMeasure.Milliliter, CurrentStock = 2000m, MinimumStockLevel = 400m, AverageCost = 1.80m, LastPurchaseCost = 1.80m },
            new() { Sku = "EMP-ENV-16", Name = "Envase Pote 16 oz + Tapa", Category = RawMaterialCategory.Packaging, UnitOfMeasure = UnitOfMeasure.Unit, CurrentStock = 250m, MinimumStockLevel = 50m, AverageCost = 38.00m, LastPurchaseCost = 38.00m },
            new() { Sku = "EMP-ETQ-16", Name = "Etiqueta Impermeable 16 oz", Category = RawMaterialCategory.Label, UnitOfMeasure = UnitOfMeasure.Unit, CurrentStock = 300m, MinimumStockLevel = 50m, AverageCost = 12.00m, LastPurchaseCost = 12.00m }
        };
        await context.RawMaterials.AddRangeAsync(materials);

        var mascara = new Product
        {
            Sku = "CAP-MAS-16",
            Name = "Mascarilla Nutritiva Reparación Profunda",
            Line = "Reparación",
            Presentation = "16 oz",
            Description = "Tratamiento intensivo formulado con queratina hidrolizada y aceites naturales.",
            Benefits = "Reduce el frizz, sella puntas abiertas y devuelve elasticidad desde la primera aplicación.",
            UsageInstructions = "Aplicar de medios a puntas sobre el cabello húmedo, dejar actuar 15 minutos y enjuagar.",
            KeyIngredients = "Queratina Hidrolizada, Aceite de Argán, Aceite de Coco, Biotina",
            RetailPrice = 850m,
            SalonPrice = 650m,
            WholesalePrice = 520m,
            Cost = 265m,
            MinimumStockLevel = 15,
            ShowInPublicCatalog = true
        };

        var gotero = new Product
        {
            Sku = "CAP-GOT-04",
            Name = "Gotero Fortalecedor Anticaída",
            Line = "Crecimiento",
            Presentation = "4 oz",
            Description = "Tónico concentrado para cuero cabelludo con biotina y extractos botánicos.",
            Benefits = "Estimula el folículo piloso y fortalece la hebra desde la raíz.",
            UsageInstructions = "Aplicar directamente en el cuero cabelludo 3 veces por semana masajeando suavemente.",
            KeyIngredients = "Biotina, Romero, Aceite de Argán",
            RetailPrice = 690m,
            SalonPrice = 520m,
            WholesalePrice = 420m,
            Cost = 190m,
            MinimumStockLevel = 20,
            ShowInPublicCatalog = true
        };

        await context.Products.AddRangeAsync(mascara, gotero);

        var customer = new Customer
        {
            Name = "Consumidor Final",
            RncCedula = "00000000000",
            CustomerType = CustomerType.Retail,
            Phone = "809-000-0000",
            CreditLimit = 0m
        };
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        context.ProductFormulas.Add(new ProductFormula
        {
            ProductId = mascara.Id,
            Name = "Fórmula Estándar Mascarilla 16 oz",
            Version = "1.0",
            BaseYieldQuantity = 10,
            StandardLossMarginPercent = 5m,
            OverheadCostPerBatch = 350m,
            Instructions = "1. Homogeneizar base cremosa. 2. Incorporar aceites y activos a baja temperatura. 3. Envasar y etiquetar.",
            Items = new List<FormulaItem>
            {
                new() { RawMaterialId = materials[0].Id, QuantityRequired = 3500m, UnitOfMeasure = UnitOfMeasure.Gram },
                new() { RawMaterialId = materials[1].Id, QuantityRequired = 400m, UnitOfMeasure = UnitOfMeasure.Milliliter },
                new() { RawMaterialId = materials[2].Id, QuantityRequired = 150m, UnitOfMeasure = UnitOfMeasure.Milliliter },
                new() { RawMaterialId = materials[3].Id, QuantityRequired = 40m, UnitOfMeasure = UnitOfMeasure.Milliliter },
                new() { RawMaterialId = materials[4].Id, QuantityRequired = 10m, UnitOfMeasure = UnitOfMeasure.Unit },
                new() { RawMaterialId = materials[5].Id, QuantityRequired = 10m, UnitOfMeasure = UnitOfMeasure.Unit }
            }
        });

        context.FinishedBatches.AddRange(
            new FinishedBatch { ProductId = mascara.Id, BatchCode = "LOT-INI-001", ProductionDate = DateTime.UtcNow.AddDays(-10), ExpirationDate = DateTime.UtcNow.AddMonths(18), InitialQuantity = 24, QuantityRemaining = 24, UnitCost = 265m },
            new FinishedBatch { ProductId = gotero.Id, BatchCode = "LOT-INI-002", ProductionDate = DateTime.UtcNow.AddDays(-5), ExpirationDate = DateTime.UtcNow.AddMonths(24), InitialQuantity = 30, QuantityRemaining = 30, UnitCost = 190m });

        await context.SaveChangesAsync();
    }
}
