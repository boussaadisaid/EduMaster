using EduMaster.Application.Sms;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace EduMaster.UI.Sms;

public sealed class SmsTemplatesViewModel : BaseViewModel, IDialogViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;
    private readonly IDialogService _dialogs;
    private bool _changed;

    public SmsTemplatesViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier, IDialogService dialogs)
    {
        _scopeFactory = scopeFactory; _notifier = notifier; _dialogs = dialogs;
        NewCommand = new AsyncRelayCommand(NewAsync);
        EditCommand = new AsyncRelayCommand(EditAsync, () => Selected is not null);
        ToggleCommand = new AsyncRelayCommand(ToggleAsync, () => Selected is not null);
        CloseCommand = new AsyncRelayCommand(() => { CloseRequested?.Invoke(this, _changed); return Task.CompletedTask; });
    }

    public event EventHandler<bool>? CloseRequested;
    public ObservableCollection<SmsTemplateItem> Templates { get; } = new();
    private SmsTemplateItem? _selected;
    public SmsTemplateItem? Selected { get => _selected; set { if (SetProperty(ref _selected, value)) { EditCommand.RaiseCanExecuteChanged(); ToggleCommand.RaiseCanExecuteChanged(); } } }
    private bool _loading;
    public bool IsLoading { get => _loading; private set => SetProperty(ref _loading, value); }
    public AsyncRelayCommand NewCommand { get; }
    public AsyncRelayCommand EditCommand { get; }
    public AsyncRelayCommand ToggleCommand { get; }
    public AsyncRelayCommand CloseCommand { get; }

    public async Task InitializeAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var r = await scope.ServiceProvider.GetRequiredService<GetSmsTemplatesHandler>().ExecuteAsync(false);
            if (!r.IsSuccess) { _notifier.ShowError(r.ErrorMessage!); return; }
            Templates.Clear(); foreach (var item in r.Value!) Templates.Add(item);
            Selected = null;
        }
        finally { IsLoading = false; }
    }

    private async Task NewAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var vm = scope.ServiceProvider.GetRequiredService<SmsTemplateEditorViewModel>();
        vm.InitializeForCreate();
        if (await _dialogs.ShowDialogAsync(vm, "قالب SMS جديد")) { _changed = true; await LoadAsync(); }
    }

    private async Task EditAsync()
    {
        if (Selected is null) return;
        using var scope = _scopeFactory.CreateScope();
        var vm = scope.ServiceProvider.GetRequiredService<SmsTemplateEditorViewModel>();
        vm.InitializeForEdit(Selected);
        if (await _dialogs.ShowDialogAsync(vm, "تعديل قالب SMS")) { _changed = true; await LoadAsync(); }
    }

    private async Task ToggleAsync()
    {
        if (Selected is null) return;
        var title = Selected.IsActive ? "تعطيل القالب" : "تفعيل القالب";
        if (!await _dialogs.ConfirmAsync(title, Selected.IsActive ? "هل تريد تعطيل هذا القالب؟" : "هل تريد تفعيل هذا القالب؟", title)) return;
        await using var scope = _scopeFactory.CreateAsyncScope();
        var r = await scope.ServiceProvider.GetRequiredService<SetSmsTemplateActiveHandler>().ExecuteAsync(new SetSmsTemplateActiveRequest(Selected.Id), !Selected.IsActive);
        if (!r.IsSuccess) { _notifier.ShowWarning(r.ErrorMessage!); return; }
        _changed = true;
        await LoadAsync();
    }
}
