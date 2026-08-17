using ToastNotifications;
using ToastNotifications.Lifetime;
using ToastNotifications.Messages;
using ToastNotifications.Position;



namespace EduMaster.UI.Common.Services
{
    public sealed class ToastUserNotifier : IUserNotifier, IDisposable
    {
        private readonly Notifier _notifier;

        public ToastUserNotifier()
        {
            _notifier = new Notifier(cfg =>
            {
                // توجيه مناسب للواجهات العربية RTL
                cfg.PositionProvider = new PrimaryScreenPositionProvider(
                    corner: Corner.BottomLeft,
                    offsetX: 16,
                    offsetY: 16);

                cfg.LifetimeSupervisor = new TimeAndCountBasedLifetimeSupervisor(
                    notificationLifetime: TimeSpan.FromSeconds(4),
                    maximumNotificationCount: MaximumNotificationCount.FromCount(5));

                cfg.Dispatcher = System.Windows.Application.Current.Dispatcher;
            });
        }

        public void ShowSuccess(string message) => _notifier.ShowSuccess(message);
        public void ShowError(string message) => _notifier.ShowError(message);
        public void ShowWarning(string message) => _notifier.ShowWarning(message);
        public void ShowInfo(string message) => _notifier.ShowInformation(message);

        public void Dispose()
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;

            if (dispatcher is null)
            {
                _notifier.Dispose();
                return;
            }

            if (dispatcher.CheckAccess())
            {
                _notifier.Dispose();
            }
            else
            {
                dispatcher.Invoke(_notifier.Dispose);
            }
        }


    }
}
