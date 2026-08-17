using Microsoft.Web.WebView2.Core;
using MusicDownloader.Vk.Services.AuthorizationServices.Vk;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MessageBox = System.Windows.MessageBox;
using Path = System.IO.Path;

namespace MusicDownloader.WPF.Views.VK
{
    /// <summary>
    /// Логика взаимодействия для VkLoginWindow.xaml
    /// </summary>
    public partial class VkLoginWindow : Window
    {
        private const string AuthorizationUrl =
            "https://oauth.vk.com/authorize" +
            "?client_id=6463690" +
            "&scope=1073737727" +
            "&redirect_uri=https://oauth.vk.com/blank.html" +
            "&display=page" +
            "&response_type=token" +
            "&revoke=1";

        private readonly VkTokenStorage _tokenStorage;

        public VkLoginWindow()
        {
            InitializeComponent();

            _tokenStorage = new VkTokenStorage();
            Loaded += VkLoginWindow_Loaded;
        }

        private async void VkLoginWindow_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                await WebView.EnsureCoreWebView2Async();

                WebView.NavigationCompleted +=
                    WebView_NavigationCompleted;

                WebView.CoreWebView2.Navigate(
                    AuthorizationUrl);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Не удалось запустить WebView2:\n\n{ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                DialogResult = false;
                Close();
            }
        }

        private async void WebView_NavigationCompleted(
            object? sender,
            CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess)
                return;

            try
            {
                string jsonUrl =
                    await WebView.CoreWebView2.ExecuteScriptAsync(
                        "window.location.href");

                string? currentUrl =
                    JsonSerializer.Deserialize<string>(jsonUrl);

                if (string.IsNullOrWhiteSpace(currentUrl))
                    return;


                bool isVkRedirect =
                    currentUrl.StartsWith(
                        "https://oauth.vk.ru/blank.html",
                        StringComparison.OrdinalIgnoreCase)
                    ||
                    currentUrl.StartsWith(
                        "https://oauth.vk.com/blank.html",
                        StringComparison.OrdinalIgnoreCase);

                if (!isVkRedirect)
                    return;


                string? accessToken =
                    GetFragmentParameter(
                        currentUrl,
                        "access_token");

                if (string.IsNullOrWhiteSpace(accessToken))
                    return;


                await _tokenStorage.SaveAsync(accessToken);


                DialogResult = true;

                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка обработки VK OAuth:\n\n{ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                DialogResult = false;
                Close();
            }
        }


        private static string? GetFragmentParameter(
            string url,
            string parameterName)
        {
            if (!Uri.TryCreate(
                    url,
                    UriKind.Absolute,
                    out Uri? uri))
            {
                return null;
            }


            string fragment = uri.Fragment;

            if (string.IsNullOrWhiteSpace(fragment))
                return null;


            fragment = fragment.TrimStart('#');


            string[] parameters =
                fragment.Split(
                    '&',
                    StringSplitOptions.RemoveEmptyEntries);


            foreach (string parameter in parameters)
            {
                string[] parts =
                    parameter.Split(
                        '=',
                        2,
                        StringSplitOptions.None);


                if (parts.Length != 2)
                    continue;


                string key =
                    Uri.UnescapeDataString(parts[0]);

                string value =
                    Uri.UnescapeDataString(parts[1]);


                if (string.Equals(
                        key,
                        parameterName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return value;
                }
            }


            return null;
        }
    }
}
