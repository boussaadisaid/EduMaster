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

namespace EduMaster.UI.Billing.Tabs;

public sealed class PaymentsTabViewModel : BaseViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;
    private readonly ILogger<PaymentsTabViewModel> _logger;
    private readonly IPrintService _printService;
    private readonly IDialogService _dialogs;

    private CancellationTokenSource? _paymentsCts;

    public PaymentsTabViewModel(
        IServiceScopeFactory scopeFactory,
        IUserNotifier notifier,
        ILogger<PaymentsTabViewModel> logger,
        IPrintService printService,
        IDialogService dialogs)
    {
        _scopeFactory = scopeFactory;
        _notifier = notifier;
        _logger = logger;
        _printService = printService;
        _dialogs = dialogs;

        PrintReceiptCommand =
            new AsyncRelayCommand(PrintSelectedReceiptAsync);

        ReverseReceiptCommand =
            new AsyncRelayCommand(ReverseSelectedReceiptAsync);
    }

    public string DisplayName => "💰 سجل المدفوعات";

    public ObservableCollection<PaymentListItem> Payments { get; } = new();

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

        private set
        {
            if (SetProperty(ref _isLoadingPayments, value))
                OnPropertyChanged(nameof(PaymentsEmpty));
        }
    }

    public bool PaymentsEmpty =>
        !IsLoadingPayments && Payments.Count == 0;

    private string _logSummaryText = string.Empty;

    public string LogSummaryText
    {
        get => _logSummaryText;
        private set => SetProperty(ref _logSummaryText, value);
    }

    public AsyncRelayCommand PrintReceiptCommand { get; }

    public AsyncRelayCommand ReverseReceiptCommand { get; }

    public void SetInitialDates(
        DateTime from,
        DateTime to)
    {
        _fromDate = from;
        _toDate = to;

        OnPropertyChanged(nameof(FromDate));
        OnPropertyChanged(nameof(ToDate));
    }

    public void ReloadPayments()
    {
        _paymentsCts?.Cancel();
        _paymentsCts?.Dispose();

        var cts = new CancellationTokenSource();

        _paymentsCts = cts;

        _ = LoadPaymentsAsync(cts.Token);
    }

    public async Task LoadPaymentsAsync(
        CancellationToken cancellationToken = default)
    {
        if (FromDate is null || ToDate is null)
            return;

        IsLoadingPayments = true;

        try
        {
            await using var scope =
                _scopeFactory.CreateAsyncScope();

            var handler =
                scope.ServiceProvider
                    .GetRequiredService<GetPaymentsLogHandler>();

            var result =
                await handler.ExecuteAsync(
                    DateOnly.FromDateTime(FromDate.Value),
                    DateOnly.FromDateTime(ToDate.Value),
                    cancellationToken);

            if (result.IsSuccess)
            {
                Payments.Clear();

                foreach (var item in result.Value!)
                    Payments.Add(item);

                var receipts =
                    Payments
                        .Where(p =>
                            p.Kind ==
                            Domain.Enums.PaymentKind.Receipt)
                        .Sum(p => p.AmountCentimes);

                var refunds =
                    Payments
                        .Where(p =>
                            p.Kind ==
                            Domain.Enums.PaymentKind.Refund)
                        .Sum(p => p.AmountCentimes);

                var unallocated =
                    Payments.Sum(p => p.UnallocatedCentimes);

                LogSummaryText =
                    $"مقبوض: {MoneyInput.FormatDinars(receipts)} دج · " +
                    $"مصروف: {MoneyInput.FormatDinars(refunds)} دج · " +
                    $"غير مخصص: {MoneyInput.FormatDinars(unallocated)} دج";
            }
            else if (!cancellationToken.IsCancellationRequested)
            {
                if (result.ErrorType == ErrorType.Unexpected)
                    _notifier.ShowError(result.ErrorMessage!);
                else
                    _notifier.ShowWarning(result.ErrorMessage!);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to load payments log");

            _notifier.ShowError(
                "تعذّر تحميل سجل المدفوعات — أعد المحاولة.");
        }
        finally
        {
            IsLoadingPayments = false;
        }
    }

    private async Task PrintSelectedReceiptAsync()
    {
        var selected = SelectedPayment;

        if (selected is null)
            return;

        try
        {
            await using var scope =
                _scopeFactory.CreateAsyncScope();

            var handler =
                scope.ServiceProvider
                    .GetRequiredService<GetReceiptPrintModelHandler>();

            var result =
                await handler.ExecuteAsync(selected.Id);

            if (!result.IsSuccess)
            {
                if (result.ErrorType == ErrorType.Unexpected)
                    _notifier.ShowError(result.ErrorMessage!);
                else
                    _notifier.ShowWarning(result.ErrorMessage!);

                return;
            }

            if (_printService.PrintReceipt(result.Value!) ==
                PrintOutcome.Failed)
            {
                _notifier.ShowError(
                    "تعذّرت الطباعة — تحقق من الطابعة وأعد المحاولة.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to print receipt for payment {PaymentId}",
                selected.Id);

            _notifier.ShowError(
                "تعذّرت طباعة الإيصال — أعد المحاولة.");
        }
    }

    private async Task ReverseSelectedReceiptAsync()
    {
        var selected = SelectedPayment;

        if (selected is null)
            return;

        var confirmed =
            await _dialogs.ConfirmAsync(
                "عكس إيصال قبض",
                $"سيُعكَس إيصال القبض #{selected.ReceiptNo:000000} " +
                $"({selected.AmountCentimes / 100m:0.00} دج — {selected.StudentName}):\n\n" +
                "· يُكتب إيصال صرف معاكس بنفس المبلغ (يُصفَّر أثره النقدي)\n" +
                $"· تُفكّ تخصيصاته ({selected.AllocatedCentimes / 100m:0.00} دج) " +
                "فتعود مستحقاته مفتوحة بمتبقيها الصحيح\n" +
                "· يبقى الإيصال الأصلي موثقاً في السجل — لا حذف إطلاقاً\n\n" +
                "السبب المسجَّل: «تصحيح خطأ إدخال».\n\n" +
                "أعكس الإيصال؟",
                "↩ اعكس الإيصال");

        if (!confirmed)
            return;

        try
        {
            await using var scope =
                _scopeFactory.CreateAsyncScope();

            var handler =
                scope.ServiceProvider
                    .GetRequiredService<ReverseReceiptHandler>();

            var result =
                await handler.ExecuteAsync(
                    new ReverseReceiptRequest(
                        selected.Id,
                        "تصحيح خطأ إدخال"));

            if (result.IsSuccess)
            {
                _notifier.ShowSuccess(
                    $"عُكس الإيصال ✔ — أُنشئ إيصال العكس #{result.Value:000000}");

                await LoadPaymentsAsync();
            }
            else if (result.ErrorType == ErrorType.Unexpected)
            {
                _notifier.ShowError(result.ErrorMessage!);
            }
            else
            {
                _notifier.ShowWarning(result.ErrorMessage!);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to reverse receipt for payment {PaymentId}",
                selected.Id);

            _notifier.ShowError(
                "تعذّر عكس الإيصال — أعد المحاولة.");
        }
    }
}