using EduMaster.Application.Common;
using EduMaster.Application.People;
using EduMaster.Application.Users;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using EduMaster.UI.Students;
using EduMaster.UI.Teachers;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;



namespace EduMaster.UI.People;

public sealed class PeopleViewModel : BaseViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IServiceProvider _services;
    private readonly IUserNotifier _notifier;
    private readonly IDialogService _dialogs;
    private CancellationTokenSource? _searchCts;

    public PeopleViewModel(
        IServiceScopeFactory scopeFactory,
        IServiceProvider services,
        IUserNotifier notifier,
        IDialogService dialogs)
    {
        _scopeFactory = scopeFactory;
        _services = services;
        _notifier = notifier;
        _dialogs = dialogs;

        RefreshCommand = new AsyncRelayCommand(() => LoadAsync());
        AddCommand = new AsyncRelayCommand(AddAsync);
        EditCommand = new AsyncRelayCommand(EditAsync, () => SelectedPerson is not null);
        DeactivateCommand = new AsyncRelayCommand(DeactivateAsync, () => SelectedPerson is { IsActive: true });
        ActivateCommand = new AsyncRelayCommand(ActivateAsync, () => SelectedPerson is { IsActive: false });
        CreateAccountCommand = new AsyncRelayCommand(CreateAccountAsync, () => SelectedPerson is { IsActive: true } && SelectedAccount is null);
        UnlockAccountCommand = new AsyncRelayCommand(UnlockAccountAsync, () => SelectedAccount is { IsLockedOut: true });
        ResetPasswordCommand = new AsyncRelayCommand(ResetPasswordAsync, () => SelectedAccount is not null);
        AssignStudentRoleCommand = new AsyncRelayCommand(AssignStudentRoleAsync, () => SelectedPerson is { IsActive: true });
        AssignTeacherRoleCommand = new AsyncRelayCommand(AssignTeacherRoleAsync, () => SelectedPerson is { IsActive: true });
    }

    // ---------- البحث الفوري (ح-4: live مع مهلة 300ms) ----------

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

    private async Task DebouncedSearchAsync()
    {
        _searchCts?.Cancel();
        var cts = _searchCts = new CancellationTokenSource();

        try
        {
            await Task.Delay(300, cts.Token);
            await LoadAsync(cts.Token);
        }
        catch (OperationCanceledException) { }
    }

    // ---------- الحالة ----------

    public ObservableCollection<PersonListItem> People { get; } = new();

    private PersonListItem? _selectedPerson;
    public PersonListItem? SelectedPerson
    {
        get => _selectedPerson;
        set
        {
            SetProperty(ref _selectedPerson, value);
            RaiseActionCommandsCanExecute();
            _ = LoadAccountCardAsync();
        }
    }

    private PersonAccountInfo? _selectedAccount;
    public PersonAccountInfo? SelectedAccount
    {
        get => _selectedAccount;
        private set
        {
            SetProperty(ref _selectedAccount, value);
            OnPropertyChanged(nameof(AccountStatusText));
            RaiseActionCommandsCanExecute();
        }
    }

    public string AccountStatusText
    {
        get
        {
            if (SelectedPerson is null) return "حدّد شخصاً لعرض حسابه.";
            if (SelectedAccount is null) return "لا يوجد حساب دخول لهذا الشخص.";
            if (SelectedAccount.IsLockedOut) return $"🔒 مقفل مؤقتاً — بقي نحو {SelectedAccount.LockoutRemainingMinutes} دقيقة";
            if (!SelectedAccount.IsActive) return "الحساب معطّل.";
            return "الحساب فعّال ✔";
        }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        private set { SetProperty(ref _isLoading, value); OnPropertyChanged(nameof(IsEmpty)); }
    }

    public bool IsEmpty => !IsLoading && People.Count == 0;

    // ---------- الأوامر ----------

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand AddCommand { get; }
    public AsyncRelayCommand EditCommand { get; }
    public AsyncRelayCommand DeactivateCommand { get; }
    public AsyncRelayCommand ActivateCommand { get; }
    public AsyncRelayCommand CreateAccountCommand { get; }
    public AsyncRelayCommand UnlockAccountCommand { get; }
    public AsyncRelayCommand ResetPasswordCommand { get; }
    public AsyncRelayCommand AssignStudentRoleCommand { get; }
    public AsyncRelayCommand AssignTeacherRoleCommand { get; }
    public Task InitializeAsync() => LoadAsync();

    private void RaiseActionCommandsCanExecute()
    {
        EditCommand.RaiseCanExecuteChanged();
        DeactivateCommand.RaiseCanExecuteChanged();
        ActivateCommand.RaiseCanExecuteChanged();
        CreateAccountCommand.RaiseCanExecuteChanged();
        UnlockAccountCommand.RaiseCanExecuteChanged();
        ResetPasswordCommand.RaiseCanExecuteChanged();
        AssignStudentRoleCommand.RaiseCanExecuteChanged();
        AssignTeacherRoleCommand.RaiseCanExecuteChanged();
    }

    private async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<SearchPersonsHandler>();
            var result = await handler.ExecuteAsync(SearchText, cancellationToken);

            if (result.IsSuccess)
            {
                People.Clear();
                foreach (var person in result.Value!)
                    People.Add(person);

                SelectedPerson = SelectedPerson is null ? null : People.FirstOrDefault(p => p.Id == SelectedPerson.Id);
            }
            else
            {
                _notifier.ShowError(result.ErrorMessage!);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadAccountCardAsync()
    {
        if (SelectedPerson is null)
        {
            SelectedAccount = null;
            return;
        }

        var personId = SelectedPerson.Id;   // حارس السبق: تُقبل النتيجة فقط إن بقي نفس الشخص محدداً

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<GetPersonAccountHandler>();
        var result = await handler.ExecuteAsync(personId);

        if (result.IsSuccess && SelectedPerson?.Id == personId)
            SelectedAccount = result.Value;
        else if (!result.IsSuccess)
            _notifier.ShowError(result.ErrorMessage!);
    }

    // ---------- العمليات ----------

    private async Task AddAsync()
    {
        var editor = _services.GetRequiredService<PersonEditorViewModel>();
        editor.InitializeForCreate();

        if (await _dialogs.ShowDialogAsync(editor, editor.Title))
            await LoadAsync();
    }

    private async Task EditAsync()
    {
        if (SelectedPerson is null) return;

        var editor = _services.GetRequiredService<PersonEditorViewModel>();
        editor.InitializeForEdit(SelectedPerson);

        if (await _dialogs.ShowDialogAsync(editor, editor.Title))
            await LoadAsync();
    }

    private async Task DeactivateAsync()
    {
        var person = SelectedPerson;
        if (person is null) return;

        var confirmed = await _dialogs.ConfirmAsync(
            "تعطيل الشخص",
            $"سيُعطَّل «{person.FullName}» ويُخفى من قوائم الاختيار مستقبلاً، دون حذف بياناته. حساب دخوله (إن وجد) لا يتأثر. يمكن إعادة تفعيله في أي وقت.",
            "تعطيل");
        if (!confirmed) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<DeactivatePersonHandler>();
        var result = await handler.ExecuteAsync(new DeactivatePersonRequest(person.Id));
        await HandlePersonResultAsync(result.IsSuccess, result.ErrorMessage, result.ErrorType, $"عُطّل «{person.FullName}»");
    }

    private async Task ActivateAsync()
    {
        var person = SelectedPerson;
        if (person is null) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ActivatePersonHandler>();
        var result = await handler.ExecuteAsync(new ActivatePersonRequest(person.Id));
        await HandlePersonResultAsync(result.IsSuccess, result.ErrorMessage, result.ErrorType, $"فُعّل «{person.FullName}»");
    }

    private async Task CreateAccountAsync()
    {
        if (SelectedPerson is null || SelectedAccount is not null) return;

        var dialog = _services.GetRequiredService<CreateAccountViewModel>();
        dialog.Initialize(SelectedPerson);

        if (await _dialogs.ShowDialogAsync(dialog, "إنشاء حساب دخول"))
            await LoadAccountCardAsync();
    }

    private async Task UnlockAccountAsync()
    {
        var person = SelectedPerson;
        if (person is null || SelectedAccount is null) return;

        var confirmed = await _dialogs.ConfirmAsync(
            "فك قفل الحساب",
            $"سيُرفع القفل عن «{SelectedAccount.Username}» فوراً ويُصفَّر عداد المحاولات الفاشلة.",
            "فك القفل");
        if (!confirmed) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<UnlockUserAccountHandler>();
        var result = await handler.ExecuteAsync(new UnlockUserAccountRequest(person.Id));

        if (result.IsSuccess)
        {
            _notifier.ShowSuccess("فُكّ قفل الحساب ✔");
            await LoadAccountCardAsync();
        }
        else if (result.ErrorType == ErrorType.Unexpected)
            _notifier.ShowError(result.ErrorMessage!);
        else
            _notifier.ShowWarning(result.ErrorMessage!);
    }

    private async Task ResetPasswordAsync()
    {
        if (SelectedPerson is null || SelectedAccount is null) return;

        var dialog = _services.GetRequiredService<ResetPasswordViewModel>();
        dialog.Initialize(SelectedPerson);

        if (await _dialogs.ShowDialogAsync(dialog, "إعادة تعيين كلمة المرور"))
            await LoadAccountCardAsync();
    }

    // D-22 الموسَّعة (D-29): نجاح ← Toast · متوقع خارج الفورم ← تحذيري · غير متوقع ← خطأ
    private async Task HandlePersonResultAsync(bool isSuccess, string? errorMessage, ErrorType errorType, string successMessage)
    {
        if (isSuccess)
        {
            _notifier.ShowSuccess(successMessage);
            await LoadAsync();
        }
        else if (errorType == ErrorType.Unexpected)
            _notifier.ShowError(errorMessage!);
        else
            _notifier.ShowWarning(errorMessage!);
    }

    private async Task AssignStudentRoleAsync()
    {
        var person = SelectedPerson;
        if (person is null) return;

        var dialog = _services.GetRequiredService<AssignStudentRoleViewModel>();
        dialog.Initialize(person);
        await _dialogs.ShowDialogAsync(dialog, "إضافة ملف طالب");
        // لا إعادة تحميل — الملف لا يغيّر صف الشخص في الشبكة
    }

    private async Task AssignTeacherRoleAsync()
    {
        var person = SelectedPerson;
        if (person is null) return;

        var dialog = _services.GetRequiredService<AssignTeacherRoleViewModel>();
        dialog.Initialize(person);
        await _dialogs.ShowDialogAsync(dialog, "إضافة ملف أستاذ");
    }



}
