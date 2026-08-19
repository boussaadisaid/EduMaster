using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using EduMaster.UI.Dialogs;
using Microsoft.Extensions.DependencyInjection;


namespace EduMaster.UI.Services
{
    public sealed class DialogService : IDialogService
    {
        private readonly IServiceProvider _services;

        public DialogService(IServiceProvider services)
        {
            _services = services;
        }

        public Task<bool> ShowDialogAsync(BaseViewModel viewModel, string title)
        {
            var window = _services.GetRequiredService<DialogWindow>();
            window.Title = title;
            window.DataContext = viewModel;
            window.Owner = System.Windows.Application.Current.MainWindow;

            var result = false;
            if (viewModel is IDialogViewModel dialogVm)
                dialogVm.CloseRequested += (_, r) =>
                {
                    result = r;
                    window.DialogResult = r;   // تعيينها يغلق النافذة
                };

            window.ShowDialog();              // الإغلاق بـ X يبقي result = false
            return Task.FromResult(result);
        }

        public Task<bool> ConfirmAsync(string title, string message, string confirmText = "تأكيد")
        {
            var vm = _services.GetRequiredService<ConfirmDialogViewModel>();
            vm.Initialize(message, confirmText);
            return ShowDialogAsync(vm, title);
        }
    }
}
