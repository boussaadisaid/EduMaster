using EduMaster.Application.Billing;
using EduMaster.Application.Reports;
using EduMaster.Application.Settings;
using EduMaster.Application.Scheduling;
using EduMaster.Application.Sms;
using EduMaster.Application.Students;
using EduMaster.Domain.Enums;
using EduMaster.UI.Common;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace EduMaster.UI.Sms;

public sealed class SmsViewModel : BaseViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;
    private readonly IDialogService _dialogs;
    private readonly DispatcherTimer _syncTimer;
    private string? _preferredDeviceId;
    private string _schoolName = "EduMaster";

    public SmsViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier, IDialogService dialogs)
    {
        _scopeFactory = scopeFactory;
        _notifier = notifier;
        _dialogs = dialogs;
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
        RefreshDevicesCommand = new AsyncRelayCommand(LoadDevicesAsync);
        SendTestCommand = new AsyncRelayCommand(SendTestAsync, () => CanSendTest);
        RefreshHistoryCommand = new AsyncRelayCommand(LoadHistoryAsync);
        SyncSelectedCommand = new AsyncRelayCommand(SyncSelectedAsync, () => SelectedHistory is not null);
        ManageTemplatesCommand = new AsyncRelayCommand(ManageTemplatesAsync);
        LoadRecipientsCommand = new AsyncRelayCommand(LoadRecipientsAsync);
        SelectAllRecipientsCommand = new RelayCommand(SelectAllRecipients);
        ClearRecipientsCommand = new RelayCommand(ClearRecipients);
        SendSelectedCommand = new AsyncRelayCommand(SendSelectedAsync, () => CanSendSelected);
        LoadAbsenceSessionsCommand = new AsyncRelayCommand(LoadAbsenceSessionsAsync);
        _recipientSource = RecipientSources[0];

        _syncTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
        _syncTimer.Tick += async (_, _) => await SyncPendingAsync();
    }

    public ObservableCollection<SmsProviderDevice> Devices { get; } = new();
    public ObservableCollection<SmsHistoryItem> History { get; } = new();
    public ObservableCollection<SmsTemplateItem> Templates { get; } = new();
    public ObservableCollection<SmsRecipientDraft> Recipients { get; } = new();
    public ObservableCollection<ClassSessionListItem> AbsenceSessions { get; } = new();

    private string _apiKey = string.Empty;
    public string ApiKey { get => _apiKey; set { if (SetProperty(ref _apiKey, value)) { SendTestCommand.RaiseCanExecuteChanged(); } } }

    private SmsProviderDevice? _selectedDevice;
    public SmsProviderDevice? SelectedDevice { get => _selectedDevice; set { if (SetProperty(ref _selectedDevice, value)) SendTestCommand.RaiseCanExecuteChanged(); } }

    private string _testPhone = string.Empty;
    public string TestPhone { get => _testPhone; set { if (SetProperty(ref _testPhone, value)) SendTestCommand.RaiseCanExecuteChanged(); } }
    private string _testMessage = "هذه رسالة اختبار من EduMaster.";
    public string TestMessage { get => _testMessage; set { if (SetProperty(ref _testMessage, value)) SendTestCommand.RaiseCanExecuteChanged(); } }

    private SmsHistoryItem? _selectedHistory;
    public SmsHistoryItem? SelectedHistory { get => _selectedHistory; set { if (SetProperty(ref _selectedHistory, value)) SyncSelectedCommand.RaiseCanExecuteChanged(); } }

    private SmsTemplateItem? _selectedTemplate;
    public SmsTemplateItem? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            if (!SetProperty(ref _selectedTemplate, value)) return;
            if (value is not null) MessageText = value.Body;
            OnPropertyChanged(nameof(SelectedRecipientCount));
            SendSelectedCommand.RaiseCanExecuteChanged();
        }
    }

    private string _messageText = string.Empty;
    public string MessageText { get => _messageText; set { if (SetProperty(ref _messageText, value)) SendSelectedCommand.RaiseCanExecuteChanged(); } }

    private string _searchTerm = string.Empty;
    public string SearchTerm { get => _searchTerm; set => SetProperty(ref _searchTerm, value); }

    public ObservableCollection<SmsRecipientSourceOption> RecipientSources { get; } = new()
    {
        new(SmsRecipientSource.Students, "الطلاب"),
        new(SmsRecipientSource.Debtors, "الطلاب المدينون"),
        new(SmsRecipientSource.LowSessionBalances, "أرصدة الحصص المنخفضة"),
        new(SmsRecipientSource.AbsentStudents, "الغائبون عن حصة")
    };
    private SmsRecipientSourceOption _recipientSource;
    public SmsRecipientSourceOption RecipientSource { get => _recipientSource; set => SetProperty(ref _recipientSource, value); }

    private DateTime _absenceDate = DateTime.Today;
    public DateTime AbsenceDate { get => _absenceDate; set => SetProperty(ref _absenceDate, value); }
    private ClassSessionListItem? _selectedAbsenceSession;
    public ClassSessionListItem? SelectedAbsenceSession { get => _selectedAbsenceSession; set => SetProperty(ref _selectedAbsenceSession, value); }

    private bool _isLoading;
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    private string _connectionText = "غير مُعد";
    public string ConnectionText { get => _connectionText; private set => SetProperty(ref _connectionText, value); }
    public string SchoolName => _schoolName;
    public int SelectedRecipientCount => Recipients.Count(x => x.IsSelected);
    public int RecipientCount => Recipients.Count;
    public bool CanSendTest => !string.IsNullOrWhiteSpace(ApiKey) && SelectedDevice is not null && !string.IsNullOrWhiteSpace(TestPhone) && !string.IsNullOrWhiteSpace(TestMessage);
    public bool CanSendSelected => SelectedDevice is not null && Recipients.Any(x => x.IsSelected) && !string.IsNullOrWhiteSpace(MessageText) && MessageText.Trim().Length <= 1000;

    public AsyncRelayCommand SaveSettingsCommand { get; }
    public AsyncRelayCommand RefreshDevicesCommand { get; }
    public AsyncRelayCommand SendTestCommand { get; }
    public AsyncRelayCommand RefreshHistoryCommand { get; }
    public AsyncRelayCommand SyncSelectedCommand { get; }
    public AsyncRelayCommand ManageTemplatesCommand { get; }
    public AsyncRelayCommand LoadRecipientsCommand { get; }
    public RelayCommand SelectAllRecipientsCommand { get; }
    public RelayCommand ClearRecipientsCommand { get; }
    public AsyncRelayCommand SendSelectedCommand { get; }
    public AsyncRelayCommand LoadAbsenceSessionsCommand { get; }

    public async Task InitializeAsync()
    {
        await LoadSchoolNameAsync();
        await LoadSettingsAsync();
        await LoadDevicesAsync();
        await LoadTemplatesAsync();
        await LoadHistoryAsync();
        StartPolling();
    }

    public void StartPolling() => _syncTimer.Start();
    public void StopPolling() => _syncTimer.Stop();

    private async Task LoadSchoolNameAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<GetSchoolInfoHandler>().ExecuteAsync();
        if (result.IsSuccess) _schoolName = result.Value?.DisplayName ?? "EduMaster";
        OnPropertyChanged(nameof(SchoolName));
    }

    private async Task LoadSettingsAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<GetSmsSettingsHandler>().ExecuteAsync();
        if (!result.IsSuccess) { _notifier.ShowError(result.ErrorMessage!); return; }
        ApiKey = result.Value!.ApiKey ?? string.Empty;
        _preferredDeviceId = result.Value.DeviceId;
        ConnectionText = result.Value.IsConfigured ? "مُعد" : "غير مُعد";
    }

    private async Task LoadDevicesAsync()
    {
        if (string.IsNullOrWhiteSpace(ApiKey)) { ConnectionText = "أدخل مفتاح API ثم حدّث الأجهزة"; return; }
        IsLoading = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<GetSmsDevicesHandler>().ExecuteAsync(ApiKey);
            if (!result.IsSuccess) { _notifier.ShowWarning(result.ErrorMessage!); return; }
            Devices.Clear(); foreach (var item in result.Value!) Devices.Add(item);
            SelectedDevice = Devices.FirstOrDefault(x => x.Id == _preferredDeviceId)
                ?? Devices.FirstOrDefault(x => x.IsDefault && x.Enabled)
                ?? Devices.FirstOrDefault(x => x.Enabled);
            ConnectionText = Devices.Count == 0 ? "لا يوجد هاتف مسجّل" : $"تم العثور على {Devices.Count} جهاز";
        }
        finally { IsLoading = false; SendTestCommand.RaiseCanExecuteChanged(); SendSelectedCommand.RaiseCanExecuteChanged(); }
    }

    private async Task<bool> SaveSettingsCoreAsync()
    {
        if (SelectedDevice is null) { _notifier.ShowWarning("حدّد هاتف الإرسال أولاً."); return false; }
        await using var scope = _scopeFactory.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<SaveSmsSettingsHandler>().ExecuteAsync(new SaveSmsSettingsRequest(ApiKey, SelectedDevice.Id));
        if (!result.IsSuccess) { _notifier.ShowWarning(result.ErrorMessage!); return false; }
        _preferredDeviceId = SelectedDevice.Id;
        ConnectionText = "مُعد";
        return true;
    }

    private async Task SaveSettingsAsync()
    {
        if (await SaveSettingsCoreAsync()) _notifier.ShowSuccess("تم حفظ إعدادات SMS بأمان على هذا الجهاز.");
    }

    private async Task SendTestAsync()
    {
        if (!CanSendTest || SelectedDevice is null) return;
        if (!await SaveSettingsCoreAsync()) return;
        var normalized = NormalizeForSms(TestPhone);
        if (normalized is null) { _notifier.ShowWarning("رقم الهاتف غير صالح. استخدم رقمًا جزائريًا مثل 0550123456."); return; }
        await SendRecipientsAsync(
            new List<SmsRecipientDraft> { new(null, null, "اختبار", null, normalized) },
            SmsMessageCategory.Administrative, null, TestMessage.Trim(), "تم قبول رسالة الاختبار للإرسال.");
    }

    private async Task LoadTemplatesAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<GetSmsTemplatesHandler>().ExecuteAsync(true);
        if (!result.IsSuccess) { _notifier.ShowWarning(result.ErrorMessage!); return; }
        Templates.Clear(); foreach (var item in result.Value!) Templates.Add(item);
        SelectedTemplate = Templates.FirstOrDefault();
    }

    private async Task ManageTemplatesAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var vm = scope.ServiceProvider.GetRequiredService<SmsTemplatesViewModel>();
        await vm.InitializeAsync();
        if (await _dialogs.ShowDialogAsync(vm, "قوالب SMS"))
        {
            await LoadTemplatesAsync();
        }
    }

    private async Task LoadRecipientsAsync()
    {
        Recipients.Clear();
        if (RecipientSource.Kind == SmsRecipientSource.AbsentStudents)
        {
            await LoadAbsenceSessionsAsync();
            if (SelectedAbsenceSession is null) { _notifier.ShowWarning("اختر الحصة أولاً."); return; }
            await LoadAbsentRecipientsAsync(SelectedAbsenceSession.Id);
        }
        else if (RecipientSource.Kind == SmsRecipientSource.Debtors)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<GetDebtorsHandler>().ExecuteAsync(SearchTerm);
            if (!result.IsSuccess) { _notifier.ShowWarning(result.ErrorMessage!); return; }
            foreach (var item in result.Value!)
            {
                var phone = NormalizeForSms(item.Phone);
                if (phone is null) continue;
                Recipients.Add(new SmsRecipientDraft(null, item.StudentId, item.FullName, null, phone, item.RemainingCentimes));
            }
        }
        else if (RecipientSource.Kind == SmsRecipientSource.LowSessionBalances)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<GetLowSessionBalancesHandler>().ExecuteAsync();
            if (!result.IsSuccess) { _notifier.ShowWarning(result.ErrorMessage!); return; }
            foreach (var item in result.Value!)
            {
                if (!string.IsNullOrWhiteSpace(SearchTerm) && !item.StudentName.Contains(SearchTerm.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                var phone = NormalizeForSms(item.StudentPhone) ?? NormalizeForSms(item.GuardianPhone);
                if (phone is null) continue;
                Recipients.Add(new SmsRecipientDraft(null, item.StudentId, item.StudentName, item.GuardianName, phone, null, item.Balance, item.SubjectName));
            }
        }
        else
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<SearchStudentsHandler>().ExecuteAsync(SearchTerm);
            if (!result.IsSuccess) { _notifier.ShowWarning(result.ErrorMessage!); return; }
            foreach (var item in result.Value!)
            {
                var phone = NormalizeForSms(item.Phone);
                if (phone is null) continue;
                Recipients.Add(new SmsRecipientDraft(item.PersonId, item.Id, item.FullName, item.GuardianFullName, phone));
            }
        }
        OnPropertyChanged(nameof(RecipientCount)); OnPropertyChanged(nameof(SelectedRecipientCount)); SendSelectedCommand.RaiseCanExecuteChanged();
    }

    private async Task LoadAbsenceSessionsAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<GetSessionsHandler>().ExecuteAsync(AbsenceDate.Date, AbsenceDate.Date, null);
        if (!result.IsSuccess) { _notifier.ShowWarning(result.ErrorMessage!); return; }
        AbsenceSessions.Clear();
        foreach (var session in result.Value!.Where(x => x.Status == SessionStatus.Held).OrderBy(x => x.StartsAt)) AbsenceSessions.Add(session);
        if (SelectedAbsenceSession is null || !AbsenceSessions.Contains(SelectedAbsenceSession)) SelectedAbsenceSession = AbsenceSessions.FirstOrDefault();
    }

    private async Task LoadAbsentRecipientsAsync(int sessionId)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var attendance = await scope.ServiceProvider.GetRequiredService<GetSessionAttendanceHandler>().ExecuteAsync(sessionId);
        if (!attendance.IsSuccess) { _notifier.ShowWarning(attendance.ErrorMessage!); return; }
        var absent = attendance.Value!.Where(x => x.Status == AttendanceStatus.Absent).ToList();
        if (absent.Count == 0) { _notifier.ShowWarning("لا توجد حالات غياب في الحصة المحددة."); return; }

        var studentsResult = await scope.ServiceProvider.GetRequiredService<SearchStudentsHandler>().ExecuteAsync(null);
        if (!studentsResult.IsSuccess) { _notifier.ShowWarning(studentsResult.ErrorMessage!); return; }
        var byId = studentsResult.Value!.ToDictionary(x => x.Id);
        foreach (var mark in absent)
        {
            if (!byId.TryGetValue(mark.StudentId, out var student)) continue;
            var phone = NormalizeForSms(student.Phone);
            if (phone is null) continue;
            Recipients.Add(new SmsRecipientDraft(student.PersonId, student.Id, student.FullName, student.GuardianFullName, phone,
                null, null, SelectedAbsenceSession?.SubjectName, SelectedAbsenceSession?.StartsAt.ToString("dd/MM/yyyy")));
        }
        OnPropertyChanged(nameof(RecipientCount)); OnPropertyChanged(nameof(SelectedRecipientCount)); SendSelectedCommand.RaiseCanExecuteChanged();
    }

    private async Task SendSelectedAsync()
    {
        var selected = Recipients.Where(x => x.IsSelected).ToList();
        if (selected.Count == 0) { _notifier.ShowWarning("اختر مستلمًا واحدًا على الأقل."); return; }
        var preview = MessageText.Replace("{SchoolName}", SchoolName, StringComparison.Ordinal);
        if (!await _dialogs.ConfirmAsync("تأكيد إرسال SMS", $"سيتم إرسال {selected.Count} رسالة. هل تريد المتابعة؟\n\n{preview}", "إرسال")) return;
        var category = SelectedTemplate?.Category ?? SmsMessageCategory.Custom;
        await SendRecipientsAsync(selected, category, SelectedTemplate?.Id, MessageText.Trim(), $"تم قبول {selected.Count} رسالة للإرسال.");
    }

    private async Task SendRecipientsAsync(IEnumerable<SmsRecipientDraft> drafts, SmsMessageCategory category, int? templateId, string templateBody, string successMessage)
    {
        if (!await SaveSettingsCoreAsync()) return;
        var schoolName = SchoolName;
        var recipients = drafts.Select(d => new SmsSendRecipient(d.PersonId, d.StudentId, d.PhoneNumber, RenderTemplate(templateBody, d, schoolName))).ToList();
        if (recipients.Count == 0) { _notifier.ShowWarning("لا يوجد مستلم صالح للإرسال."); return; }
        await using var scope = _scopeFactory.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<SendSmsBatchHandler>().ExecuteAsync(new SendSmsRequest(category, templateId, recipients));
        if (!result.IsSuccess) { _notifier.ShowWarning(result.ErrorMessage!); return; }
        _notifier.ShowSuccess(successMessage);
        await LoadHistoryAsync();
    }

    private static string? NormalizeForSms(string? phone)
    {
        var digits = PhoneNumberNormalizer.ToWhatsAppInternational(phone);
        return digits is null ? null : "+" + digits;
    }

    private static string RenderTemplate(string template, SmsRecipientDraft r, string schoolName)
        => SmsTemplateRenderer.Render(template, new SmsTemplateRenderData(
            r.FullName, r.GuardianName, r.AmountCentimes, r.AmountCentimes, r.RemainingSessions,
            r.SubjectName, r.DateText ?? DateTime.Today.ToString("dd/MM/yyyy"), schoolName, null));

    private void SelectAllRecipients() { foreach (var x in Recipients) x.IsSelected = true; OnPropertyChanged(nameof(SelectedRecipientCount)); SendSelectedCommand.RaiseCanExecuteChanged(); }
    private void ClearRecipients() { foreach (var x in Recipients) x.IsSelected = false; OnPropertyChanged(nameof(SelectedRecipientCount)); SendSelectedCommand.RaiseCanExecuteChanged(); }

    private async Task LoadHistoryAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<GetSmsHistoryHandler>().ExecuteAsync();
        if (!result.IsSuccess) { _notifier.ShowError(result.ErrorMessage!); return; }
        History.Clear(); foreach (var row in result.Value!) History.Add(row);
    }

    private async Task SyncSelectedAsync()
    {
        if (SelectedHistory is null) return;
        await using var scope = _scopeFactory.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<SyncSmsBatchHandler>().ExecuteAsync(SelectedHistory.BatchId);
        if (!result.IsSuccess) _notifier.ShowWarning(result.ErrorMessage!);
        await LoadHistoryAsync();
    }

    private async Task SyncPendingAsync()
    {
        var batches = History.Select(x => x.BatchId).Distinct().Take(10).ToList();
        foreach (var batchId in batches)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<SyncSmsBatchHandler>().ExecuteAsync(batchId);
        }
        await LoadHistoryAsync();
    }
}

public enum SmsRecipientSource { Students = 1, Debtors = 2, LowSessionBalances = 3, AbsentStudents = 4 }
public sealed record SmsRecipientSourceOption(SmsRecipientSource Kind, string DisplayName);
