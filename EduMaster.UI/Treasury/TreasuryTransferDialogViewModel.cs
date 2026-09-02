
using EduMaster.Application.Treasury;
using EduMaster.UI.Common;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace EduMaster.UI.Treasury;

public sealed class TreasuryTransferDialogViewModel : BaseViewModel, IDialogViewModel
{
    private readonly IServiceScopeFactory _scopeFactory; private readonly IUserNotifier _notifier;
    public TreasuryTransferDialogViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier) { _scopeFactory = scopeFactory; _notifier = notifier; SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsSaving); CancelCommand = new AsyncRelayCommand(() => { CloseRequested?.Invoke(this, false); return Task.CompletedTask; }); }
    public event EventHandler<bool>? CloseRequested; public ObservableCollection<TreasuryAccountItem> Accounts { get; } = new();
    private TreasuryAccountItem? _from; public TreasuryAccountItem? FromAccount { get => _from; set => SetProperty(ref _from, value); }
    private TreasuryAccountItem? _to; public TreasuryAccountItem? ToAccount { get => _to; set => SetProperty(ref _to, value); }
    private DateTime? _date = DateTime.Today; public DateTime? TransferDate { get => _date; set => SetProperty(ref _date, value); }
    private string _amountText = ""; public string AmountText { get => _amountText; set => SetProperty(ref _amountText, value); }
    private string _note = ""; public string Note { get => _note; set => SetProperty(ref _note, value); }
    private string? _error; public string? ErrorMessage { get => _error; private set { SetProperty(ref _error, value); OnPropertyChanged(nameof(HasErrorMessage)); } }
    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage); private bool _saving; public bool IsSaving { get => _saving; private set { if (SetProperty(ref _saving, value)) SaveCommand.RaiseCanExecuteChanged(); } }
    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand CancelCommand { get; }
    public void Initialize(int? preferredFrom) { _ = LoadAsync(preferredFrom); }
    private async Task LoadAsync(int? preferredFrom) { await using var scope = _scopeFactory.CreateAsyncScope(); var r = await scope.ServiceProvider.GetRequiredService<GetTreasuryAccountsHandler>().ExecuteAsync(true); if (!r.IsSuccess) { _notifier.ShowError(r.ErrorMessage!); return; } Accounts.Clear(); foreach (var a in r.Value!) Accounts.Add(a); FromAccount = Accounts.FirstOrDefault(a => a.Id == preferredFrom) ?? Accounts.FirstOrDefault(); ToAccount = Accounts.FirstOrDefault(a => a.Id != FromAccount?.Id); }
    private async Task SaveAsync() { ErrorMessage = null; if (FromAccount is null || ToAccount is null) { ErrorMessage = "اختر الحساب المصدر والحساب المستفيد."; return; } if (FromAccount.Id == ToAccount.Id) { ErrorMessage = "لا يمكن التحويل إلى الحساب نفسه."; return; } if (TransferDate is null) { ErrorMessage = "اختر تاريخ التحويل."; return; } if (TransferDate.Value.Date > DateTime.Today) { ErrorMessage = "تاريخ التحويل لا يمكن أن يكون في المستقبل."; return; } if (!MoneyInput.TryParseDinars(AmountText, out var amount) || amount <= 0) { ErrorMessage = "أدخل مبلغاً صحيحاً أكبر من صفر."; return; } IsSaving = true; try { await using var scope = _scopeFactory.CreateAsyncScope(); var r = await scope.ServiceProvider.GetRequiredService<AddTreasuryTransferHandler>().ExecuteAsync(new AddTreasuryTransferRequest(FromAccount.Id, ToAccount.Id, DateOnly.FromDateTime(TransferDate.Value), amount, Note)); if (r.IsSuccess) { _notifier.ShowSuccess("تم تنفيذ التحويل المالي."); CloseRequested?.Invoke(this, true); } else ErrorMessage = r.ErrorMessage; } finally { IsSaving = false; } }
}
