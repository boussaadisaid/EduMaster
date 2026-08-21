using EduMaster.Application.Academic;
using EduMaster.Application.Common;
using EduMaster.Domain.Academic;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EduMaster.UI.Academic;

public sealed class RoomEditorViewModel : BaseViewModel, IDialogViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserNotifier _notifier;
    private int? _editingId;

    public RoomEditorViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier)
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
    public string Title => _editingId is null ? "قاعة جديدة" : "تعديل القاعة";

    private string _name = string.Empty;
    public string Name { get => _name; set => SetProperty(ref _name, value); }

    // السعة كنص — فارغة = بلا سعة (ح-3: القاعة وسعتها اختياريان)
    private string _capacityText = string.Empty;
    public string CapacityText { get => _capacityText; set => SetProperty(ref _capacityText, value); }

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
    }

    public void InitializeForEdit(Room room)
    {
        _editingId = room.Id;
        Name = room.Name;
        CapacityText = room.Capacity?.ToString() ?? string.Empty;
    }

    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "أدخل اسم القاعة.";
            return;
        }

        int? capacity = null;
        if (!string.IsNullOrWhiteSpace(CapacityText))
        {
            if (!int.TryParse(CapacityText, out var parsed))
            {
                ErrorMessage = "أدخل رقماً صحيحاً للسعة أو اتركها فارغة.";
                return;
            }
            capacity = parsed;
        }

        IsSaving = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();

            if (_editingId is null)
            {
                var handler = scope.ServiceProvider.GetRequiredService<CreateRoomHandler>();
                var result = await handler.ExecuteAsync(new CreateRoomRequest(Name, capacity));
                if (!HandleResult(result.IsSuccess, result.ErrorMessage, result.ErrorType, "أُضيفت القاعة بنجاح ✔"))
                    return;
            }
            else
            {
                var handler = scope.ServiceProvider.GetRequiredService<UpdateRoomHandler>();
                var result = await handler.ExecuteAsync(new UpdateRoomRequest(_editingId.Value, Name, capacity));
                if (!HandleResult(result.IsSuccess, result.ErrorMessage, result.ErrorType, "حُفظت القاعة ✔"))
                    return;
            }

            CloseRequested?.Invoke(this, true);
        }
        finally
        {
            IsSaving = false;
        }
    }

    private bool HandleResult(bool isSuccess, string? errorMessage, ErrorType errorType, string successMessage)
    {
        if (isSuccess)
        {
            _notifier.ShowSuccess(successMessage);
            return true;
        }

        if (errorType == ErrorType.Unexpected)
            _notifier.ShowError(errorMessage!);
        else
            ErrorMessage = errorMessage;   // Conflict/Validation ← بانر (D-22) · والكيان يصدّ السعة الصفرية برسالة عربية

        return false;
    }
}