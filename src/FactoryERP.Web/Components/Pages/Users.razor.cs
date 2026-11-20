using CurrieTechnologies.Razor.SweetAlert2;
using FactoryERP.Core.Interfaces;
using FactoryERP.Core.Models;
using Microsoft.AspNetCore.Components;

namespace FactoryERP.Web.Components.Pages;

public partial class Users
{
    [Inject] private IUserManagementService UserManagementService { get; set; } = default!;
    [Inject] private IRoleManagementService RoleManagementService { get; set; } = default!;
    [Inject] private SweetAlertService Swal { get; set; } = default!;

    private List<UserSummaryDto> _users = new();
    private List<RoleSummaryDto> _availableRoles = new();

    private bool _showUserModal;
    private string? _editingUserId;
    private string _fullName = string.Empty;
    private string _email = string.Empty;
    private string _password = string.Empty;
    private readonly HashSet<string> _selectedRoles = new(StringComparer.OrdinalIgnoreCase);

    private bool _showResetModal;
    private UserSummaryDto? _resetTarget;
    private string _newPassword = string.Empty;

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        _users = await UserManagementService.GetAllUsersAsync();
        _availableRoles = await RoleManagementService.GetAllRolesAsync();
    }

    private void OpenCreateModal()
    {
        _editingUserId = null;
        _fullName = string.Empty;
        _email = string.Empty;
        _password = string.Empty;
        _selectedRoles.Clear();
        if (_availableRoles.Count > 0) _selectedRoles.Add(_availableRoles[0].Name);
        _showUserModal = true;
    }

    private void OpenEditModal(UserSummaryDto user)
    {
        _editingUserId = user.Id;
        _fullName = user.FullName;
        _email = user.Email;
        _password = string.Empty;
        _selectedRoles.Clear();
        foreach (string r in user.Roles) _selectedRoles.Add(r);
        _showUserModal = true;
    }

    private void OpenResetModal(UserSummaryDto user)
    {
        _resetTarget = user;
        _newPassword = string.Empty;
        _showResetModal = true;
    }

    private void ToggleRoleSelection(string roleName, bool selected)
    {
        if (selected) _selectedRoles.Add(roleName);
        else _selectedRoles.Remove(roleName);
    }

    private async Task SaveUserAsync()
    {
        try
        {
            var roles = _selectedRoles.ToList();
            if (_editingUserId is null)
                await UserManagementService.CreateUserAsync(new CreateUserRequest(_fullName, _email, _password, roles));
            else
                await UserManagementService.UpdateUserAsync(new UpdateUserRequest(_editingUserId, _fullName, roles));

            _showUserModal = false;
            await LoadAsync();
            await Swal.FireAsync("Guardado", "Usuario y roles actualizados.", SweetAlertIcon.Success);
        }
        catch (Exception ex)
        {
            await Swal.FireAsync("Error", ex.Message, SweetAlertIcon.Error);
        }
    }

    private async Task ConfirmResetPasswordAsync()
    {
        if (_resetTarget is null) return;
        try
        {
            await UserManagementService.ResetPasswordAsync(_resetTarget.Id, _newPassword);
            _showResetModal = false;
            await Swal.FireAsync("Clave actualizada", $"Se restableció la contraseña de {_resetTarget.FullName}.", SweetAlertIcon.Success);
        }
        catch (Exception ex)
        {
            await Swal.FireAsync("Error", ex.Message, SweetAlertIcon.Error);
        }
    }

    private async Task ToggleActiveAsync(UserSummaryDto user)
    {
        try
        {
            await UserManagementService.ToggleActiveAsync(user.Id);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await Swal.FireAsync("Acción bloqueada", ex.Message, SweetAlertIcon.Warning);
        }
    }
}
