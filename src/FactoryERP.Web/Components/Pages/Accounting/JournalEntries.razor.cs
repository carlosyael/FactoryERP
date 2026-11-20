using CurrieTechnologies.Razor.SweetAlert2;
using FactoryERP.Core.Constants;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Interfaces;
using FactoryERP.Core.Models;
using Microsoft.AspNetCore.Components;

namespace FactoryERP.Web.Components.Pages.Accounting;

public partial class JournalEntries
{
    [Inject] private IJournalEntryService JournalService { get; set; } = default!;
    [Inject] private IChartOfAccountsService ChartService { get; set; } = default!;
    [Inject] private SweetAlertService Swal { get; set; } = default!;

    private List<JournalEntry> _entries = new();
    private List<AccountingAccount> _postableAccounts = new();
    private DateTime _startDate = DateTime.UtcNow.Date.AddMonths(-1);
    private DateTime _endDate = DateTime.UtcNow.Date;

    private bool _showExpense;
    private string _expAccountCode = AccountCodes.OperatingExpenses;
    private string _payAccountCode = AccountCodes.Cash;
    private decimal _expAmount;
    private string _expConcept = "";
    private string _expRef = "";

    private bool _showManual;
    private string _concept = "";
    private string _reference = "";
    private List<EditableLine> _manualLines = new();

    protected override async Task OnInitializedAsync()
    {
        await LoadEntriesAsync();
        _postableAccounts = await ChartService.GetPostableAccountsAsync();
    }

    private async Task LoadEntriesAsync()
        => _entries = await JournalService.GetAllAsync(_startDate, _endDate);

    private void OpenExpenseModal()
    {
        _expAmount = 0m;
        _expConcept = "";
        _expRef = $"EXP-{DateTime.Now:yyyyMMdd}";
        _showExpense = true;
    }

    private void OpenManualModal()
    {
        _concept = "";
        _reference = $"MAN-{DateTime.Now:yyyyMMdd}";
        int firstId = _postableAccounts.FirstOrDefault()?.Id ?? 0;
        _manualLines = [new() { AccountId = firstId }, new() { AccountId = firstId }];
        _showManual = true;
    }

    private void AddManualLine()
    {
        int firstId = _postableAccounts.FirstOrDefault()?.Id ?? 0;
        _manualLines.Add(new() { AccountId = firstId });
    }

    private async Task SaveExpenseAsync()
    {
        try
        {
            await JournalService.RecordExpenseAsync(_expAccountCode, _payAccountCode, _expAmount, _expConcept, _expRef);
            _showExpense = false;
            await LoadEntriesAsync();
            await Swal.FireAsync("Gasto registrado", "Asiento mayorizado correctamente.", SweetAlertIcon.Success);
        }
        catch (Exception ex) { await Swal.FireAsync("Error", ex.Message, SweetAlertIcon.Error); }
    }

    private async Task SaveManualAsync(bool postImmediately)
    {
        try
        {
            var req = new CreateJournalEntryRequest(
                DateTime.UtcNow, _concept, _reference, postImmediately,
                _manualLines.Select(l => new CreateJournalEntryLineRequest(l.AccountId, l.Debit, l.Credit, _concept)).ToList());
            await JournalService.CreateAsync(req);
            _showManual = false;
            await LoadEntriesAsync();
            await Swal.FireAsync(
                postImmediately ? "Asiento mayorizado" : "Borrador guardado",
                postImmediately ? "Registro contabilizado." : "El asiento quedó en borrador para revisión.",
                SweetAlertIcon.Success);
        }
        catch (Exception ex) { await Swal.FireAsync("Error", ex.Message, SweetAlertIcon.Error); }
    }

    private async Task PostEntryAsync(int entryId)
    {
        try
        {
            await JournalService.PostAsync(entryId);
            await LoadEntriesAsync();
            await Swal.FireAsync("Mayorizado", "Asiento contabilizado.", SweetAlertIcon.Success);
        }
        catch (Exception ex) { await Swal.FireAsync("Error", ex.Message, SweetAlertIcon.Error); }
    }

    private async Task VoidEntryAsync(int entryId)
    {
        var confirm = await Swal.FireAsync(new SweetAlertOptions
        {
            Title = "¿Anular asiento?",
            Text = "El asiento dejará de afectar los reportes financieros.",
            Icon = SweetAlertIcon.Warning,
            ShowCancelButton = true,
            ConfirmButtonText = "Sí, anular",
            CancelButtonText = "Cancelar"
        });
        if (!confirm.IsConfirmed) return;

        try
        {
            await JournalService.VoidAsync(entryId);
            await LoadEntriesAsync();
            await Swal.FireAsync("Asiento anulado", "El registro fue excluido de los reportes.", SweetAlertIcon.Info);
        }
        catch (Exception ex) { await Swal.FireAsync("Error", ex.Message, SweetAlertIcon.Error); }
    }

    private sealed class EditableLine
    {
        public int AccountId { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
    }
}
