using MusicDownloader.Vk.Services.AuthorizationServices.Vk;
using MusicDownloader.Vk.Services.MusicDownloader;
using MusicDownloader.Vk.Services.MusicDownloader.Models;
using MusicDownloader.Vk.Services.VkServices;
using MusicDownloader.WPF.Models.Vk;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using VkNet.Model;
using Button = System.Windows.Controls.Button;
using Forms = System.Windows.Forms;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using Path = System.IO.Path;
using UserControl = System.Windows.Controls.UserControl;

namespace MusicDownloader.WPF.Views.VK
{
    /// <summary>
    /// Логика взаимодействия для VkMusicView.xaml
    /// </summary>
    public partial class VkMusicView : UserControl
    {
        private readonly VkServices _vkServices;
        private readonly IMusicDownloader _musicDownloader;

        private IReadOnlyCollection<Audio> _tracks =
            Array.Empty<Audio>();

        private string _outputDirectory;

        private CancellationTokenSource? _downloadCancellationTokenSource;


        public VkMusicView(
            VkServices vkServices,
            IMusicDownloader musicDownloader)
        {
            InitializeComponent();

            _vkServices = vkServices;
            _musicDownloader = musicDownloader;

            _outputDirectory =
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.MyMusic),
                    "MusicDownloader");

            OutputDirectoryTextBox.Text =
                _outputDirectory;

