using EduMaster.UI.Billing.Tabs;
using EduMaster.UI.Common;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace EduMaster.UI.Billing;

public sealed class FinanceViewModel : BaseViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;
    private readonly ILogger<FinanceViewModel> _logger;

    public ObservableCollection<BaseViewModel> Tabs { get; } = new();

    private BaseViewModel? _selectedTab;
    public BaseViewModel? SelectedTab
    {
        get => _selectedTab;
        set => SetProperty(ref _selectedTab, value);
    }

    public AsyncRelayCommand RefreshCommand { get; }

    private DebtsTabViewModel? _debtsTab;
    private PaymentsTabViewModel? _paymentsTab;

    public FinanceViewModel(
        IServiceScopeFactory scopeFactory,
        IUserNotifier notifier,
        ILogger<FinanceViewModel> logger)
    {
        _scopeFactory = scopeFactory;
        _notifier = notifier;
        _logger = logger;

        RefreshCommand = new AsyncRelayCommand(RefreshAllAsync);
    }

    public async Task InitializeAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;

        _debtsTab = ActivatorUtilities.CreateInstance<DebtsTabViewModel>(sp);
        _paymentsTab = ActivatorUtilities.CreateInstance<PaymentsTabViewModel>(sp);

        _paymentsTab.SetInitialDates(DateTime.Today, DateTime.Today);

        Tabs.Clear();

        Tabs.Add(_debtsTab);
        Tabs.Add(_paymentsTab);

        SelectedTab = _debtsTab;

        await Task.WhenAll(
            _debtsTab.LoadDebtorsAsync(),
            _paymentsTab.LoadPaymentsAsync()
        );
    }

    private async Task RefreshAllAsync()
    {
        if (_debtsTab is not null)
            await _debtsTab.LoadDebtorsAsync();

        if (_paymentsTab is not null)
            await _paymentsTab.LoadPaymentsAsync();
    }
}