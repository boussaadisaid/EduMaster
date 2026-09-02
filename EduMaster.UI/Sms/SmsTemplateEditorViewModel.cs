using EduMaster.Application.Sms;
using EduMaster.Application.Common;
using EduMaster.Domain.Enums;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace EduMaster.UI.Sms;

public sealed class SmsTemplateEditorViewModel : BaseViewModel, IDialogViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;
    private int? _editingId;

    public SmsTemplateEditorViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier)
    {
        _scopeFactory = scopeFactory;
        _notifier = notifier;
        Categories = new ObservableCollection<SmsCategoryOption>(new SmsCategoryOption[]
        {
            new(SmsMessageCategory.DebtReminder, "تذكير بالدين"),
            new(SmsMessageCategory.PaymentConfirmation, "تأكيد الدفع"),
            new(SmsMessageCategory.AbsenceNotification, "إشعار الغياب"),
            new(SmsMessageCategory.SessionBalanceNotification, "نهاية الحصص"),
            new(SmsMessageCategory.Administrative, "رسالة إدارية"),
            new(SmsMessageCategory.Custom, "رسالة عامة")
        });
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsSaving);
        CancelCommand = new AsyncRelayCommand(() => { CloseRequested?.Invoke(this, false); return Task.CompletedTask; });
    }

    public event EventHandler<bool>? CloseRequested;
    public string Title => _editingId is null ? "قالب SMS جديد" : "تعديل قالب SMS";
    public ObservableCollection<SmsCategoryOption> Categories { get; }
    private string _name = string.Empty;
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    private SmsCategoryOption? _category;
    public SmsCategoryOption? Category { get => _category; set => SetProperty(ref _category, value); }
    private string _body = string.Empty;
    public string Body { get => _body; set => SetProperty(ref _body, value); }
    private bool _saving;
    public bool IsSaving { get => _saving; private set { if (SetProperty(ref _saving, value)) SaveCommand.RaiseCanExecuteChanged(); } }
    private string? _error;
    public string? ErrorMessage { get => _error; private set { SetProperty(ref _error, value); OnPropertyChanged(nameof(HasErrorMessage)); } }
    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);
    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand CancelCommand { get; }

    public void InitializeForCreate()
    {
        _editingId = null;
        Name = string.Empty;
        Category = Categories.First(x => x.Kind == SmsMessageCategory.Custom);
        Body = "السلام عليكم، نحيطكم علماً بما يلي: ... مع تحيات {SchoolName}.";
        ErrorMessage = null;
        OnPropertyChanged(nameof(Title));
    }

    public void InitializeForEdit(SmsTemplateItem item)
    {
        _editingId = item.Id;
        Name = item.Name;
        Category = Categories.FirstOrDefault(x => x.Kind == item.Category) ?? Categories.First();
        Body = item.Body;
        ErrorMessage = null;
        OnPropertyChanged(nameof(Title));
    }

    private async Task SaveAsync()
    {
        ErrorMessage = null;
        if (Category is null) { ErrorMessage = "اختر نوع الرسالة."; return; }
        IsSaving = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            if (_editingId is null)
            {
                var result = await scope.ServiceProvider.GetRequiredService<CreateSmsTemplateHandler>().ExecuteAsync(
                    new CreateSmsTemplateRequest(Name, Category.Kind, Body));
                if (!result.IsSuccess) { ErrorMessage = result.ErrorMessage; return; }
            }
            else
            {
                var result = await scope.ServiceProvider.GetRequiredService<UpdateSmsTemplateHandler>().ExecuteAsync(
                    new UpdateSmsTemplateRequest(_editingId.Value, Name, Category.Kind, Body));
                if (!result.IsSuccess) { ErrorMessage = result.ErrorMessage; return; }
            }

            _notifier.ShowSuccess(_editingId is null ? "تم إنشاء القالب." : "تم تعديل القالب.");
            CloseRequested?.Invoke(this, true);
        }
        finally { IsSaving = false; }
    }

}

public sealed record SmsCategoryOption(SmsMessageCategory Kind, string DisplayName);
