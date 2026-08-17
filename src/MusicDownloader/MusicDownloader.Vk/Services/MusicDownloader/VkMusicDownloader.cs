using MusicDownloader.Vk.Services.MusicDownloader.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VkNet.Model;

namespace MusicDownloader.Vk.Services.MusicDownloader
{
    public class VkMusicDownloader : IMusicDownloader
    {
        private readonly HttpClient _httpClient;
        private readonly Random _random = new();

        public VkMusicDownloader(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromMinutes(5);
        }

        public async Task<MusicDownloadResult> DownloadAsync(
            IReadOnlyCollection<Audio> tracks,
            string outputDirectory,
            IProgress<MusicDownloadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(outputDirectory);

            var downloaded = 0;
            var skipped = 0;
            var errors = 0;

            var index = 0;

            foreach (var track in tracks)
            {
                cancellationToken.ThrowIfCancellationRequested();

                index++;

                if (track == null)
                {
                    errors++;
                    continue;
                }

                var artist = string.IsNullOrWhiteSpace(track.Artist)
                    ? "Unknown Artist"
                    : track.Artist.Trim();

                var title = string.IsNullOrWhiteSpace(track.Title)
                    ? $"Unknown Track {index}"
                    : track.Title.Trim();

                if (string.IsNullOrWhiteSpace(track.Url.ToString()))
                {
                    skipped++;
                    continue;
                }

                var fileName = CreateFileName(
                    artist,
                    title);

                var filePath = Path.Combine(
                    outputDirectory,
                    fileName);

                var tempFilePath = filePath + ".part";

                if (File.Exists(filePath))
                {
                    skipped++;
                    continue;
                }

                DeleteTempFile(tempFilePath);

                var success = await DownloadTrackAsync(
                    track.Url.ToString(),
                    filePath,
                    tempFilePath,
                    artist,
                    title,
                    index,
                    tracks.Count,
                    progress,
                    cancellationToken);

                if (success)
                    downloaded++;
                else
                    errors++;

                if (success &&
                    index < tracks.Count &&
                    !cancellationToken.IsCancellationRequested)
                {
                    var delay = _random.Next(2000, 4000);

                    await Task.Delay(
                        delay,
                        cancellationToken);
                }
            }

            return new MusicDownloadResult(
                downloaded,
                skipped,
                errors);
        }

        private async Task<bool> DownloadTrackAsync(
            string url,
            string filePath,
            string tempFilePath,
            string artist,
            string title,
            int current,
            int total,
            IProgress<MusicDownloadProgress>? progress,
            CancellationToken cancellationToken)
        {
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    using var response = await _httpClient.GetAsync(
                        url,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken);

                    response.EnsureSuccessStatusCode();

                    var totalBytes =
                        response.Content.Headers.ContentLength;

                    await using var sourceStream =
                        await response.Content.ReadAsStreamAsync(
                            cancellationToken);

                    await using var fileStream =
                        new FileStream(
                            tempFilePath,
                            FileMode.Create,
                            FileAccess.Write,
                            FileShare.None,
                            bufferSize: 81920,
                            useAsync: true);

                    var buffer = new byte[81920];

                    long bytesDownloaded = 0;

                    int bytesRead;

                    while ((bytesRead = await sourceStream.ReadAsync(
                               buffer,
                               cancellationToken)) > 0)
                    {
                        await fileStream.WriteAsync(
                            buffer.AsMemory(0, bytesRead),
                            cancellationToken);

                        bytesDownloaded += bytesRead;

                        progress?.Report(
                            new MusicDownloadProgress(
                                current,
                                total,
                                artist,
                                title,
                                bytesDownloaded,
                                totalBytes));
                    }

                    await fileStream.FlushAsync(
                        cancellationToken);

                    fileStream.Close();

                    File.Move(
                        tempFilePath,
                        filePath,
                        overwrite: false);

                    progress?.Report(
                        new MusicDownloadProgress(
                            current,
                            total,
                            artist,
                            title,
                            totalBytes ?? bytesDownloaded,
                            totalBytes ?? bytesDownloaded));

                    return true;
                }
                catch (OperationCanceledException)
                {
                    if (cancellationToken.IsCancellationRequested)
                        throw;
                }
                catch (HttpRequestException)
                {
                    // retry
                }
                catch (IOException)
                {
                    // retry
                }
                catch (Exception)
                {
                    // retry
                }

                if (attempt < 3)
                {
                    var delay = TimeSpan.FromSeconds(
                        Math.Pow(2, attempt) +
                        _random.NextDouble());

                    await Task.Delay(
                        delay,
                        cancellationToken);
                }
            }

            DeleteTempFile(tempFilePath);

            return false;
        }

        private static string CreateFileName(
            string artist,
            string title)
        {
            var fileName =
                $"{artist} - {title}.mp3";

            foreach (var invalidChar in
                     Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(
                    invalidChar,
                    '_');
            }

            fileName = fileName.Trim();

            if (fileName.Length > 200)
            {
                fileName = fileName[..200] + ".mp3";
            }

            return fileName;
        }

        private static void DeleteTempFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Ничего не делаем.
            }
        }
    }
}