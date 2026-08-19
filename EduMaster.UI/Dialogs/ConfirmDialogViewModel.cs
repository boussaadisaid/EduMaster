using EduMaster.UI.Common.MVVM;


namespace EduMaster.UI.Dialogs
{
    public sealed class ConfirmDialogViewModel : BaseViewModel, IDialogViewModel
    {
        public event EventHandler<bool>? CloseRequested;

        public ConfirmDialogViewModel()
        {
            ConfirmCommand = new AsyncRelayCommand(() =>
            {
                CloseRequested?.Invoke(this, true);
                return Task.CompletedTask;
            });
            CancelCommand = new AsyncRelayCommand(() =>
            {
                CloseRequested?.Invoke(this, false);
                return Task.CompletedTask;
            });
        }

        public string Message { get; private set; } = string.Empty;
        public string ConfirmText { get; private set; } = "تأكيد";

        public AsyncRelayCommand ConfirmCommand { get; }
        public AsyncRelayCommand CancelCommand { get; }

        public void Initialize(string message, string confirmText)
        {
            Message = message;
            ConfirmText = confirmText;
            OnPropertyChanged(nameof(Message));
            OnPropertyChanged(nameof(ConfirmText));
        }
    }
}
