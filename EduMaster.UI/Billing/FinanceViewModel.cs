using EduMaster.Application.Billing;
using EduMaster.Application.Common;
using EduMaster.Application.Printing;
using EduMaster.UI.Common;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using EduMaster.UI.Printing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace EduMaster.UI.Billing;

/// <summary>
/// شاشة «المالية» الرئيسية (4.3): قسم الديون (من عليهم متبقٍّ — بحث حي 300ms) + سجل المدفوعات بفلتر فترة
/// افتراضه اليوم — وعمود «غير مخصص» يُخرج الزائدة الدائنة من حبس الديالوغ إلى الضوء (فجوة مسجّلة).
/// قراءات فقط هنا — القبض من لوحة الطالب (4.2) والاسترجاع منها أيضاً.
/// 6.3 (ط-هـ): زر 🖨 على سطر السجل — إعادة طباعة دائمة لأن الإيصال وثيقة (D-105/D-130).
/// </summary>
public sealed class FinanceViewModel : BaseViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;
    private readonly ILogger<FinanceViewModel> _logger;
    private readonly IPrintService _printService;
    private readonly IDialogService _dialogs;
    private CancellationTokenSource? _debtorsCts;
    private CancellationTokenSource? _paymentsCts;
    private CancellationTokenSource? _searchCts;

    public FinanceViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier,
        ILogger<FinanceViewModel> logger, IPrintService printService, IDialogService dialogs)
    {
        _scopeFactory = scopeFactory;
        _notifier = notifier;
        _logger = logger;
        _printService = printService;
        _dialogs = dialogs;

        RefreshCommand = new AsyncRelayCommand(RefreshAllAsync);
        // بلا بوابة تفعيل عمداً: الزر المعطَّل يبتلع النقرة الأولى التي كان يجب أن تحدّد السطر (درس تجريب 6.3) —
        // التحديد يتم عند MouseDown قبل Click فيُطبع من أول نقرة، وغياب التحديد لا-عملية آمنة
        PrintReceiptCommand = new AsyncRelayCommand(PrintSelectedReceiptAsync);
        ReverseReceiptCommand = new AsyncRelayCommand(ReverseSelectedReceiptAsync);   // 6.6-ع-ب (ع-4): بلا بوابة — نفس درس زر الطباعة أعلاه
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

    /// <summary>سطر السجل المحدَّد — أوامر الطباعة بالتحديد لا بالمعاملات (قاعدة الواجهة)</summary>
    private PaymentListItem? _selectedPayment;
    public PaymentListItem? SelectedPayment
    {
        get => _selectedPayment;
        set => SetProperty(ref _selectedPayment, value);
    }

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
    public AsyncRelayCommand PrintReceiptCommand { get; }
    public AsyncRelayCommand ReverseReceiptCommand { get; }   // جديد 6.6-ع-ب

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

    /// <summary>
    /// 🖨 من سطر السجل (6.3 — ط-هـ): إعادة طباعة دائمة — الإيصال وثيقة (D-105).
    /// النموذج يُبنى بالمعالج النقي المختبَر (ط-2) ثم يُرسم ويُطبع · إلغاء نافذة الطباعة يمرّ بصمت (روح D-64).
    /// </summary>
    private async Task PrintSelectedReceiptAsync()
    {
        var selected = SelectedPayment;
        if (selected is null)
            return;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<GetReceiptPrintModelHandler>();
            var result = await handler.ExecuteAsync(selected.Id);

            if (!result.IsSuccess)
            {
                if (result.ErrorType == ErrorType.Unexpected)
                    _notifier.ShowError(result.ErrorMessage!);
                else
                    _notifier.ShowWarning(result.ErrorMessage!);   // «الإيصال غير موجود» — شاشة قائمة ← تحذيري (D-29)
                return;
            }

            if (_printService.PrintReceipt(result.Value!) == PrintOutcome.Failed)
                _notifier.ShowError("تعذّرت الطباعة — تحقق من الطابعة وأعد المحاولة.");
        }
        catch (Exception ex)   // D-69
        {
            _logger.LogError(ex, "Failed to print receipt for payment {PaymentId}", selected.Id);
            _notifier.ShowError("تعذّرت طباعة الإيصال — أعد المحاولة.");
        }
    }

    // 6.6-ع-ب (ع-4): عكس إيصال قبض خاطئ — تأكيد موثّق ثم المعالج ثم تحديث القسمين (السبب ثابت V1: «تصحيح خطأ إدخال»)
    private async Task ReverseSelectedReceiptAsync()
    {
        var selected = SelectedPayment;
        if (selected is null) return;

        var confirmed = await _dialogs.ConfirmAsync(
            "عكس إيصال قبض",
            $"سيُعكَس إيصال القبض #{selected.ReceiptNo:000000} ({selected.AmountCentimes / 100m:0.00} دج — {selected.StudentName}):\n\n" +
            "· يُكتب إيصال صرف معاكس بنفس المبلغ (يُصفَّر أثره النقدي)\n" +
            $"· تُفكّ تخصيصاته ({selected.AllocatedCentimes / 100m:0.00} دج) فتعود مستحقاته مفتوحة بمتبقيها الصحيح\n" +
            "· يبقى الإيصال الأصلي موثقاً في السجل — لا حذف إطلاقاً\n\n" +
            "السبب المسجَّل: «تصحيح خطأ إدخال».\n\nأعكس الإيصال؟",
            "↩ اعكس الإيصال");
        if (!confirmed) return;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<ReverseReceiptHandler>();
            var result = await handler.ExecuteAsync(new ReverseReceiptRequest(selected.Id, "تصحيح خطأ إدخال"));

            if (result.IsSuccess)
            {
                _notifier.ShowSuccess($"عُكس الإيصال ✔ — أُنشئ إيصال العكس #{result.Value:000000}");
                await RefreshAllAsync();
            }
            else if (result.ErrorType == ErrorType.Unexpected)
                _notifier.ShowError(result.ErrorMessage!);
            else
                _notifier.ShowWarning(result.ErrorMessage!);
        }
        catch (Exception ex)   // D-69
        {
            _logger.LogError(ex, "Failed to reverse receipt for payment {PaymentId}", selected.Id);
            _notifier.ShowError("تعذّر عكس الإيصال — أعد المحاولة.");
        }
    }
}