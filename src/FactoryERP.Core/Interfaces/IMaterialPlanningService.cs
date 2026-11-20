using FactoryERP.Core.Models;

namespace FactoryERP.Core.Interfaces;

public interface IMaterialPlanningService
{
    Task<List<MaterialPlanningSuggestionDto>> GetPurchaseSuggestionsAsync(
        bool onlyWithShortage = true,
        bool excludeManufacturedInHouse = true);

    Task<List<MaterialPlanningSuggestionDto>> GetShortagesForFormulaAsync(
        int formulaId,
        int plannedQuantity,
        decimal? overrideLossMarginPercent = null);
}
