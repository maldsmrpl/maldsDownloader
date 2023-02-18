using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace malds_yt_downloader
{
    [Serializable]
    public class DownloadTask
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Author { get; set; }
        public string YouTubeUrl { get; set; }
        public string VideoUrl { get; set; }
        public string Thumb { get; set; }
        public string Duration { get; set; }
        public string Quality { get; set; }
        public long SizeByteTotal { get; set; }
        public long SizeByteCurrent { get; set; }
        public string SizeToDisplay { get; set; }
        public DownloadType Status { get; set; }
        public DateTime UploadDate { get; set;}
        public string UploadDateString { get; set; }
        public string Progress { get; set; }
        public int? DownloadQueue { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string Container { get; set; }

    }
}
