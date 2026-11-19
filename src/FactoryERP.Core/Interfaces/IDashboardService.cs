using FactoryERP.Core.Models;

namespace FactoryERP.Core.Interfaces;

public interface IDashboardService
{
    Task<DashboardKpiDto> GetKpisAsync();
}
