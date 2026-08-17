using MusicDownloader.Vk.Services.AuthorizationServices.Vk.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MusicDownloader.Vk.Services.AuthorizationServices.Vk
{
    public sealed class VkTokenStorage
    {
        private const string FileName = "vk-token.json";

        private readonly string _filePath;

        public VkTokenStorage()
        {
            _filePath = Path.Combine(
                AppContext.BaseDirectory,
                FileName);
        }

        public async Task<VkToken?> LoadAsync(
            CancellationToken cancellationToken = default)
        {
            if (!File.Exists(_filePath))
                return null;

            try
            {
                string json = await File.ReadAllTextAsync(
                    _filePath,
                    cancellationToken);

                return JsonSerializer.Deserialize<VkToken>(json);
            }
            catch (JsonException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
        }

        public bool Exists()
        {
            return File.Exists(_filePath);
        }

        public async Task SaveAsync(string accessToken,CancellationToken cancellationToken = default)
        {
            var token = new VkToken
            {
                AccessToken = accessToken,
                SavedAtUtc = DateTime.UtcNow
            };

            string json = JsonSerializer.Serialize(
                token,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

            await File.WriteAllTextAsync(
                _filePath,
                json,
                cancellationToken);
        }

        public Task DeleteAsync()
        {
            if (File.Exists(_filePath))
                File.Delete(_filePath);

            return Task.CompletedTask;
        }
    }

}
