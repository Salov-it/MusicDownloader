using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusicDownloader.Vk.Services.MusicDownloader.Models
{
    public sealed record MusicDownloadResult(
     int Downloaded,
     int Skipped,
     int Errors);
}
