using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusicDownloader.Vk.Services.AuthorizationServices.Vk.Models
{
    public sealed class VkToken
    {
        public string AccessToken { get; set; } = string.Empty;

        public DateTime SavedAtUtc { get; set; }
    }
}
