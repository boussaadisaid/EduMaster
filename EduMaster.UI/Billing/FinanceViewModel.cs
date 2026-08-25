using EduMaster.Application.Billing;
using EduMaster.Application.Common;
using EduMaster.UI.Common;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace EduMaster.UI.Billing;

/// <summary>
/// شاشة «المالية» الرئيسية (4.3): قسم الديون (من عليهم متبقٍّ — بحث حي 300ms) + سجل المدفوعات بفلتر فترة
/// افتراضه اليوم — وعمود «غير مخصص» يُخرج الزائدة الدائنة من حبس الديالوغ إلى الضوء (فجوة مسجّلة).
/// قراءات فقط هنا — القبض من لوحة الطالب (4.2) والاسترجاع منها أيضاً.
/// </summary>
public sealed class FinanceViewModel : BaseViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;
    private readonly ILogger<FinanceViewModel> _logger;
    private CancellationTokenSource? _debtorsCts;
    private CancellationTokenSource? _paymentsCts;
    private CancellationTokenSource? _searchCts;

    public FinanceViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier, ILogger<FinanceViewModel> logger)
    {
        _scopeFactory = scopeFactory;
        _notifier = notifier;
        _logger = logger;

        RefreshCommand = new AsyncRelayCommand(RefreshAllAsync);
    }

    // ---------- قسم الديون ----------
    public ObservableCollection<DebtorItem> Debtors { get; } = new();

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            SetProperty(ref _searchText, value);
            _ = DebouncedDebtorSearchAsync();
        }
    }

    private bool _isLoadingDebtors;
    public bool IsLoadingDebtors
    {
        get => _isLoadingDebtors;
        private set { SetProperty(ref _isLoadingDebtors, value); OnPropertyChanged(nameof(DebtorsEmpty)); }
    }

    public bool DebtorsEmpty => !IsLoadingDebtors && Debtors.Count == 0;

    private string _totalDebtText = string.Empty;
    public string TotalDebtText
    {
        get => _totalDebtText;
        private set => SetProperty(ref _totalDebtText, value);
    }

    // ---------- قسم سجل المدفوعات ----------
    public ObservableCollection<PaymentListItem> Payments { get; } = new();

    private DateTime? _fromDate;
    public DateTime? FromDate
    {
        get => _fromDate;
        set
        {
            if (SetProperty(ref _fromDate, value))
                ReloadPayments();
        }
    }

    private DateTime? _toDate;
    public DateTime? ToDate
    {
        get => _toDate;
        set
        {
            if (SetProperty(ref _toDate, value))
                ReloadPayments();
        }
    }

    private bool _isLoadingPayments;
    public bool IsLoadingPayments
    {
        get => _isLoadingPayments;
        private set { SetProperty(ref _isLoadingPayments, value); OnPropertyChanged(nameof(PaymentsEmpty)); }
    }

    public bool PaymentsEmpty => !IsLoadingPayments && Payments.Count == 0;

    private string _logSummaryText = string.Empty;
    public string LogSummaryText
    {
        get => _logSummaryText;
        private set => SetProperty(ref _logSummaryText, value);
    }

    public AsyncRelayCommand RefreshCommand { get; }

    public async Task InitializeAsync()
    {
        // الافتراضي: اليوم — حقلاً مباشرةً بلا إطلاق مزدوج ثم تحميل واحد (نمط شاشة الحصص)
        _fromDate = DateTime.Today;
        _toDate = DateTime.Today;
        OnPropertyChanged(nameof(FromDate));
        OnPropertyChanged(nameof(ToDate));

        await LoadDebtorsAsync();
        await LoadPaymentsAsync();
    }

    private async Task RefreshAllAsync()
    {
        await LoadDebtorsAsync();
        ReloadPayments();
    }

    private async Task DebouncedDebtorSearchAsync()
    {
        _searchCts?.Cancel();
        var cts = _searchCts = new CancellationTokenSource();

        try
        {
            await Task.Delay(300, cts.Token);   // D-33
            await LoadDebtorsAsync(cts.Token);
        }
        catch (OperationCanceledException) { }
    }

    private async Task LoadDebtorsAsync(CancellationToken cancellationToken = default)
    {
        IsLoadingDebtors = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<GetDebtorsHandler>();
            var result = await handler.ExecuteAsync(SearchText, cancellationToken);

            if (result.IsSuccess)
            {
                Debtors.Clear();
                foreach (var item in result.Value!)
                    Debtors.Add(item);

                // ملخص حي: إجمالي الديون القائمة وعدد المدينين
                var total = Debtors.Sum(d => d.RemainingCentimes);
                TotalDebtText = Debtors.Count == 0
                    ? "لا ديون قائمة 🎉"
                    : $"إجمالي الديون القائمة: {MoneyInput.FormatDinars(total)} دج — {Debtors.Count} طالب";
            }
            else if (!cancellationToken.IsCancellationRequested)
            {
                _notifier.ShowError(result.ErrorMessage!);
            }
        }
        catch (OperationCanceledException) { }   // D-64
        catch (Exception ex)                     // D-69: قناة fire-and-forget محصّنة
        {
            _logger.LogError(ex, "Failed to load debtors");
            _notifier.ShowError("تعذّر تحميل قائمة الديون — أعد المحاولة.");
        }
        finally
        {
            IsLoadingDebtors = false;
        }
    }

    private void ReloadPayments()
    {
        _paymentsCts?.Cancel();   // D-64: تبديل الفلاتر فوري — يلغي التحميل السابق
        var cts = _paymentsCts = new CancellationTokenSource();
        _ = LoadPaymentsAsync(cts.Token);
    }

    private async Task LoadPaymentsAsync(CancellationToken cancellationToken = default)
    {
        if (FromDate is null || ToDate is null)
            return;

        IsLoadingPayments = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<GetPaymentsLogHandler>();
            var result = await handler.ExecuteAsync(
                DateOnly.FromDateTime(FromDate.Value), DateOnly.FromDateTime(ToDate.Value), cancellationToken);

            if (result.IsSuccess)
            {
                Payments.Clear();
                foreach (var item in result.Value!)
                    Payments.Add(item);

                // ملخص الفترة: مقبوض · مصروف · غير مخصص (الزائدة الدائنة ظاهرة هنا — خارجة من حبس الديالوغ)
                var receipts = Payments.Where(p => p.Kind == Domain.Enums.PaymentKind.Receipt).Sum(p => p.AmountCentimes);
                var refunds = Payments.Where(p => p.Kind == Domain.Enums.PaymentKind.Refund).Sum(p => p.AmountCentimes);
                var unallocated = Payments.Sum(p => p.UnallocatedCentimes);
                LogSummaryText = $"مقبوض: {MoneyInput.FormatDinars(receipts)} دج · مصروف: {MoneyInput.FormatDinars(refunds)} دج · غير مخصص: {MoneyInput.FormatDinars(unallocated)} دج";
            }
            else if (!cancellationToken.IsCancellationRequested)
            {
                if (result.ErrorType == ErrorType.Unexpected)
                    _notifier.ShowError(result.ErrorMessage!);
                else
                    _notifier.ShowWarning(result.ErrorMessage!);   // «من بعد إلى» — شاشة قائمة ← تحذيري (D-29)
            }
        }
        catch (OperationCanceledException) { }   // D-64
        catch (Exception ex)                     // D-69
        {
            _logger.LogError(ex, "Failed to load payments log");
            _notifier.ShowError("تعذّر تحميل سجل المدفوعات — أعد المحاولة.");
        }
        finally
        {
            IsLoadingPayments = false;
        }
    }
}