using CurrieTechnologies.Razor.SweetAlert2;
using FactoryERP.Core.Constants;
using FactoryERP.Core.Interfaces;
using FactoryERP.Core.Models;
using Microsoft.AspNetCore.Components;

namespace FactoryERP.Web.Components.Pages;

public partial class Roles
{
    [Inject] private IRoleManagementService RoleManagementService { get; set; } = default!;
    [Inject] private SweetAlertService Swal { get; set; } = default!;

    private List<RoleSummaryDto> _roles = new();
    private readonly List<IGrouping<string, PermissionDefinition>> _permissionGroups =
        Permissions.GetDefinitions().GroupBy(d => d.Module).ToList();
    private readonly Dictionary<string, string> _displayNames =
        Permissions.GetDefinitions().ToDictionary(d => d.Code, d => d.DisplayName, StringComparer.OrdinalIgnoreCase);

    private bool _showModal;
    private string? _editingRoleId;
    private bool _isEditingAdmin;
    private string _roleName = string.Empty;
    private readonly HashSet<string> _selectedPermissions = new(StringComparer.OrdinalIgnoreCase);

    protected override async Task OnInitializedAsync() => await LoadRolesAsync();

    private async Task LoadRolesAsync() => _roles = await RoleManagementService.GetAllRolesAsync();

    private string GetDisplayName(string code) =>
        _displayNames.TryGetValue(code, out string? name) ? name : code;

    private bool IsLockedAdminPermission(string code) =>
        _isEditingAdmin && (code == Permissions.UsersManage || code == Permissions.SettingsManage);

    private void OpenCreateModal()
    {
        _editingRoleId = null;
        _isEditingAdmin = false;
        _roleName = string.Empty;
        _selectedPermissions.Clear();
        _selectedPermissions.Add(Permissions.DashboardView);
        _showModal = true;
    }

    private void OpenEditModal(RoleSummaryDto role)
    {
        _editingRoleId = role.Id;
        _isEditingAdmin = string.Equals(role.Name, UserRoles.Admin, StringComparison.OrdinalIgnoreCase);
        _roleName = role.Name;
        _selectedPermissions.Clear();
        foreach (string perm in role.Permissions)
            _selectedPermissions.Add(perm);
        _showModal = true;
    }

    private void TogglePermission(string code, bool selected)
    {
        if (IsLockedAdminPermission(code)) return;
        if (selected) _selectedPermissions.Add(code);
        else _selectedPermissions.Remove(code);
    }

    private void ToggleGroup(string module, bool selectAll)
    {
        var group = _permissionGroups.FirstOrDefault(g => g.Key == module);
        if (group is null) return;
        foreach (var item in group)
        {
            if (IsLockedAdminPermission(item.Code)) continue;
            if (selectAll) _selectedPermissions.Add(item.Code);
            else _selectedPermissions.Remove(item.Code);
        }
    }

    private async Task SaveRoleAsync()
    {
        try
        {
            if (_editingRoleId is null)
                await RoleManagementService.CreateRoleAsync(_roleName, _selectedPermissions);
            else
                await RoleManagementService.UpdateRoleAsync(_editingRoleId, _roleName, _selectedPermissions);

            _showModal = false;
            await LoadRolesAsync();
            await Swal.FireAsync("Rol guardado", "Los permisos fueron actualizados correctamente.", SweetAlertIcon.Success);
        }
        catch (Exception ex)
        {
            await Swal.FireAsync("Error", ex.Message, SweetAlertIcon.Error);
        }
    }

    private async Task DeleteRoleAsync(RoleSummaryDto role)
    {
        var confirm = await Swal.FireAsync(new SweetAlertOptions
        {
            Title = $"¿Eliminar rol '{role.Name}'?",
            Text = "Esta acción eliminará el rol personalizado permanentemente.",
            Icon = SweetAlertIcon.Warning,
            ShowCancelButton = true,
            ConfirmButtonText = "Sí, eliminar",
            CancelButtonText = "Cancelar"
        });
        if (!confirm.IsConfirmed) return;

        try
        {
            await RoleManagementService.DeleteRoleAsync(role.Id);
            await LoadRolesAsync();
            await Swal.FireAsync("Rol eliminado", $"El rol '{role.Name}' fue eliminado.", SweetAlertIcon.Info);
        }
        catch (Exception ex)
        {
            await Swal.FireAsync("No permitido", ex.Message, SweetAlertIcon.Error);
        }
    }
}
