
using EduMaster.Application.Treasury;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace EduMaster.UI.Treasury;

public sealed class TreasuryViewModel : BaseViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;
    private readonly IDialogService _dialogs;
    private bool _suppressReload;
    public TreasuryViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier, IDialogService dialogs)
    {
        _scopeFactory = scopeFactory; _notifier = notifier; _dialogs = dialogs;
        TodayCommand = new AsyncRelayCommand(() => SetPeriodAsync(DateTime.Today, DateTime.Today));
        WeekCommand = new AsyncRelayCommand(() => SetPeriodAsync(DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 1) % 7)), DateTime.Today));
        MonthCommand = new AsyncRelayCommand(() => SetPeriodAsync(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1), DateTime.Today));
        RefreshCommand = new AsyncRelayCommand(LoadAsync, () => !IsLoading);
        AddIncomeCommand = new AsyncRelayCommand(() => OpenMovementAsync(true));
        AddExpenseCommand = new AsyncRelayCommand(() => OpenMovementAsync(false));
        TransferCommand = new AsyncRelayCommand(OpenTransferAsync);
        ManageAccountsCommand = new AsyncRelayCommand(OpenAccountsAsync);
    }
    public ObservableCollection<TreasuryAccountItem> Accounts { get; } = new();
    private TreasuryAccountItem? _selectedAccount;
    public TreasuryAccountItem? SelectedAccount { get => _selectedAccount; set { if (SetProperty(ref _selectedAccount, value) && !_suppressReload) _ = LoadAsync(); } }
    public ObservableCollection<TreasuryMovementItem> Movements { get; } = new();
    private DateTime? _fromDate = DateTime.Today;
    public DateTime? FromDate { get => _fromDate; set { if (SetProperty(ref _fromDate, value) && !_suppressReload) _ = LoadAsync(); } }
    private DateTime? _toDate = DateTime.Today;
    public DateTime? ToDate { get => _toDate; set { if (SetProperty(ref _toDate, value) && !_suppressReload) _ = LoadAsync(); } }
    private TreasurySummaryItem _summary = new(0, 0, 0, 0, 0);
    public string OpeningBalanceText => Money(_summary.OpeningBalanceCentimes); public string PeriodIncomingText => Money(_summary.PeriodIncomingCentimes); public string PeriodOutgoingText => Money(_summary.PeriodOutgoingCentimes); public string PeriodNetText => Money(_summary.PeriodNetCentimes); public string ClosingBalanceText => Money(_summary.ClosingBalanceCentimes);
    private bool _isLoading; public bool IsLoading { get => _isLoading; private set { if (SetProperty(ref _isLoading, value)) RefreshCommand.RaiseCanExecuteChanged(); } }
    public bool IsEmpty => !IsLoading && Movements.Count == 0;
    public AsyncRelayCommand TodayCommand { get; }
    public AsyncRelayCommand WeekCommand { get; }
    public AsyncRelayCommand MonthCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand AddIncomeCommand { get; }
    public AsyncRelayCommand AddExpenseCommand { get; }
    public AsyncRelayCommand TransferCommand { get; }
    public AsyncRelayCommand ManageAccountsCommand { get; }
    public async Task InitializeAsync() { await LoadAccountsAsync(); await LoadAsync(); }
    private async Task LoadAccountsAsync() { await using var scope = _scopeFactory.CreateAsyncScope(); var r = await scope.ServiceProvider.GetRequiredService<GetTreasuryAccountsHandler>().ExecuteAsync(false); if (!r.IsSuccess) { _notifier.ShowError(r.ErrorMessage!); return; } _suppressReload = true; Accounts.Clear(); Accounts.Add(new TreasuryAccountItem(0, "كل الحسابات", true, 0)); foreach (var a in r.Value!) Accounts.Add(a); SelectedAccount = Accounts.FirstOrDefault(a => a.Id == 0); _suppressReload = false; }
    private async Task LoadAsync() { if (FromDate is null || ToDate is null || FromDate > ToDate) return; IsLoading = true; try { await using var scope = _scopeFactory.CreateAsyncScope(); var aid = SelectedAccount?.Id is > 0 ? SelectedAccount.Id : (int?)null; var from = DateOnly.FromDateTime(FromDate.Value); var to = DateOnly.FromDateTime(ToDate.Value); var rh = scope.ServiceProvider.GetRequiredService<GetTreasuryMovementsHandler>(); var sh = scope.ServiceProvider.GetRequiredService<GetTreasurySummaryHandler>(); var rr = await rh.ExecuteAsync(aid, from, to); if (rr.IsSuccess) { Movements.Clear(); foreach (var m in rr.Value!) Movements.Add(m); } var sr = await sh.ExecuteAsync(aid, from, to); if (sr.IsSuccess) { _summary = sr.Value!; OnPropertyChanged(nameof(OpeningBalanceText)); OnPropertyChanged(nameof(PeriodIncomingText)); OnPropertyChanged(nameof(PeriodOutgoingText)); OnPropertyChanged(nameof(PeriodNetText)); OnPropertyChanged(nameof(ClosingBalanceText)); } OnPropertyChanged(nameof(IsEmpty)); } catch { _notifier.ShowError("تعذّر تحميل الخزينة."); } finally { IsLoading = false; OnPropertyChanged(nameof(IsEmpty)); } }
    private async Task SetPeriodAsync(DateTime from, DateTime to) { _suppressReload = true; FromDate = from; ToDate = to; _suppressReload = false; await LoadAsync(); }
    private async Task OpenMovementAsync(bool income) { using var scope = _scopeFactory.CreateScope(); var vm = scope.ServiceProvider.GetRequiredService<TreasuryMovementEditorViewModel>(); vm.InitializeForCreate(income, SelectedAccount?.Id > 0 ? SelectedAccount.Id : null); if (await _dialogs.ShowDialogAsync(vm, income ? "دخل آخر" : "مصروف آخر")) await LoadAsync(); }
    private async Task OpenTransferAsync() { using var scope = _scopeFactory.CreateScope(); var vm = scope.ServiceProvider.GetRequiredService<TreasuryTransferDialogViewModel>(); vm.Initialize(SelectedAccount?.Id > 0 ? SelectedAccount.Id : null); if (await _dialogs.ShowDialogAsync(vm, "تحويل بين الحسابات")) await LoadAsync(); }
    private async Task OpenAccountsAsync() { using var scope = _scopeFactory.CreateScope(); var vm = scope.ServiceProvider.GetRequiredService<TreasuryAccountsViewModel>(); if (await _dialogs.ShowDialogAsync(vm, "الحسابات المالية")) await InitializeAsync(); }
    private static string Money(long c) => $"{c / 100m:0.00} دج";
}
