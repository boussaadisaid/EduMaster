using EduMaster.Application.Billing;
using EduMaster.UI.Common;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace EduMaster.UI.Billing.Tabs;

public sealed class DebtsTabViewModel : BaseViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;
    private readonly ILogger<DebtsTabViewModel> _logger;

    private CancellationTokenSource? _searchCts;

    public DebtsTabViewModel(
        IServiceScopeFactory scopeFactory,
        IUserNotifier notifier,
        ILogger<DebtsTabViewModel> logger)
    {
        _scopeFactory = scopeFactory;
        _notifier = notifier;
        _logger = logger;
    }

    public string DisplayName => "📋 الديون القائمة";

    public ObservableCollection<DebtorItem> Debtors { get; } = new();

    private string _searchText = string.Empty;

    public string SearchText
    {
        get => _searchText;

        set
        {
            if (!SetProperty(ref _searchText, value))
                return;

            _ = DebouncedDebtorSearchAsync();
        }
    }

    private bool _isLoadingDebtors;

    public bool IsLoadingDebtors
    {
        get => _isLoadingDebtors;

        private set
        {
            if (SetProperty(ref _isLoadingDebtors, value))
                OnPropertyChanged(nameof(DebtorsEmpty));
        }
    }

    public bool DebtorsEmpty =>
        !IsLoadingDebtors && Debtors.Count == 0;

    private string _totalDebtText = string.Empty;

    public string TotalDebtText
    {
        get => _totalDebtText;
        private set => SetProperty(ref _totalDebtText, value);
    }

    public async Task LoadDebtorsAsync(
        CancellationToken cancellationToken = default)
    {
        IsLoadingDebtors = true;

        try
        {
            await using var scope =
                _scopeFactory.CreateAsyncScope();

            var handler =
                scope.ServiceProvider
                    .GetRequiredService<GetDebtorsHandler>();

            var result =
                await handler.ExecuteAsync(
                    SearchText,
                    cancellationToken);

            if (result.IsSuccess)
            {
                Debtors.Clear();

                foreach (var item in result.Value!)
                    Debtors.Add(item);

                var total =
                    Debtors.Sum(d => d.RemainingCentimes);

                TotalDebtText =
                    Debtors.Count == 0
                        ? "لا ديون قائمة 🎉"
                        : $"إجمالي الديون القائمة: {MoneyInput.FormatDinars(total)} دج — {Debtors.Count} طالب";
            }
            else if (!cancellationToken.IsCancellationRequested)
            {
                _notifier.ShowError(result.ErrorMessage!);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to load debtors");

            _notifier.ShowError(
                "تعذّر تحميل قائمة الديون — أعد المحاولة.");
        }
        finally
        {
            IsLoadingDebtors = false;
        }
    }

    private async Task DebouncedDebtorSearchAsync()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();

        var cts =
            _searchCts = new CancellationTokenSource();

        try
        {
            await Task.Delay(300, cts.Token);

            await LoadDebtorsAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
    }
}