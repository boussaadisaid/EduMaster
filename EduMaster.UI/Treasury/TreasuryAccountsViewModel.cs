
using EduMaster.Application.Treasury;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace EduMaster.UI.Treasury;

public sealed class TreasuryAccountsViewModel : BaseViewModel, IDialogViewModel
{
    private readonly IServiceScopeFactory _scopeFactory; private readonly IUserNotifier _notifier; private readonly IDialogService _dialogs; private bool _changed;
    public TreasuryAccountsViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier, IDialogService dialogs) { _scopeFactory = scopeFactory; _notifier = notifier; _dialogs = dialogs; NewCommand = new AsyncRelayCommand(NewAsync); EditCommand = new AsyncRelayCommand(EditAsync, () => SelectedAccount is not null); ToggleCommand = new AsyncRelayCommand(ToggleAsync, () => SelectedAccount is not null); CloseCommand = new AsyncRelayCommand(() => { CloseRequested?.Invoke(this, _changed); return Task.CompletedTask; }); }
    public event EventHandler<bool>? CloseRequested; public ObservableCollection<TreasuryAccountItem> Accounts { get; } = new(); private TreasuryAccountItem? _selected; public TreasuryAccountItem? SelectedAccount { get => _selected; set { if (SetProperty(ref _selected, value)) { EditCommand.RaiseCanExecuteChanged(); ToggleCommand.RaiseCanExecuteChanged(); } } }
    private bool _loading; public bool IsLoading { get => _loading; private set => SetProperty(ref _loading, value); }
    public AsyncRelayCommand NewCommand { get; }
    public AsyncRelayCommand EditCommand { get; }
    public AsyncRelayCommand ToggleCommand { get; }
    public AsyncRelayCommand CloseCommand { get; }
    public async Task InitializeAsync() { await LoadAsync(); }
    private async Task LoadAsync() { IsLoading = true; try { await using var scope = _scopeFactory.CreateAsyncScope(); var r = await scope.ServiceProvider.GetRequiredService<GetTreasuryAccountsHandler>().ExecuteAsync(false); if (!r.IsSuccess) { _notifier.ShowError(r.ErrorMessage!); return; } Accounts.Clear(); foreach (var a in r.Value!) Accounts.Add(a); SelectedAccount = null; } finally { IsLoading = false; } }
    private async Task NewAsync() { using var scope = _scopeFactory.CreateScope(); var vm = scope.ServiceProvider.GetRequiredService<TreasuryAccountEditorViewModel>(); vm.InitializeForCreate(); if (await _dialogs.ShowDialogAsync(vm, "حساب مالي جديد")) { _changed = true; await LoadAsync(); } }
    private async Task EditAsync() { if (SelectedAccount is null) return; using var scope = _scopeFactory.CreateScope(); var vm = scope.ServiceProvider.GetRequiredService<TreasuryAccountEditorViewModel>(); vm.InitializeForEdit(SelectedAccount); if (await _dialogs.ShowDialogAsync(vm, "تعديل الحساب المالي")) { _changed = true; await LoadAsync(); } }
    private async Task ToggleAsync() { if (SelectedAccount is null) return; var title = SelectedAccount.IsActive ? "تعطيل الحساب المالي" : "تفعيل الحساب المالي"; if (!await _dialogs.ConfirmAsync(title, SelectedAccount.IsActive ? "هل تريد تعطيل هذا الحساب؟" : "هل تريد تفعيل هذا الحساب؟", title)) return; await using var scope = _scopeFactory.CreateAsyncScope(); var r = SelectedAccount.IsActive ? await scope.ServiceProvider.GetRequiredService<DeactivateTreasuryAccountHandler>().ExecuteAsync(new SetTreasuryAccountActiveRequest(SelectedAccount.Id)) : await scope.ServiceProvider.GetRequiredService<ActivateTreasuryAccountHandler>().ExecuteAsync(new SetTreasuryAccountActiveRequest(SelectedAccount.Id)); if (r.IsSuccess) { _changed = true; await LoadAsync(); } else _notifier.ShowWarning(r.ErrorMessage!); }
}
