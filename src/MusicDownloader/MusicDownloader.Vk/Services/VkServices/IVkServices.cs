using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VkNet.Model;

namespace MusicDownloader.Vk.Services.VkServices
{
   public interface IVkServices
   {
        Task<IReadOnlyCollection<Audio>> GetAllMusicAsync(CancellationToken cancellationToken = default);
   }
}
