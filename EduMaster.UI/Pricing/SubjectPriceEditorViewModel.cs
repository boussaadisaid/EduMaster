using EduMaster.Application.Academic;
using EduMaster.Application.AcademicYears;
using EduMaster.Application.Common;
using EduMaster.Application.Pricing;
using EduMaster.UI.Common;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace EduMaster.UI.Pricing;

public sealed class SubjectPriceEditorViewModel : BaseViewModel, IDialogViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;

    private int? _editingPriceId;   // null = إنشاء

    public SubjectPriceEditorViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier)
    {
        _scopeFactory = scopeFactory;
        _notifier = notifier;

        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsSaving);
        CancelCommand = new AsyncRelayCommand(() =>
        {
            CloseRequested?.Invoke(this, false);
            return Task.CompletedTask;
        });
    }

    public event EventHandler<bool>? CloseRequested;

    public string Title => _editingPriceId is null ? "سعر جديد" : "تعديل السعر";
    public bool IsCreateMode => _editingPriceId is null;

    // ---------- خيارات القوائم ----------
    public sealed record YearOption(int Id, string Label, bool IsCurrent);
    public sealed record NamedOption(int Id, string Name);

    public ObservableCollection<YearOption> Years { get; } = new();
    public ObservableCollection<NamedOption> Levels { get; } = new();
    public ObservableCollection<NamedOption> Subjects { get; } = new();

    private YearOption? _selectedYear;
    public YearOption? SelectedYear
    {
        get => _selectedYear;
        set => SetProperty(ref _selectedYear, value);
    }

    private NamedOption? _selectedLevel;
    public NamedOption? SelectedLevel
    {
        get => _selectedLevel;
        set => SetProperty(ref _selectedLevel, value);
    }

    private NamedOption? _selectedSubject;
    public NamedOption? SelectedSubject
    {
        get => _selectedSubject;
        set => SetProperty(ref _selectedSubject, value);
    }

    // ---------- السعر بالدينار (D-51/D-67) ----------
    private string _priceText = string.Empty;
    public string PriceText
    {
        get => _priceText;
        set => SetProperty(ref _priceText, value);
    }

    // ---------- الخطأ والحفظ ----------
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

    // ---------- التهيئة ----------
    public async Task InitializeForCreateAsync(int? preferredYearId)
    {
        _editingPriceId = null;
        OnPropertyChanged(nameof(IsCreateMode));

        await LoadOptionsAsync();

        SelectedYear = Years.FirstOrDefault(y => preferredYearId is not null && y.Id == preferredYearId)
            ?? Years.FirstOrDefault(y => y.IsCurrent)
            ?? Years.FirstOrDefault();
    }

    public async Task InitializeForEditAsync(SubjectPriceListItem item)
    {
        _editingPriceId = item.Id;
        OnPropertyChanged(nameof(IsCreateMode));

        PriceText = MoneyInput.FormatDinars(item.UnitPriceCentimes);

        await LoadOptionsAsync();

        // الهوية (سنة/مستوى/مادة) ثابتة — تُعرض معطّلة، والقيمة الحالية تُضمَّن حتى لو لم تعد فعّالة
        SelectedYear = Years.FirstOrDefault(y => y.Id == item.AcademicYearId);
        EnsureCurrentOption(Levels, item.LevelId, item.LevelName);
        EnsureCurrentOption(Subjects, item.SubjectId, item.SubjectName);
        SelectedLevel = Levels.FirstOrDefault(l => l.Id == item.LevelId);
        SelectedSubject = Subjects.FirstOrDefault(s => s.Id == item.SubjectId);
    }

    private static void EnsureCurrentOption(ObservableCollection<NamedOption> options, int id, string name)
    {
        if (options.All(o => o.Id != id))
            options.Add(new NamedOption(id, name + " (معطّل)"));
    }

    private async Task LoadOptionsAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        // السنوات — الكل: الأسعار إعداد لا تشغيل، وتسعير سنة قادمة أو معطّلة جائز (D-65)
        var yearsResult = await scope.ServiceProvider.GetRequiredService<GetAllAcademicYearsHandler>().ExecuteAsync();
        if (!yearsResult.IsSuccess)
        {
            _notifier.ShowError(yearsResult.ErrorMessage!);
            return;
        }
        Years.Clear();
        foreach (var year in yearsResult.Value!)
            Years.Add(new YearOption(year.Id, year.Name + (year.IsActive ? string.Empty : " (معطّلة)"), year.IsCurrent));   // D-63: لا ToString للكيان

        // المستويات الفعّالة فقط
        var levelsResult = await scope.ServiceProvider.GetRequiredService<GetLevelsHandler>().ExecuteAsync();
        if (!levelsResult.IsSuccess)
        {
            _notifier.ShowError(levelsResult.ErrorMessage!);
            return;
        }
        Levels.Clear();
        foreach (var level in levelsResult.Value!.Where(l => l.IsActive))
            Levels.Add(new NamedOption(level.Id, level.Name));

        // المواد الفعّالة فقط
        var subjectsResult = await scope.ServiceProvider.GetRequiredService<GetSubjectsHandler>().ExecuteAsync();
        if (!subjectsResult.IsSuccess)
        {
            _notifier.ShowError(subjectsResult.ErrorMessage!);
            return;
        }
        Subjects.Clear();
        foreach (var subject in subjectsResult.Value!.Where(s => s.IsActive))
            Subjects.Add(new NamedOption(subject.Id, subject.Name));
    }

    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (IsCreateMode && SelectedYear is null)
        {
            ErrorMessage = "اختر السنة الدراسية.";
            return;
        }
        if (SelectedLevel is null)
        {
            ErrorMessage = "اختر المستوى.";
            return;
        }
        if (SelectedSubject is null)
        {
            ErrorMessage = "اختر المادة.";
            return;
        }
        if (!MoneyInput.TryParseDinars(PriceText, out var unitPriceCentimes))
        {
            ErrorMessage = "أدخل سعراً صحيحاً بالدينار (مثل 0 أو 1500 أو 1500.50) — والفارغ = صفر (مجاني).";
            return;
        }

        IsSaving = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();

            if (_editingPriceId is null)
            {
                var handler = scope.ServiceProvider.GetRequiredService<CreateSubjectPriceHandler>();
                var result = await handler.ExecuteAsync(new CreateSubjectPriceRequest(
                    SelectedYear!.Id, SelectedLevel.Id, SelectedSubject.Id, unitPriceCentimes));

                if (!HandleSaveResult(result.IsSuccess, result.ErrorMessage, result.ErrorType, "أُضيف السعر بنجاح ✔"))
                    return;
            }
            else
            {
                var handler = scope.ServiceProvider.GetRequiredService<UpdateSubjectPriceHandler>();
                var result = await handler.ExecuteAsync(new UpdateSubjectPriceRequest(_editingPriceId.Value, unitPriceCentimes));

                if (!HandleSaveResult(result.IsSuccess, result.ErrorMessage, result.ErrorType, "حُفظ السعر ✔"))
                    return;
            }

            CloseRequested?.Invoke(this, true);
        }
        finally
        {
            IsSaving = false;
        }
    }

    // D-22 داخل الديالوغ: المتوقع ← بانر أحمر · غير المتوقع ← Toast
    private bool HandleSaveResult(bool isSuccess, string? errorMessage, ErrorType errorType, string successMessage)
    {
        if (isSuccess)
        {
            _notifier.ShowSuccess(successMessage);
            return true;
        }

        if (errorType == ErrorType.Unexpected)
            _notifier.ShowError(errorMessage!);
        else
            ErrorMessage = errorMessage;

        return false;
    }
}