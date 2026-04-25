using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace EverythingToolbar.Statistics
{
    public class FileTypeCategory
    {
        public string Name { get; set; } = "";
        public string[] Extensions { get; set; } = [];
        public string HexColor { get; set; } = "#767676";
        public long FileCount { get; set; }
        public double Percentage { get; set; }

        public SolidColorBrush Brush =>
            new((Color)ColorConverter.ConvertFromString(HexColor));

        public string FormattedCount =>
            FileCount >= 1_000_000
                ? $"{FileCount / 1_000_000.0:F1}M"
                : FileCount >= 1_000
                    ? $"{FileCount / 1_000.0:F1}K"
                    : FileCount.ToString();

        public string FormattedPercentage => $"{Percentage:F1}%";
    }

    public class DriveStats
    {
        public string Name { get; set; } = "";
        public long TotalBytes { get; set; }
        public long UsedBytes { get; set; }
        public double UsedPercent => TotalBytes > 0 ? (double)UsedBytes / TotalBytes * 100 : 0;
        public string FormattedTotal => FormatBytes(TotalBytes);
        public string FormattedUsed => FormatBytes(UsedBytes);
        public string FormattedFree => FormatBytes(TotalBytes - UsedBytes);

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1_099_511_627_776L) return $"{bytes / 1_099_511_627_776.0:F1} TB";
            if (bytes >= 1_073_741_824L) return $"{bytes / 1_073_741_824.0:F1} GB";
            if (bytes >= 1_048_576L) return $"{bytes / 1_048_576.0:F1} MB";
            return $"{bytes / 1024.0:F1} KB";
        }
    }

    public class StatisticsData
    {
        public List<FileTypeCategory> Categories { get; set; } = [];
        public long TotalIndexedFiles { get; set; }
        public long TotalIndexedFolders { get; set; }
        public List<DriveStats> Drives { get; set; } = [];
    }
}
