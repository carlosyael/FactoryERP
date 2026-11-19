using System.ComponentModel.DataAnnotations;
using FactoryERP.Core.Enums;

namespace FactoryERP.Core.Entities;

/// <summary>
/// Parámetro de configuración general o de identidad white-label del comercio.
/// </summary>
public class SystemSetting
{
    [Key]
    [StringLength(100)]
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    [StringLength(300)]
    public string Description { get; set; } = string.Empty;

    [StringLength(50)]
    public string Category { get; set; } = "General";

    public SettingDataType DataType { get; set; } = SettingDataType.Text;
    public string? DefaultValue { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public bool IsSystem { get; set; } = true;
}
