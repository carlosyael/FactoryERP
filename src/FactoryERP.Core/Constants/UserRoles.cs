namespace FactoryERP.Core.Constants;

/// <summary>
/// Roles predefinidos del sistema.
/// </summary>
public static class UserRoles
{
    public const string Admin = "Admin";
    public const string Produccion = "Produccion";
    public const string Ventas = "Ventas";
    public const string Contabilidad = "Contabilidad";

    public static IReadOnlyList<string> GetAll() =>
        [Admin, Produccion, Ventas, Contabilidad];
}
