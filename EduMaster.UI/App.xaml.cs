using EduMaster.Application.Abstractions;
using EduMaster.Application.DependencyInjection;
using EduMaster.Infrastructure.DependencyInjection;
using EduMaster.Infrastructure.Persistence;
using EduMaster.UI.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System.IO;
using System.Windows;
using System.Windows.Threading;


namespace EduMaster.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private IHost? _host;
        private ILogger<App>? _logger;

        protected override async void OnStartup(StartupEventArgs e)
        {
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            base.OnStartup(e);
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;


            try
            {
                _host = Host.CreateDefaultBuilder(e.Args)
                    .UseSerilog((context, services, configuration) =>
                    {
                        configuration.WriteTo.File(
                            Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                "EduMaster", "Logs", "edumaster-.log"),
                            rollingInterval: RollingInterval.Day,
                            retainedFileCountLimit: 14,
                            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}");
                    })
                    .ConfigureServices((context, services) =>
                    {
                        var cs = context.Configuration.GetConnectionString("DefaultConnection")
                                 ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");
                        services.AddApplication();
                        services.AddInfrastructure(cs);
                        services.AddPresentation();
                    })
                    .Build();

                _logger = _host.Services.GetRequiredService<ILogger<App>>();

                await _host.StartAsync();


                var loginWindow = _host.Services.GetRequiredService<LoginWindow>();
                MainWindow = loginWindow;
                loginWindow.Show();
            }
            catch (Exception ex)
            {
                _logger?.LogCritical(ex, "فشل بدء تشغيل التطبيق");
                MessageBox.Show(
                    $"حدث خطأ أثناء تشغيل التطبيق:\n\n{ex.Message}",
                    "خطأ في التشغيل",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown();
            }
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            _logger?.LogCritical(e.Exception, "An unhandled exception occurred on the WPF UI thread.");

            MessageBox.Show(
                "حدث خطأ غير متوقع في التطبيق.\n\n" +
                "تم تسجيل تفاصيل الخطأ ويمكن مراجعتها لاحقًا.",
                "خطأ غير متوقع",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            e.Handled = true;
        }

        private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            _logger?.LogCritical(ex, "استثناء قاتل غير مُمسك خارج UI Thread (IsTerminating: {IsTerminating})", e.IsTerminating);
            Log.CloseAndFlush(); // ضروري هنا لأن Windows ستُنهي العملية فورًا تقريبًا بعد هذا الحدث
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            _logger?.LogError(e.Exception, "استثناء Task غير مُلاحَظ (Unobserved)");
            e.SetObserved();
        }


        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                _host?.StopAsync(TimeSpan.FromSeconds(5))
                     .GetAwaiter()
                     .GetResult();
            }
            finally
            {
                _host?.Dispose();
                Log.CloseAndFlush();
            }
            base.OnExit(e);
        }


    }

}
