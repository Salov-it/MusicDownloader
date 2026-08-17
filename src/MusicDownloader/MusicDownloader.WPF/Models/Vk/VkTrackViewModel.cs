using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VkNet.Model;

namespace MusicDownloader.WPF.Models.Vk
{
    public class VkTrackViewModel
    {
        public Audio Track { get; }

        public string Artist => string.IsNullOrWhiteSpace(Track.Artist) ? "Неизвестный исполнитель" : Track.Artist;

        public string Title => string.IsNullOrWhiteSpace(Track.Title) ? "Без названия" : Track.Title;

        public string Duration
        {
            get
            {
                var time = TimeSpan.FromSeconds(Track.Duration);

                return time.Hours > 0
                    ? time.ToString(@"h\:mm\:ss")
                    : time.ToString(@"mm\:ss");
            }
        }

        public VkTrackViewModel(Audio track)
        {
            Track = track;
        }
    }
}
