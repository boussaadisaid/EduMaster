using EduMaster.Application.ClassGroups;
using EduMaster.Application.Common;
using EduMaster.Application.Enrollments;
using EduMaster.Application.Pricing;
using EduMaster.UI.Common;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace EduMaster.UI.ClassGroups;

/// <summary>
/// النقل بين الأفواج (D-53/D-78): الأهداف المطابقة فقط تُعرض، والحفظ = انسحاب + إلحاق بمعاملة واحدة بسنابشوت الهدف.
/// التهيئة بالمعرّف (D-84) — يُفتح من ديالوغ المسجَّلين ومن لوحة الطالب معاً.
/// </summary>
public sealed class TransferGroupEnrollmentViewModel : BaseViewModel, IDialogViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;
    private readonly ILogger<TransferGroupEnrollmentViewModel> _logger;

    private int _currentEnrollmentId;
    private string _studentName = string.Empty;
    private string _currentGroupName = string.Empty;
    private long _currentAgreedPriceCentimes;
    private long? _targetPriceCentimes;
    private CancellationTokenSource? _priceCts;

    public TransferGroupEnrollmentViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier,
        ILogger<TransferGroupEnrollmentViewModel> logger)
    {
        _scopeFactory = scopeFactory;
        _notifier = notifier;
        _logger = logger;

        SaveCommand = new AsyncRelayCommand(SaveAsync, () => SelectedTarget is not null && PricesMatch && !IsSaving);
        CancelCommand = new AsyncRelayCommand(() =>
        {
            CloseRequested?.Invoke(this, false);
            return Task.CompletedTask;
        });
    }

    public event EventHandler<bool>? CloseRequested;

    public string Title => "نقل إلى فوج آخر";

    public string StudentName => _studentName;
    public string CurrentGroupName => _currentGroupName;
    public string CurrentPriceText => MoneyInput.FormatDinars(_currentAgreedPriceCentimes);

    public long? TargetPriceCentimes
    {
        get => _targetPriceCentimes;
        private set
        {
            if (SetProperty(ref _targetPriceCentimes, value))
            {
                OnPropertyChanged(nameof(HasComparablePrice));
                OnPropertyChanged(nameof(PricesMatch));
                OnPropertyChanged(nameof(PriceStatusText));
                SaveCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasComparablePrice => TargetPriceCentimes.HasValue;
    public bool PricesMatch => HasComparablePrice && TargetPriceCentimes!.Value == _currentAgreedPriceCentimes;
    public string PriceStatusText => SelectedTarget is null
        ? "اختر فوجاً هدفاً لعرض مقارنة السعر."
        : !HasComparablePrice
            ? "لا يوجد سعر محدد للفوج الهدف — لا يمكن إتمام النقل."
            : PricesMatch
            ? "سعر الحصة متطابق — سينتقل الرصيد المتبقي تلقائياً."
            : $"سعر الحصة مختلف: الحالي {CurrentPriceText} دج مقابل {MoneyInput.FormatDinars(TargetPriceCentimes!.Value)} دج للهدف — لا يمكن نقل الرصيد.";

    // ---------- الأهداف المطابقة ----------
    public ObservableCollection<ClassGroupListItem> Targets { get; } = new();

    private ClassGroupListItem? _selectedTarget;
    public ClassGroupListItem? SelectedTarget
    {
        get => _selectedTarget;
        set
        {
            SetProperty(ref _selectedTarget, value);
            SaveCommand.RaiseCanExecuteChanged();
            _ = LoadSuggestedPriceAsync();
        }
    }

    public bool TargetsEmpty => Targets.Count == 0;

    // ---------- السعر على الهدف (D-77) ----------
    private string _discountNote = string.Empty;
    public string DiscountNote
    {
        get => _discountNote;
        set => SetProperty(ref _discountNote, value);
    }

    // ---------- الخطأ والانشغال ----------
    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set { SetProperty(ref _errorMessage, value); OnPropertyChanged(nameof(HasErrorMessage)); }
    }

    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

    private bool _isSaving;
    public bool IsSaving
    {
        get => _isSaving;
        private set { SetProperty(ref _isSaving, value); SaveCommand.RaiseCanExecuteChanged(); }
    }

    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand CancelCommand { get; }

    public async Task InitializeAsync(int groupEnrollmentId, string studentName, string currentGroupName, long currentAgreedPriceCentimes)
    {
        _priceCts?.Cancel();
        _priceCts?.Dispose();
        _priceCts = null;
        _currentEnrollmentId = groupEnrollmentId;
        _studentName = studentName;
        _currentGroupName = currentGroupName;
        _currentAgreedPriceCentimes = currentAgreedPriceCentimes;
        TargetPriceCentimes = null;
        OnPropertyChanged(nameof(StudentName));
        OnPropertyChanged(nameof(CurrentGroupName));
        OnPropertyChanged(nameof(CurrentPriceText));
        OnPropertyChanged(nameof(PriceStatusText));

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<GetTransferTargetsHandler>();
        var result = await handler.ExecuteAsync(groupEnrollmentId);

        if (result.IsSuccess)
        {
            Targets.Clear();
            foreach (var target in result.Value!)
                Targets.Add(target);
            OnPropertyChanged(nameof(TargetsEmpty));
        }
        else if (result.ErrorType == ErrorType.Unexpected)
            _notifier.ShowError(result.ErrorMessage!);
        else
            ErrorMessage = result.ErrorMessage;
    }

    private async Task LoadSuggestedPriceAsync()
    {
        TargetPriceCentimes = null;

        var target = SelectedTarget;
        if (target is null) return;

        _priceCts?.Cancel();
        _priceCts?.Dispose();
        var cts = _priceCts = new CancellationTokenSource();

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<GetSubjectPriceHandler>();
            var result = await handler.ExecuteAsync(target.AcademicYearId, target.LevelId, target.SubjectId, cts.Token);

            if (cts.IsCancellationRequested || !ReferenceEquals(cts, _priceCts)) return;

            if (!result.IsSuccess)
            {
                _notifier.ShowError(result.ErrorMessage!);
                return;
            }

            TargetPriceCentimes = result.Value;
            if (result.Value is null)
            {
            }
            else
            {
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (cts.IsCancellationRequested || !ReferenceEquals(cts, _priceCts)) return;
            _logger.LogError(ex, "Failed to load transfer price suggestion for class group {ClassGroupId}", target.Id);
            _notifier.ShowError("تعذّر جلب سعر الفوج الهدف.");
        }
    }

    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (SelectedTarget is null) return;

        IsSaving = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<TransferGroupEnrollmentHandler>();
            var result = await handler.ExecuteAsync(new TransferGroupEnrollmentRequest(
                _currentEnrollmentId, SelectedTarget.Id,
                string.IsNullOrWhiteSpace(DiscountNote) ? null : DiscountNote));

            if (result.IsSuccess)
            {
                _notifier.ShowSuccess($"نُقل «{StudentName}» إلى «{SelectedTarget.Name}» ✔");
                CloseRequested?.Invoke(this, true);
            }
            else if (result.ErrorType == ErrorType.Unexpected)
                _notifier.ShowError(result.ErrorMessage!);
            else
                ErrorMessage = result.ErrorMessage;
        }
        finally
        {
            IsSaving = false;
        }
    }
}