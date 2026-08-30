using EduMaster.Application.Abstractions;
using EduMaster.Application.Common;
using EduMaster.Application.People;
using EduMaster.Application.Teachers;
using EduMaster.Domain.Enums;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Windows.Media.Imaging;

namespace EduMaster.UI.Teachers;

public sealed class TeacherEditorViewModel : BaseViewModel, IDialogViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;
    private readonly IImageStore _imageStore;
    private readonly IDialogService _dialogs;

    private int? _editingTeacherId;   // معرف الملف — null = إنشاء
    private int _editingPersonId;     // معرف الشخص (لوضع التحرير وقناة الصورة)
    private string? _photoSourcePath;
    private bool _photoRemoved;

    public TeacherEditorViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier, IImageStore imageStore, IDialogService dialogs)
    {
        _scopeFactory = scopeFactory;
        _notifier = notifier;
        _imageStore = imageStore;
        _dialogs = dialogs;

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
    }

    public event EventHandler<bool>? CloseRequested;

    public string Title => _editingTeacherId is null ? "أستاذ جديد" : "تعديل الأستاذ";

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

    // ---------- الحقول ----------
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

    private string _specialty = string.Empty;
    public string Specialty { get => _specialty; set => SetProperty(ref _specialty, value); }

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
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.UriSource = new Uri(fullPath);
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
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

    public void InitializeForCreate()
    {
        _editingTeacherId = null;
        SelectedGender = GenderOptions[0];
    }

    public void InitializeForEdit(TeacherListItem item)
    {
        _editingTeacherId = item.Id;
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
        Specialty = item.Specialty ?? string.Empty;
        Notes = item.Notes ?? string.Empty;

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

        IsSaving = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            DateOnly? birthDate = BirthDate is null ? null : DateOnly.FromDateTime(BirthDate.Value);

            if (_editingTeacherId is null)
            {
                // تحذير غير مانع قبل إنشاء سجل شخص جديد للدور — نفس آلية محرر الأشخاص.
                var duplicateResult = await scope.ServiceProvider.GetRequiredService<FindPersonDuplicateHandler>()
                    .ExecuteAsync(new FindPersonDuplicateRequest(FirstName, LastName, FatherName));
                if (duplicateResult.IsSuccess && duplicateResult.Value is not null
                    && !await _dialogs.ConfirmAsync(
                        "تنبيه تكرار محتمل",
                        $"يوجد شخص قائم بنفس الاسم: «{duplicateResult.Value.FullName}». هل تريد متابعة إنشاء أستاذ جديد كسجل شخص منفصل؟",
                        "تابع بالإنشاء"))
                    return;

                var handler = scope.ServiceProvider.GetRequiredService<CreateTeacherHandler>();
                var result = await handler.ExecuteAsync(new CreateTeacherRequest(
                    FirstName, LastName, FatherName, birthDate, SelectedGender?.Value,
                    Phone, Phone2, Email, Address, Specialty, Notes,
                    PhotoSourcePath: _photoRemoved ? null : _photoSourcePath));

                if (!HandleSaveResult(result.IsSuccess, result.ErrorMessage, result.ErrorType, "أُضيف الأستاذ بنجاح ✔"))
                    return;
            }
            else
            {
                var handler = scope.ServiceProvider.GetRequiredService<UpdateTeacherHandler>();
                var result = await handler.ExecuteAsync(new UpdateTeacherRequest(
                    _editingTeacherId.Value, FirstName, LastName, FatherName, birthDate, SelectedGender?.Value,
                    Phone, Phone2, Email, Address, Specialty, Notes));

                if (!HandleSaveResult(result.IsSuccess, result.ErrorMessage, result.ErrorType, "حُفظت بيانات الأستاذ ✔"))
                    return;

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