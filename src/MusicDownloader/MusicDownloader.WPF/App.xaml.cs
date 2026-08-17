using Microsoft.Extensions.DependencyInjection;
using MusicDownloader.Vk.Services.AuthorizationServices.Vk;
using MusicDownloader.Vk.Services.MusicDownloader;
using MusicDownloader.Vk.Services.VkServices;
using System.Windows;
using Application = System.Windows.Application;

namespace MusicDownloader.WPF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private ServiceProvider? _serviceProvider;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var services = new ServiceCollection();

            ConfigureServices(services);

            _serviceProvider = services.BuildServiceProvider();

            var mainWindow =
                _serviceProvider.GetRequiredService<MainWindow>();

            mainWindow.Show();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<VkTokenStorage>();

            services.AddTransient<VkServices>();

            services.AddHttpClient<IMusicDownloader,VkMusicDownloader>();

            services.AddTransient<MainWindow>();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _serviceProvider?.Dispose();

            base.OnExit(e);
        }
    }

}
