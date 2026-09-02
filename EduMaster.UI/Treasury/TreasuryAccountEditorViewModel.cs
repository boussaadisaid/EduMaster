
using EduMaster.Application.Treasury;
using EduMaster.UI.Common;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EduMaster.UI.Treasury;

public sealed class TreasuryAccountEditorViewModel : BaseViewModel, IDialogViewModel
{
    private readonly IServiceScopeFactory _scopeFactory; private readonly IUserNotifier _notifier; private int? _editingId;
    public TreasuryAccountEditorViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier) { _scopeFactory = scopeFactory; _notifier = notifier; SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsSaving); CancelCommand = new AsyncRelayCommand(() => { CloseRequested?.Invoke(this, false); return Task.CompletedTask; }); }
    public event EventHandler<bool>? CloseRequested; public string Title => _editingId is null ? "حساب مالي جديد" : "تعديل الحساب المالي"; private string _name = ""; public string Name { get => _name; set => SetProperty(ref _name, value); }
    private string _opening = ""; public string OpeningBalanceText { get => _opening; set => SetProperty(ref _opening, value); }
    private string? _error; public string? ErrorMessage { get => _error; private set { SetProperty(ref _error, value); OnPropertyChanged(nameof(HasErrorMessage)); } }
    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage); private bool _saving; public bool IsSaving { get => _saving; private set { if (SetProperty(ref _saving, value)) SaveCommand.RaiseCanExecuteChanged(); } }
    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand CancelCommand { get; }
    public void InitializeForCreate() { _editingId = null; Name = ""; OpeningBalanceText = "0"; ErrorMessage = null; }
    public void InitializeForEdit(TreasuryAccountItem item) { _editingId = item.Id; Name = item.Name; OpeningBalanceText = MoneyInput.FormatDinars(item.OpeningBalanceCentimes); ErrorMessage = null; }
    private async Task SaveAsync() { ErrorMessage = null; if (string.IsNullOrWhiteSpace(Name)) { ErrorMessage = "أدخل اسم الحساب المالي."; return; } if (!MoneyInput.TryParseDinars(OpeningBalanceText, out var opening) || opening < 0) { ErrorMessage = "أدخل رصيداً افتتاحياً صحيحاً غير سالب."; return; } IsSaving = true; try { await using var scope = _scopeFactory.CreateAsyncScope(); if (_editingId is null) { var r = await scope.ServiceProvider.GetRequiredService<CreateTreasuryAccountHandler>().ExecuteAsync(new CreateTreasuryAccountRequest(Name, opening)); if (r.IsSuccess) { _notifier.ShowSuccess("تم إنشاء الحساب المالي."); CloseRequested?.Invoke(this, true); } else ErrorMessage = r.ErrorMessage; } else { var r = await scope.ServiceProvider.GetRequiredService<UpdateTreasuryAccountHandler>().ExecuteAsync(new UpdateTreasuryAccountRequest(_editingId.Value, Name, opening)); if (r.IsSuccess) { _notifier.ShowSuccess("تم تعديل الحساب المالي."); CloseRequested?.Invoke(this, true); } else ErrorMessage = r.ErrorMessage; } } finally { IsSaving = false; } }
}
