using EduMaster.Application.Common;
using EduMaster.Application.People;
using EduMaster.Domain.Enums;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EduMaster.UI.People;

public sealed class PersonEditorViewModel : BaseViewModel, IDialogViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;

    public PersonEditorViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier)
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

    private int? _editingId;   // null = إنشاء

    public string Title => _editingId is null ? "شخص جديد" : "تعديل الشخص";

    // ---------- الجنس ----------
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

    public void InitializeForCreate()
    {
        _editingId = null;
        SelectedGender = GenderOptions[0];
    }

    public void InitializeForEdit(PersonListItem person)
    {
        _editingId = person.Id;
        FirstName = person.FirstName;
        LastName = person.LastName;
        FatherName = person.FatherName ?? string.Empty;
        BirthDate = person.BirthDate?.ToDateTime(TimeOnly.MinValue);
        SelectedGender = GenderOptions.FirstOrDefault(g => g.Value == person.Gender) ?? GenderOptions[0];
        Phone = person.Phone ?? string.Empty;
        Phone2 = person.Phone2 ?? string.Empty;
        Email = person.Email ?? string.Empty;
        Address = person.Address ?? string.Empty;
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

            if (_editingId is null)
            {
                var handler = scope.ServiceProvider.GetRequiredService<CreatePersonHandler>();
                var result = await handler.ExecuteAsync(new CreatePersonRequest(
                    FirstName, LastName, FatherName, birthDate, SelectedGender?.Value,
                    Phone, Phone2, Email, Address));   // PhotoPath تبقى null افتراضياً (ح-6)

                if (!HandleSaveResult(result.IsSuccess, result.ErrorMessage, result.ErrorType, "أُضيف الشخص بنجاح ✔"))
                    return;
            }
            else
            {
                var handler = scope.ServiceProvider.GetRequiredService<UpdatePersonHandler>();
                var result = await handler.ExecuteAsync(new UpdatePersonRequest(
                    _editingId.Value, FirstName, LastName, FatherName, birthDate, SelectedGender?.Value,
                    Phone, Phone2, Email, Address));

                if (!HandleSaveResult(result.IsSuccess, result.ErrorMessage, result.ErrorType, "حُفظت بيانات الشخص ✔"))
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