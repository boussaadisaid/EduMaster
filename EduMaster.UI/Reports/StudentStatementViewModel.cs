using EduMaster.Application.AcademicYears;
using EduMaster.Application.Common;
using EduMaster.Application.Printing;
using EduMaster.Application.Reports;
using EduMaster.Application.Settings;
using EduMaster.Application.Students;
using EduMaster.UI.Common;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using EduMaster.UI.Printing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace EduMaster.UI.Reports;

/// <summary>
/// كشف حساب طالب (6.1 — D-127): منتقى ببحث 300ms (D-33) + ترويسة من السطر المحدَّد (لا استعلام ترويسة) + الكشف التجميعي.
/// 6.3 (ط-هـ): زر 🖨 يطبع الكشف المعروض حرفياً (WYSIWYP) بترويسة هوية المدرسة (ط-7) +
/// 🖨 على كل سطر إيصال داخل الكشف — إعادة طباعة من موضع القراءة نفسه (تتمة ط-هـ بشكوى المستخدم).
/// </summary>
public sealed class StudentStatementViewModel : BaseViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;
    private readonly ILogger<StudentStatementViewModel> _logger;
    private readonly IPrintService _printService;
    private CancellationTokenSource? _searchCts;
    private CancellationTokenSource? _loadCts;
    private bool _suppressScopeReload;

    public sealed record StatementScopeOption(int? AcademicYearId, string Title);

    public ObservableCollection<StatementScopeOption> ScopeOptions { get; } = new();

    private StatementScopeOption? _selectedScope;
    public StatementScopeOption? SelectedScope
    {
        get => _selectedScope;
        set
        {
            if (SetProperty(ref _selectedScope, value) && !_suppressScopeReload && SelectedStudent is not null)
            {
                var cts = _loadCts = new CancellationTokenSource();
                _ = LoadStatementAsync(SelectedStudent.Id, cts.Token);
            }
        }
    }

    public StudentStatementViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier,
        ILogger<StudentStatementViewModel> logger, IPrintService printService)
    {
        _scopeFactory = scopeFactory;
        _notifier = notifier;
        _logger = logger;
        _printService = printService;

        PrintCommand = new AsyncRelayCommand(PrintAsync, () => Statement is not null && SelectedStudent is not null);
        PrintReceiptCommand = new AsyncRelayCommand(PrintSelectedReceiptAsync);   // بلا بوابة — النقرة الأولى تحدّد السطر قبل Click (درس بوابة السجل)
    }

    // ---------- منتقي الطالب ----------
    public ObservableCollection<StudentListItem> SearchResults { get; } = new();

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            SetProperty(ref _searchText, value);
            _ = DebouncedSearchAsync();
        }
    }

    private StudentListItem? _selectedStudent;
    public StudentListItem? SelectedStudent
    {
        get => _selectedStudent;
        set
        {
            if (SetProperty(ref _selectedStudent, value))
            {
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(HasNoSelection));
                _loadCts?.Cancel();   // D-64: تبديل الطالب يلغي تحميل سابقه
                Statement = null;
                if (value is not null)
                {
                    var cts = _loadCts = new CancellationTokenSource();
                    _ = LoadStatementAsync(value.Id, cts.Token);
                }
            }
        }
    }

    public bool HasSelection => SelectedStudent is not null;
    public bool HasNoSelection => SelectedStudent is null;

    // ---------- الكشف ----------
    private StudentStatementItem? _statement;
    public StudentStatementItem? Statement
    {
        get => _statement;
        private set
        {
            SetProperty(ref _statement, value);
            OnPropertyChanged(nameof(HasStatement));
            OnPropertyChanged(nameof(HasNoCharges));
            OnPropertyChanged(nameof(HasNoPayments));
            PrintCommand.RaiseCanExecuteChanged();
        }
    }

    public bool HasStatement => Statement is not null;
    public bool HasNoCharges => Statement is not null && Statement.Charges.Count == 0;
    public bool HasNoPayments => Statement is not null && Statement.Payments.Count == 0;

    /// <summary>سطر الإيصال المحدَّد في شبكة «الإيصالات وتخصيصاتها» — الأمر بالتحديد لا بالمعاملات (قاعدة الواجهة)</summary>
    private StudentPaymentLine? _selectedPayment;
    public StudentPaymentLine? SelectedPayment
    {
        get => _selectedPayment;
        set => SetProperty(ref _selectedPayment, value);
    }

    private bool _isLoadingStatement;
    public bool IsLoadingStatement
    {
        get => _isLoadingStatement;
        private set => SetProperty(ref _isLoadingStatement, value);
    }

    private string _totalsText = string.Empty;
    public string TotalsText
    {
        get => _totalsText;
        private set => SetProperty(ref _totalsText, value);
    }

    public AsyncRelayCommand PrintCommand { get; }
    public AsyncRelayCommand PrintReceiptCommand { get; }

    /// <summary>قائمة أولية بلا مصطلح (البحث بمصطلح فارغ يعيد الجميع — حجم الجدول تافه D-33)</summary>
    public async Task InitializeAsync()
    {
        await LoadScopesAsync();
        await SearchAsync();
    }

    private async Task LoadScopesAsync()
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<GetAllAcademicYearsHandler>().ExecuteAsync();
            if (!result.IsSuccess)
            {
                _notifier.ShowError(result.ErrorMessage!);
                return;
            }

            var years = result.Value!;
            _suppressScopeReload = true;
            ScopeOptions.Clear();
            ScopeOptions.Add(new StatementScopeOption(null, "كل السنوات"));
            foreach (var year in years.OrderByDescending(y => y.StartDate))
                ScopeOptions.Add(new StatementScopeOption(year.Id, year.IsCurrent ? $"{year.Name} (الحالية)" : year.Name));
            SelectedScope = ScopeOptions.FirstOrDefault(s => s.AcademicYearId == years.FirstOrDefault(y => y.IsCurrent)?.Id) ?? ScopeOptions[0];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load academic year scopes for student statement");
            _notifier.ShowError("تعذّر تحميل السنوات الدراسية — أعد المحاولة.");
        }
        finally
        {
            _suppressScopeReload = false;
        }
    }

    private async Task DebouncedSearchAsync()
    {
        _searchCts?.Cancel();
        var cts = _searchCts = new CancellationTokenSource();

        try
        {
            await Task.Delay(300, cts.Token);   // D-33
            await SearchAsync(cts.Token);
        }
        catch (OperationCanceledException) { }
    }

    private async Task SearchAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<SearchStudentsHandler>();
            var result = await handler.ExecuteAsync(SearchText, cancellationToken);

            if (result.IsSuccess)
            {
                SearchResults.Clear();
                foreach (var item in result.Value!)
                    SearchResults.Add(item);
            }
            else if (!cancellationToken.IsCancellationRequested)
            {
                _notifier.ShowError(result.ErrorMessage!);
            }
        }
        catch (OperationCanceledException) { }   // D-64
        catch (Exception ex)                     // D-69
        {
            _logger.LogError(ex, "Failed to search students for statement picker");
            _notifier.ShowError("تعذّر البحث عن الطلاب — أعد المحاولة.");
        }
    }

    private async Task LoadStatementAsync(int studentId, CancellationToken cancellationToken)
    {
        IsLoadingStatement = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<GetStudentStatementHandler>();
            var result = SelectedScope?.AcademicYearId is int academicYearId
                ? await handler.ExecuteForAcademicYearAsync(studentId, academicYearId, cancellationToken)
                : await handler.ExecuteAsync(studentId, cancellationToken);

            if (result.IsSuccess)
            {
                Statement = result.Value!;
                TotalsText = Statement.IsAcademicYearScoped
                    ? $"الرصيد القائم للسنة {Statement.AcademicYearName}: {MoneyInput.FormatDinars(Statement.BalanceCentimes)} دج" +
                      $" · المخصص للسنة من الإيصالات: {MoneyInput.FormatDinars(Statement.ReceiptsTotalCentimes)} دج" +
                      $" · الزائدة الدائنة (كل السنوات): {MoneyInput.FormatDinars(Statement.CreditCentimes)} دج"
                    : $"الرصيد القائم: {MoneyInput.FormatDinars(Statement.BalanceCentimes)} دج" +
                      $" · الزائدة الدائنة: {MoneyInput.FormatDinars(Statement.CreditCentimes)} دج" +
                      $" · إجمالي المقبوض: {MoneyInput.FormatDinars(Statement.ReceiptsTotalCentimes)} دج" +
                      $" · إجمالي المصروف: {MoneyInput.FormatDinars(Statement.RefundsTotalCentimes)} دج";
            }
            else if (!cancellationToken.IsCancellationRequested)
            {
                if (result.ErrorType == ErrorType.Unexpected)
                    _notifier.ShowError(result.ErrorMessage!);
                else
                    _notifier.ShowWarning(result.ErrorMessage!);
            }
        }
        catch (OperationCanceledException) { }   // D-64
        catch (Exception ex)                     // D-69
        {
            _logger.LogError(ex, "Failed to load account statement for student {StudentId}", studentId);
            _notifier.ShowError("تعذّر تحميل كشف الحساب — أعد المحاولة.");
        }
        finally
        {
            IsLoadingStatement = false;
        }
    }

    /// <summary>
    /// 🖨 طباعة الكشف (6.3 — ط-هـ): الكشف المعروض + بيانات الطالب من السطر المحدَّد نفسه + ترويسة الهوية ← المرسّم ← نافذة الطباعة.
    /// إلغاء النافذة بصمت · فشلها toast عربي + تسجيل إنجليزي (D-22/D-69).
    /// </summary>
    private async Task PrintAsync()
    {
        var statement = Statement;
        var student = SelectedStudent;
        if (statement is null || student is null)
            return;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var school = await scope.ServiceProvider.GetRequiredService<GetSchoolInfoHandler>().ExecuteAsync();
            var info = school.IsSuccess ? school.Value! : new SchoolInfoItem(0, string.Empty, null, null, null);
            var header = new PrintHeader(info.DisplayName, info.Phone, info.Address, info.LogoPath);

            if (_printService.PrintStudentStatement(new StudentStatementPrintModel(header, student, statement)) == PrintOutcome.Failed)
                _notifier.ShowError("تعذّرت الطباعة — تحقق من الطابعة وأعد المحاولة.");
        }
        catch (Exception ex)   // D-69
        {
            _logger.LogError(ex, "Failed to print account statement for student {StudentId}", student.Id);
            _notifier.ShowError("تعذّرت طباعة كشف الحساب — أعد المحاولة.");
        }
    }

    /// <summary>
    /// 🖨 من سطر إيصالات الكشف (تتمة ط-هـ — بشكوى المستخدم «لا أعيد طباعة إيصال قديم»):
    /// نفس مسار سجل المدفوعات — المعالج النقي المختبَر (ط-2) ثم الطباعة · يعمل لأي إيصال مهما قَدُم ومهما كان نوع مستحقه.
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
                    _notifier.ShowWarning(result.ErrorMessage!);   // «الإيصال غير موجود» — تحذيري (D-29)
                return;
            }

            if (_printService.PrintReceipt(result.Value!) == PrintOutcome.Failed)
                _notifier.ShowError("تعذّرت الطباعة — تحقق من الطابعة وأعد المحاولة.");
        }
        catch (Exception ex)   // D-69
        {
            _logger.LogError(ex, "Failed to print receipt for payment {PaymentId} from statement", selected.Id);
            _notifier.ShowError("تعذّرت طباعة الإيصال — أعد المحاولة.");
        }
    }
}