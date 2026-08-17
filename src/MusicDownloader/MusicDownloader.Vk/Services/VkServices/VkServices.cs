using MusicDownloader.Vk.Services.AuthorizationServices.Vk;
using MusicDownloader.Vk.Services.AuthorizationServices.Vk.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VkNet;
using VkNet.Model;

namespace MusicDownloader.Vk.Services.VkServices
{
    public sealed class VkServices : IVkServices
    {
        private readonly VkApi _vkApi;

        private VkServices(VkApi vkApi)
        {
            _vkApi = vkApi;
        }

        public static async Task<VkServices> CreateAsync(VkTokenStorage tokenStorage, CancellationToken cancellationToken = default)
        {
            VkToken? token = await tokenStorage.LoadAsync(
                cancellationToken);

            if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
            {
                throw new InvalidOperationException(
                    "VK токен не найден. Необходимо авторизоваться.");
            }

            var vkApi = new VkApi();

            vkApi.Authorize(new ApiAuthParams
            {
                AccessToken = token.AccessToken
            });

            return new VkServices(vkApi);
        }


        public async Task<IReadOnlyCollection<Audio>> GetAllMusicAsync(CancellationToken cancellationToken = default)
        {
            var user = _vkApi.Users.Get(new long[] { }).First();

            var audio = _vkApi.Audio.Get(new AudioGetParams
            {
                OwnerId = user.Id,
                Count = 1000,
                Offset = 0
            });



            return audio;
        }
    }
}
