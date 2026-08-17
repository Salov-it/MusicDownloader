using MusicDownloader.Vk.Services.MusicDownloader.Models;
using VkNet.Model;

namespace MusicDownloader.Vk.Services.MusicDownloader
{
    public interface IMusicDownloader
    {
        Task<MusicDownloadResult> DownloadAsync(IReadOnlyCollection<Audio> tracks,string outputDirectory,IProgress<MusicDownloadProgress>? progress = null,CancellationToken cancellationToken = default);
    }
}