            Loaded += VkMusicView_Loaded;
        }


        // =========================================================
        // ЗАГРУЗКА МУЗЫКИ
        // =========================================================

        private async void VkMusicView_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            Loaded -= VkMusicView_Loaded;

            await LoadMusicAsync();
        }


        private async Task LoadMusicAsync()
        {
            try
            {
                LoadButton.IsEnabled = false;
                DownloadAllButton.IsEnabled = false;

                StatusText.Text =
                    "Загрузка музыки из VK...";

                EmptyState.Visibility =
                    Visibility.Collapsed;

                _tracks =
                    await _vkServices.GetAllMusicAsync();

                var viewModels =
                    _tracks
                        .Select(track =>
                            new VkTrackViewModel(track))
                        .ToList();

                TracksList.ItemsSource =
                    viewModels;

                TrackCountText.Text =
                    $"Треков: {_tracks.Count}";

                if (_tracks.Count == 0)
                {
                    StatusText.Text =
                        "Музыкальная библиотека пуста";

                    EmptyState.Visibility =
                        Visibility.Visible;

                    return;
                }

                StatusText.Text =
                    "Музыкальная библиотека загружена";

                DownloadAllButton.IsEnabled =
                    true;
            }
            catch (Exception ex)
            {
                StatusText.Text =
                    "Ошибка загрузки музыки";

                EmptyState.Visibility =
                    Visibility.Visible;

                MessageBox.Show(
                    $"Не удалось загрузить музыку VK:\n\n{ex.Message}",
                    "MusicDownloader",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                LoadButton.IsEnabled =
                    true;
            }
        }


        // =========================================================
        // ОБНОВИТЬ
        // =========================================================

        private async void LoadButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            await LoadMusicAsync();
        }


        // =========================================================
        // ВЫБОР ПАПКИ
        // =========================================================

        private void SelectDirectoryButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            using var dialog =
                new System.Windows.Forms.FolderBrowserDialog
                {
                    Description =
                        "Выберите папку для сохранения музыки",

                    UseDescriptionForTitle =
                        true,

                    SelectedPath =
                        Directory.Exists(_outputDirectory)
                            ? _outputDirectory
                            : Environment.GetFolderPath(
                                Environment.SpecialFolder.MyMusic)
                };

            var result =
                dialog.ShowDialog();

            if (result !=
                System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            _outputDirectory =
                dialog.SelectedPath;

            OutputDirectoryTextBox.Text =
                _outputDirectory;

            StatusText.Text =
                $"Папка сохранения: {_outputDirectory}";
        }


        // =========================================================
        // СКАЧАТЬ ВСЁ
        // =========================================================

        private async void DownloadAllButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_tracks.Count == 0)
            {
                MessageBox.Show(
                    "Нет треков для загрузки.",
                    "MusicDownloader",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            if (string.IsNullOrWhiteSpace(
                _outputDirectory))
            {
                MessageBox.Show(
                    "Не выбрана папка для сохранения.",
                    "MusicDownloader",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            try
            {
                LoadButton.IsEnabled = false;
                DownloadAllButton.IsEnabled = false;
                SelectDirectoryButton.IsEnabled = false;

                Directory.CreateDirectory(
                    _outputDirectory);

                _downloadCancellationTokenSource =
                    new CancellationTokenSource();

                StatusText.Text =
                    $"Загрузка {_tracks.Count} треков...";

                DownloadProgressBar.Value = 0;
                DownloadProgressBar.Visibility = Visibility.Visible;

                var progress = new Progress<MusicDownloadProgress>(
                    progressInfo =>
                    {
                        double percent =
                            progressInfo.Total > 0
                                ? (double)progressInfo.Current / progressInfo.Total * 100
                                : 0;

                        DownloadProgressBar.Value = percent;

                        StatusText.Text =
                            $"Загрузка {progressInfo.Current} из {progressInfo.Total} — " +
                            $"{progressInfo.Artist} — {progressInfo.Title}";
                    });

                MusicDownloadResult result =
                    await _musicDownloader.DownloadAsync(
                        _tracks,
                        _outputDirectory,
                        progress,
                        _downloadCancellationTokenSource.Token);

                StatusText.Text =
                    "Загрузка завершена";

            }
            catch (OperationCanceledException)
            {
                StatusText.Text =
                    "Загрузка отменена";
            }
            catch (Exception ex)
            {
                StatusText.Text =
                    "Ошибка загрузки";

                MessageBox.Show(
                    $"Ошибка загрузки музыки:\n\n{ex.Message}",
                    "MusicDownloader",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                _downloadCancellationTokenSource?.Dispose();

                _downloadCancellationTokenSource =
                    null;

                LoadButton.IsEnabled =
                    true;

                SelectDirectoryButton.IsEnabled =
                    true;

                DownloadAllButton.IsEnabled =
                    _tracks.Count > 0;
            }
        }


        // =========================================================
        // СКАЧАТЬ ОДИН ТРЕК
        // =========================================================

        private async void DownloadTrackButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            if (button.DataContext
                is not VkTrackViewModel viewModel)
            {
                return;
            }

            Audio track =
                viewModel.Track;

            if (string.IsNullOrWhiteSpace(
                _outputDirectory))
            {
                MessageBox.Show(
                    "Сначала выберите папку для сохранения музыки.",
                    "MusicDownloader",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            try
            {
                button.IsEnabled = false;

                StatusText.Text =
                    $"Загрузка: {viewModel.Artist} — {viewModel.Title}";

                MusicDownloadResult result =
                    await _musicDownloader.DownloadAsync(
                        new[] { track },
                        _outputDirectory);

                if (result.Downloaded > 0)
                {
                    StatusText.Text =
                        "Трек загружен";
                }
                else if (result.Skipped > 0)
                {
                    StatusText.Text =
                        "Трек уже существует";

                    MessageBox.Show(
                        "Этот трек уже существует в выбранной папке.",
                        "MusicDownloader",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    StatusText.Text =
                        "Ошибка загрузки";

                    MessageBox.Show(
                        "Не удалось загрузить трек.",
                        "MusicDownloader",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (OperationCanceledException)
            {
                StatusText.Text =
                    "Загрузка отменена";
            }
            catch (Exception ex)
            {
                StatusText.Text =
                    "Ошибка загрузки";

                MessageBox.Show(
                    $"Не удалось скачать трек:\n\n{ex.Message}",
                    "MusicDownloader",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                button.IsEnabled =
                    true;
            }
        }


        // =========================================================
        // ГЛАВНОЕ МЕНЮ
        // =========================================================

        private void MainMenuButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (Window.GetWindow(this)
                is MainWindow mainWindow)
            {
                mainWindow.ShowMainMenu();
            }
        }

    }
}
