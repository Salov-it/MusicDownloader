using Microsoft.Web.WebView2.Core;
using MusicDownloader.Vk.Services.AuthorizationServices.Vk;
using MusicDownloader.Vk.Services.AuthorizationServices.Vk.Models;
using MusicDownloader.Vk.Services.MusicDownloader;
using MusicDownloader.Vk.Services.MusicDownloader.Models;
using MusicDownloader.Vk.Services.VkServices;
using MusicDownloader.WPF.Views.VK;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Color = System.Windows.Media.Color;
using MessageBox = System.Windows.MessageBox;
using Path = System.IO.Path;

namespace MusicDownloader.WPF
{
    public partial class MainWindow : Window
    {
        private readonly VkTokenStorage _vkTokenStorage;
        private readonly IMusicDownloader _musicDownloader;

        private VkServices? _vkServices;

        public MainWindow(VkTokenStorage vkTokenStorage,IMusicDownloader musicDownloader)
        {
            InitializeComponent();

            _vkTokenStorage = vkTokenStorage;
            _musicDownloader = musicDownloader;

            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            Loaded -= MainWindow_Loaded;

            await LoadVkAsync();
        }

        /// <summary>
        /// Загружает сохранённый VK токен
        /// и создаёт VkServices.
        /// </summary>
        private async Task LoadVkAsync()
        {
            try
            {
                VkToken? token =
                    await _vkTokenStorage.LoadAsync();

                if (token is null ||
                    string.IsNullOrWhiteSpace(token.AccessToken))
                {
                    SetVkNotAuthorized();

                    return;
                }

                _vkServices =
                    await VkServices.CreateAsync(
                        _vkTokenStorage);

                SetVkAuthorized();
            }
            catch (Exception ex)
            {
                _vkServices = null;

                SetVkNotAuthorized();

                MessageBox.Show(
                    $"Не удалось загрузить авторизацию VK:\n\n{ex.Message}",
                    "MusicDownloader",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Кнопка ВКонтакте.
        /// </summary>
        private async void VkButton_Click(object sender,RoutedEventArgs e)
        {
            try
            {
                // Если VK уже авторизован —
                // сразу открываем VK Музыку.
                if (_vkServices is not null)
                {
                    OpenVkMusic();
                    return;
                }

                // Если авторизации нет —
                // открываем окно авторизации.
                var loginWindow =
                    new VkLoginWindow
                    {
                        Owner = this
                    };

                bool? result =
                    loginWindow.ShowDialog();

                if (result != true)
                    return;

                await LoadVkAsync();

                if (_vkServices is null)
                    return;

                MessageBox.Show(
                    "ВКонтакте успешно подключён!",
                    "MusicDownloader",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                VkStatusText.Text = "Авторизован";
                LogoutButton.Visibility = Visibility.Visible;

                OpenVkMusic();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка авторизации VK:\n\n{ex.Message}",
                    "MusicDownloader",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void SetVkAuthorized()
        {
            VkStatusText.Text =
                "Авторизован";

            VkStatusText.Foreground =
                new SolidColorBrush(
                    Color.FromRgb(
                        49,
                        183,
                        122));
        }

        private void SetVkNotAuthorized()
        {
            VkStatusText.Text =
                "Не авторизован";

            VkStatusText.Foreground =
                new SolidColorBrush(
                    Color.FromRgb(
                        138,
                        150,
                        168));
        }

        /// <summary>
        /// Открывает VK Музыку внутри MainWindow.
        /// </summary>
        public void OpenVkMusic()
        {
            if (_vkServices is null)
                return;

            HomeContent.Visibility =
                Visibility.Collapsed;

            MainContent.Visibility =
                Visibility.Visible;

            MainContent.Content =
                new VkMusicView(
                    _vkServices,
                    _musicDownloader);
        }

        /// <summary>
        /// Возвращает пользователя
        /// в главное меню.
        /// </summary>
        public void ShowMainMenu()
        {
            MainContent.Content = null;

            MainContent.Visibility =
                Visibility.Collapsed;

            HomeContent.Visibility =
                Visibility.Visible;
        }

        /// <summary>
        /// Выход из вк
        /// в главное меню.
        /// </summary>
        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            LogoutButton.IsEnabled = false;

            try
            {
                await _vkTokenStorage.DeleteAsync();

                VkStatusText.Text = "Не авторизован";
                LogoutButton.Visibility = Visibility.Collapsed;

                MainContent.Content = null;
                MainContent.Visibility = Visibility.Collapsed;

                HomeContent.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Не удалось выйти из аккаунта: {ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                LogoutButton.IsEnabled = true;
            }
        }
    }
}