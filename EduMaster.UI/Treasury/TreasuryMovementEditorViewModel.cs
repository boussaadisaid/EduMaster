
using EduMaster.Application.Common;
using EduMaster.Application.Treasury;
using EduMaster.Domain.Treasury;
using EduMaster.UI.Common;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace EduMaster.UI.Treasury;

public sealed class TreasuryMovementEditorViewModel : BaseViewModel, IDialogViewModel
{
    private readonly IServiceScopeFactory _scopeFactory; private readonly IUserNotifier _notifier; private int? _editingId;
    public TreasuryMovementEditorViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier) { _scopeFactory = scopeFactory; _notifier = notifier; SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsSaving); CancelCommand = new AsyncRelayCommand(() => { CloseRequested?.Invoke(this, false); return Task.CompletedTask; }); }
    public event EventHandler<bool>? CloseRequested; public string Title => _editingId is null ? (IsIncome ? "دخل آخر" : "مصروف آخر") : "تعديل الحركة المالية";
    public ObservableCollection<TreasuryAccountItem> Accounts { get; } = new(); private TreasuryAccountItem? _selectedAccount; public TreasuryAccountItem? SelectedAccount { get => _selectedAccount; set => SetProperty(ref _selectedAccount, value); }
    private bool _isIncome; public bool IsIncome { get => _isIncome; private set { if (SetProperty(ref _isIncome, value)) { OnPropertyChanged(nameof(KindText)); OnPropertyChanged(nameof(Title)); } } }
    public string KindText => IsIncome ? "دخل آخر" : "مصروف آخر";
    private DateTime? _date = DateTime.Today; public DateTime? TransactionDate { get => _date; set => SetProperty(ref _date, value); }
    private string _amountText = ""; public string AmountText { get => _amountText; set => SetProperty(ref _amountText, value); }
    private string _note = ""; public string Note { get => _note; set => SetProperty(ref _note, value); }
    private string? _error; public string? ErrorMessage { get => _error; private set { SetProperty(ref _error, value); OnPropertyChanged(nameof(HasErrorMessage)); } }
    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);
    private bool _saving; public bool IsSaving { get => _saving; private set { if (SetProperty(ref _saving, value)) SaveCommand.RaiseCanExecuteChanged(); } }
    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand CancelCommand { get; }
    public void InitializeForCreate(bool income, int? accountId) { _editingId = null; IsIncome = income; TransactionDate = DateTime.Today; AmountText = ""; Note = ""; _ = LoadAccountsAsync(true, accountId); }
    public void InitializeForEdit(TreasuryMovementItem item) { _editingId = item.SourceId; IsIncome = item.TransactionKind == TreasuryTransactionKind.OtherIncome; TransactionDate = item.MovementDate.ToDateTime(TimeOnly.MinValue); AmountText = MoneyInput.FormatDinars(Math.Abs(item.DeltaCentimes)); Note = item.Note ?? ""; _ = LoadAccountsAsync(false, item.TreasuryAccountId); }
    private async Task LoadAccountsAsync(bool activeOnly, int? accountId) { await using var scope = _scopeFactory.CreateAsyncScope(); var r = await scope.ServiceProvider.GetRequiredService<GetTreasuryAccountsHandler>().ExecuteAsync(activeOnly); if (!r.IsSuccess) { _notifier.ShowError(r.ErrorMessage!); return; } Accounts.Clear(); foreach (var a in r.Value!) Accounts.Add(a); SelectedAccount = Accounts.FirstOrDefault(a => a.Id == accountId) ?? Accounts.FirstOrDefault(a => a.IsActive); OnPropertyChanged(nameof(Title)); }
    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (SelectedAccount is null)
        {
            ErrorMessage = "اختر الحساب المالي.";
            return;
        }

        if (TransactionDate is null)
        {
            ErrorMessage = "اختر تاريخ الحركة.";
            return;
        }

        if (TransactionDate.Value.Date > DateTime.Today)
        {
            ErrorMessage = "تاريخ الحركة لا يمكن أن يكون في المستقبل.";
            return;
        }

        if (!MoneyInput.TryParseDinars(AmountText, out var amount) || amount <= 0)
        {
            ErrorMessage = "أدخل مبلغاً صحيحاً أكبر من صفر.";
            return;
        }

        IsSaving = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var date = DateOnly.FromDateTime(TransactionDate.Value);
            var kind = IsIncome ? TreasuryTransactionKind.OtherIncome : TreasuryTransactionKind.OtherExpense;

            if (_editingId is null)
            {
                var result = await scope.ServiceProvider
                    .GetRequiredService<AddTreasuryTransactionHandler>()
                    .ExecuteAsync(new AddTreasuryTransactionRequest(
                        SelectedAccount.Id, date, kind, amount, Note));

                if (result.IsSuccess)
                {
                    _notifier.ShowSuccess("تم تسجيل الحركة المالية.");
                    CloseRequested?.Invoke(this, true);
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
                }
            }
            else
            {
                var result = await scope.ServiceProvider
                    .GetRequiredService<UpdateTreasuryTransactionHandler>()
                    .ExecuteAsync(new UpdateTreasuryTransactionRequest(
                        _editingId.Value, SelectedAccount.Id, date, kind, amount, Note));

                if (result.IsSuccess)
                {
                    _notifier.ShowSuccess("تم تعديل الحركة المالية.");
                    CloseRequested?.Invoke(this, true);
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
                }
            }
        }
        finally
        {
            IsSaving = false;
        }
    }
}
