using EduMaster.Application.Abstractions;
using EduMaster.Application.Common;
using EduMaster.Application.People;
using EduMaster.Application.Students;
using EduMaster.Domain.Enums;
using EduMaster.Domain.Students;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;

namespace EduMaster.UI.Students;

public sealed class StudentEditorViewModel : BaseViewModel, IDialogViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;
    private readonly IImageStore _imageStore;

    private int? _editingStudentId;   // معرف الملف — null = إنشاء
    private int _editingPersonId;     // معرف الشخص (لوضع التحرير وقناة الصورة)
    private int? _guardianId;
    private string? _photoSourcePath; // صورة التُقطت حديثاً
    private bool _photoRemoved;       // أزيلت صورة موجودة
    private CancellationTokenSource? _guardianSearchCts;

    public StudentEditorViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier, IImageStore imageStore)
    {
        _scopeFactory = scopeFactory;
        _notifier = notifier;
        _imageStore = imageStore;

        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsSaving);
        CancelCommand = new AsyncRelayCommand(() =>
        {
            CloseRequested?.Invoke(this, false);
            return Task.CompletedTask;
        });
        RemovePhotoCommand = new AsyncRelayCommand(() =>
        {
            _photoSourcePath = null;
            _photoRemoved = true;
            PhotoPreview = null;
            return Task.CompletedTask;
        });
        ClearGuardianCommand = new AsyncRelayCommand(() =>
        {
            _guardianId = null;
            GuardianName = null;
            OnPropertyChanged(nameof(HasGuardian));
            OnPropertyChanged(nameof(HasNoGuardian));
            return Task.CompletedTask;
        });
    }

    public event EventHandler<bool>? CloseRequested;

    public string Title => _editingStudentId is null ? "طالب جديد" : "تعديل الطالب";

    // ---------- الجنس والصنف ----------
    public sealed record GenderOption(GenderType? Value, string Label);
    public IReadOnlyList<GenderOption> GenderOptions { get; } = new[]
    {
        new GenderOption(null, "غير محدد"),
        new GenderOption(GenderType.Male, "ذكر"),
        new GenderOption(GenderType.Female, "أنثى"),
    };

    private GenderOption? _selectedGender;
    public GenderOption? SelectedGender
    {
        get => _selectedGender;
        set => SetProperty(ref _selectedGender, value);
    }

    public sealed record CategoryOption(StudentCategory Value, string Label);
    public IReadOnlyList<CategoryOption> CategoryOptions { get; } = new[]
    {
        new CategoryOption(StudentCategory.Regular, "نظامي"),
        new CategoryOption(StudentCategory.FreeCandidate, "مترشح حر"),
        new CategoryOption(StudentCategory.University, "جامعي"),
        new CategoryOption(StudentCategory.Training, "تكوين ودورات"),
    };

    private CategoryOption? _selectedCategory;
    public CategoryOption? SelectedCategory
    {
        get => _selectedCategory;
        set => SetProperty(ref _selectedCategory, value);
    }

    // ---------- حقول الشخص ----------
    private string _firstName = string.Empty;
    public string FirstName { get => _firstName; set => SetProperty(ref _firstName, value); }

    private string _lastName = string.Empty;
    public string LastName { get => _lastName; set => SetProperty(ref _lastName, value); }

    private string _fatherName = string.Empty;
    public string FatherName { get => _fatherName; set => SetProperty(ref _fatherName, value); }

    private DateTime? _birthDate;
    public DateTime? BirthDate { get => _birthDate; set => SetProperty(ref _birthDate, value); }

    private string _phone = string.Empty;
    public string Phone { get => _phone; set => SetProperty(ref _phone, value); }

    private string _phone2 = string.Empty;
    public string Phone2 { get => _phone2; set => SetProperty(ref _phone2, value); }

    private string _email = string.Empty;
    public string Email { get => _email; set => SetProperty(ref _email, value); }

    private string _address = string.Empty;
    public string Address { get => _address; set => SetProperty(ref _address, value); }

    private string _notes = string.Empty;
    public string Notes { get => _notes; set => SetProperty(ref _notes, value); }

    // ---------- الصورة ----------
    private BitmapImage? _photoPreview;
    public BitmapImage? PhotoPreview
    {
        get => _photoPreview;
        private set
        {
            SetProperty(ref _photoPreview, value);
            OnPropertyChanged(nameof(HasPhotoPreview));
            OnPropertyChanged(nameof(HasNoPhotoPreview));
        }
    }

    public bool HasPhotoPreview => PhotoPreview is not null;
    public bool HasNoPhotoPreview => PhotoPreview is null;

    /// <summary>يناديها code-behind بعد OpenFileDialog — الـVM لا يعرف النوافذ</summary>
    public void SetPickedPhoto(string path)
    {
        _photoSourcePath = path;
        _photoRemoved = false;
        PhotoPreview = LoadPreview(path);
    }

    private static BitmapImage? LoadPreview(string? fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
            return null;

        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;   // لا يقفل الملف بعد العرض
        bmp.UriSource = new Uri(fullPath);
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    // ---------- ولي الأمر ----------
    public ObservableCollection<PersonListItem> GuardianResults { get; } = new();

    private string _guardianSearchText = string.Empty;
    public string GuardianSearchText
    {
        get => _guardianSearchText;
        set
        {
            SetProperty(ref _guardianSearchText, value);
            _ = DebouncedGuardianSearchAsync();
        }
    }

    private string? _guardianName;
    public string? GuardianName
    {
        get => _guardianName;
        private set => SetProperty(ref _guardianName, value);
    }

    public bool HasGuardian => _guardianId is not null;
    public bool HasNoGuardian => _guardianId is null;

    private PersonListItem? _pickedGuardian;
    public PersonListItem? PickedGuardian
    {
        get => _pickedGuardian;
        set
        {
            SetProperty(ref _pickedGuardian, value);
            if (value is null) return;

            // التقط ثم نظّف — مسح النتائج يعيد ضبط الاختيار تلقائياً
            _guardianId = value.Id;
            GuardianName = value.FullName;
            OnPropertyChanged(nameof(HasGuardian));
            OnPropertyChanged(nameof(HasNoGuardian));

            GuardianResults.Clear();
            GuardianSearchText = string.Empty;   // البحث الفارغ يمسح النتائج ولا يحمّل الكل
        }
    }

    private async Task DebouncedGuardianSearchAsync()
    {
        _guardianSearchCts?.Cancel();
        var cts = _guardianSearchCts = new CancellationTokenSource();

        try
        {
            await Task.Delay(300, cts.Token);

            if (string.IsNullOrWhiteSpace(GuardianSearchText))
            {
                GuardianResults.Clear();
                return;
            }

            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<SearchPersonsHandler>();
            var result = await handler.ExecuteAsync(GuardianSearchText, cts.Token);

            if (result.IsSuccess)
            {
                GuardianResults.Clear();
                foreach (var p in result.Value!.Where(p => p.Id != _editingPersonId).Take(8))
                    GuardianResults.Add(p);   // لا يمكن أن يكون الطالب وليَّ نفسه (وحارس الكيان يحمي أيضاً)
            }
        }
        catch (OperationCanceledException) { }
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
    public AsyncRelayCommand RemovePhotoCommand { get; }
    public AsyncRelayCommand ClearGuardianCommand { get; }

    public void InitializeForCreate()
    {
        _editingStudentId = null;
        SelectedCategory = CategoryOptions[0];
        SelectedGender = GenderOptions[0];
    }

    public void InitializeForEdit(StudentListItem item)
    {
        _editingStudentId = item.Id;
        _editingPersonId = item.PersonId;

        FirstName = item.FirstName;
        LastName = item.LastName;
        FatherName = item.FatherName ?? string.Empty;
        BirthDate = item.BirthDate?.ToDateTime(TimeOnly.MinValue);
        SelectedGender = GenderOptions.FirstOrDefault(g => g.Value == item.Gender) ?? GenderOptions[0];
        Phone = item.Phone ?? string.Empty;
        Phone2 = item.Phone2 ?? string.Empty;
        Email = item.Email ?? string.Empty;
        Address = item.Address ?? string.Empty;
        Notes = item.Notes ?? string.Empty;
        SelectedCategory = CategoryOptions.FirstOrDefault(c => c.Value == item.Category) ?? CategoryOptions[0];

        _guardianId = item.GuardianPersonId;
        GuardianName = item.GuardianFullName;
        OnPropertyChanged(nameof(HasGuardian));
        OnPropertyChanged(nameof(HasNoGuardian));

        PhotoPreview = LoadPreview(_imageStore.GetFullPath(item.PhotoPath));
        _photoSourcePath = null;
        _photoRemoved = false;
    }

    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(FirstName) || string.IsNullOrWhiteSpace(LastName))
        {
            ErrorMessage = "أدخل الاسم الأول واللقب.";
            return;
        }
        if (SelectedCategory is null)
        {
            ErrorMessage = "اختر صنف الطالب.";
            return;
        }

        IsSaving = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            DateOnly? birthDate = BirthDate is null ? null : DateOnly.FromDateTime(BirthDate.Value);

            if (_editingStudentId is null)
            {
                var handler = scope.ServiceProvider.GetRequiredService<CreateStudentHandler>();
                var result = await handler.ExecuteAsync(new CreateStudentRequest(
                    FirstName, LastName, FatherName, birthDate, SelectedGender?.Value,
                    Phone, Phone2, Email, Address,
                    _guardianId, SelectedCategory.Value, Notes,
                    PhotoSourcePath: _photoRemoved ? null : _photoSourcePath));

                if (!HandleSaveResult(result.IsSuccess, result.ErrorMessage, result.ErrorType, "أُضيف الطالب بنجاح ✔"))
                    return;
            }
            else
            {
                var handler = scope.ServiceProvider.GetRequiredService<UpdateStudentHandler>();
                var result = await handler.ExecuteAsync(new UpdateStudentRequest(
                    _editingStudentId.Value, FirstName, LastName, FatherName, birthDate, SelectedGender?.Value,
                    Phone, Phone2, Email, Address,
                    _guardianId, SelectedCategory.Value, Notes));

                if (!HandleSaveResult(result.IsSuccess, result.ErrorMessage, result.ErrorType, "حُفظت بيانات الطالب ✔"))
                    return;

                // الصورة في وضع التحرير: قناة مستقلة — فقط عند اللمس (تغيير أو إزالة)
                if (_photoRemoved || _photoSourcePath is not null)
                {
                    var photoHandler = scope.ServiceProvider.GetRequiredService<SetPersonPhotoHandler>();
                    var photoResult = await photoHandler.ExecuteAsync(
                        new SetPersonPhotoRequest(_editingPersonId, _photoRemoved ? null : _photoSourcePath));

                    if (!photoResult.IsSuccess)
                        _notifier.ShowWarning("حُفظت البيانات، لكن الصورة لم تُحفظ: " + photoResult.ErrorMessage);
                }
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