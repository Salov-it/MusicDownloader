using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.Logging;
using MusicDownloader.Vk.Services.AuthorizationServices.Vk;
using MusicDownloader.Vk.Services.MusicDownloader;
using MusicDownloader.Vk.Services.VkServices;
using Serilog;
using System.IO;
using System.Windows;
using Application = System.Windows.Application;
using Log = Serilog.Log;

namespace MusicDownloader.WPF
{
    public partial class App : Application
    {
        private ServiceProvider? _serviceProvider;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ConfigureLogging();

            var services = new ServiceCollection();

            ConfigureServices(services);

            _serviceProvider = services.BuildServiceProvider();

            var mainWindow =
                _serviceProvider.GetRequiredService<MainWindow>();

            mainWindow.Show();
        }

        private static void ConfigureLogging()
        {
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MusicDownloader",
                "Logs");

            Directory.CreateDirectory(logDirectory);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(
                    path: Path.Combine(logDirectory, "app-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14,
                    outputTemplate:
                        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(logging =>
            {
                logging.AddSerilog(Log.Logger, dispose: false);
            });

            services.AddSingleton<VkTokenStorage>();

            services.AddTransient<VkServices>();

            services.AddHttpClient<IMusicDownloader, VkMusicDownloader>();

            services.AddTransient<MainWindow>();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _serviceProvider?.Dispose();

            Log.CloseAndFlush();

            base.OnExit(e);
        }
    }
}