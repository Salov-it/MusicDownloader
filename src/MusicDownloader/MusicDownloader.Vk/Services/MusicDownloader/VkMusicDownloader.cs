using Microsoft.Extensions.Logging;
using MusicDownloader.Vk.Services.MusicDownloader.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VkNet.Model;

namespace MusicDownloader.Vk.Services.MusicDownloader
{
    public class VkMusicDownloader : IMusicDownloader
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<VkMusicDownloader> _logger;

        private const int MaxConcurrentDownloads = 10;

        public VkMusicDownloader(
            HttpClient httpClient,
            ILogger<VkMusicDownloader> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            _httpClient.Timeout = TimeSpan.FromMinutes(5);
        }

        public async Task<MusicDownloadResult> DownloadAsync(
     IReadOnlyCollection<Audio> tracks,
     string outputDirectory,
     IProgress<MusicDownloadProgress>? progress = null,
     CancellationToken cancellationToken = default)
        {
            var downloaded = 0;
            var skipped = 0;
            var errors = 0;

            var stopwatch = Stopwatch.StartNew();

            using var semaphore = new SemaphoreSlim(MaxConcurrentDownloads);

            // Защита от одновременной записи в один файл.
            var fileLocks = new ConcurrentDictionary<string, SemaphoreSlim>(
                StringComparer.OrdinalIgnoreCase);

            _logger.LogInformation(
                "Начало загрузки музыки. Треков: {TrackCount}. Параллельность: {Concurrency}. Директория: {OutputDirectory}",
                tracks.Count,
                MaxConcurrentDownloads,
                outputDirectory);

            try
            {
                Directory.CreateDirectory(outputDirectory);

                var tasks = tracks.Select(async (track, index) =>
                {
                    var current = index + 1;

                    await semaphore.WaitAsync(cancellationToken);

                    try
                    {
                        if (track == null)
                        {
                            Interlocked.Increment(ref errors);

                            _logger.LogWarning(
                                "Пустой объект трека. Индекс: {Index}",
                                current);

                            return;
                        }

                        var artist = string.IsNullOrWhiteSpace(track.Artist)
                            ? "Unknown Artist"
                            : track.Artist.Trim();

                        var title = string.IsNullOrWhiteSpace(track.Title)
                            ? $"Unknown Track {current}"
                            : track.Title.Trim();

                        var url = track.Url?.ToString();

                        if (string.IsNullOrWhiteSpace(url))
                        {
                            Interlocked.Increment(ref skipped);

                            _logger.LogWarning(
                                "Трек пропущен из-за отсутствия URL: {Artist} - {Title}",
                                artist,
                                title);

                            return;
                        }

                        var fileName = CreateFileName(artist, title);

                        var filePath = Path.Combine(
                            outputDirectory,
                            fileName);

                        // Для одинаковых названий файлов загрузка будет последовательной.
                        var fileLock = fileLocks.GetOrAdd(
                            filePath,
                            _ => new SemaphoreSlim(1, 1));

                        await fileLock.WaitAsync(cancellationToken);

                        try
                        {
                            if (File.Exists(filePath))
                            {
                                Interlocked.Increment(ref skipped);

                                _logger.LogDebug(
                                    "Трек пропущен, файл уже существует: {FilePath}",
                                    filePath);

                                return;
                            }

                            // У каждой загрузки собственный временный файл.
                            var tempFilePath = Path.Combine(
                                outputDirectory,
                                $".{Guid.NewGuid():N}.part");

                            var success = await DownloadTrackAsync(
                                url,
                                filePath,
                                tempFilePath,
                                artist,
                                title,
                                current,
                                tracks.Count,
                                progress,
                                cancellationToken);

                            if (success)
                            {
                                Interlocked.Increment(ref downloaded);
                            }
                            else
                            {
                                Interlocked.Increment(ref errors);
                            }
                        }
                        finally
                        {
                            fileLock.Release();
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }).ToArray();

                await Task.WhenAll(tasks);

                stopwatch.Stop();

                var result = new MusicDownloadResult(
                    downloaded,
                    skipped,
                    errors);

                _logger.LogInformation(
                    "Загрузка завершена за {Elapsed}. Загружено: {Downloaded}, пропущено: {Skipped}, ошибок: {Errors}, всего: {Total}",
                    stopwatch.Elapsed,
                    downloaded,
                    skipped,
                    errors,
                    tracks.Count);

                return result;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "Загрузка отменена. Загружено: {Downloaded}, пропущено: {Skipped}, ошибок: {Errors}",
                    downloaded,
                    skipped,
                    errors);

                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Критическая ошибка загрузки музыки");

                throw;
            }
            finally
            {
                foreach (var fileLock in fileLocks.Values)
                {
                    fileLock.Dispose();
                }
            }
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
            Exception? lastException = null;

            try
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

                        var totalBytes = response.Content.Headers.ContentLength;

                        await using var sourceStream =
                            await response.Content.ReadAsStreamAsync(
                                cancellationToken);

                        await using var fileStream = new FileStream(
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

                        await fileStream.FlushAsync(cancellationToken);

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

                        _logger.LogInformation(
                            "Трек успешно скачан [{Current}/{Total}]: {Artist} - {Title}. Размер: {Bytes} байт",
                            current,
                            total,
                            artist,
                            title,
                            bytesDownloaded);

                        return true;
                    }
                    catch (OperationCanceledException ex)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            throw;

                        lastException = ex;

                        _logger.LogWarning(
                            ex,
                            "Таймаут при скачивании трека {Artist} - {Title}. Попытка {Attempt}/3",
                            artist,
                            title,
                            attempt);
                    }
                    catch (HttpRequestException ex)
                    {
                        lastException = ex;

                        _logger.LogWarning(
                            ex,
                            "HTTP-ошибка при скачивании {Artist} - {Title}. Попытка {Attempt}/3. Код ответа: {StatusCode}",
                            artist,
                            title,
                            attempt,
                            ex.StatusCode);
                    }
                    catch (IOException ex)
                    {
                        lastException = ex;

                        _logger.LogWarning(
                            ex,
                            "Ошибка файловой системы при скачивании {Artist} - {Title}. Попытка {Attempt}/3",
                            artist,
                            title,
                            attempt);
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;

                        _logger.LogWarning(
                            ex,
                            "Непредвиденная ошибка при скачивании {Artist} - {Title}. Попытка {Attempt}/3",
                            artist,
                            title,
                            attempt);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                DeleteTempFile(tempFilePath);

                _logger.LogInformation(
                    "Скачивание трека отменено: {Artist} - {Title}",
                    artist,
                    title);

                throw;
            }

            DeleteTempFile(tempFilePath);

            _logger.LogError(
                lastException,
                "Не удалось скачать трек после 3 попыток [{Current}/{Total}]: {Artist} - {Title}",
                current,
                total,
                artist,
                title);

            return false;
        }

        private static string CreateFileName(
            string artist,
            string title)
        {
            var fileName = $"{artist} - {title}.mp3";

            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(invalidChar, '_');
            }

            fileName = fileName.Trim();

            if (fileName.Length > 200)
            {
                fileName = fileName[..196] + ".mp3";
            }

            return fileName;
        }

        private void DeleteTempFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Не удалось удалить временный файл: {FilePath}",
                    path);
            }
        }
    }
}