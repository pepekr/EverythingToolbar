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
            FileCount >= 1_000_000 ? $"{FileCount / 1_000_000.0:F1}M"
            : FileCount >= 1_000   ? $"{FileCount / 1_000.0:F1}K"
            : FileCount.ToString();

        public string FormattedPercentage => $"{Percentage:F1}%";
    }

    public class LargeFileInfo
    {
        public string FullPath { get; set; } = "";
        public string FileName { get; set; } = "";
        public long SizeBytes { get; set; }

        public string FormattedSize
        {
            get
            {
                if (SizeBytes >= 1_099_511_627_776L) return $"{SizeBytes / 1_099_511_627_776.0:F2} TB";
                if (SizeBytes >= 1_073_741_824L)     return $"{SizeBytes / 1_073_741_824.0:F2} GB";
                if (SizeBytes >= 1_048_576L)         return $"{SizeBytes / 1_048_576.0:F1} MB";
                return $"{SizeBytes / 1024.0:F1} KB";
            }
        }

        // Truncated path for display
        public string ShortPath
        {
            get
            {
                var dir = System.IO.Path.GetDirectoryName(FullPath) ?? "";
                return dir.Length > 55 ? "..." + dir[^52..] : dir;
            }
        }
    }

    public class ExtensionStat
    {
        public string Extension { get; set; } = "";
        public long Count { get; set; }
        public double Percentage { get; set; }
        public string HexColor { get; set; } = "#0078D4";

        public SolidColorBrush Brush =>
            new((Color)ColorConverter.ConvertFromString(HexColor));

        public string FormattedCount =>
            Count >= 1_000_000 ? $"{Count / 1_000_000.0:F1}M"
            : Count >= 1_000   ? $"{Count / 1_000.0:F1}K"
            : Count.ToString();

        public string FormattedPercentage => $"{Percentage:F1}%";
        public string DisplayName => $".{Extension}";
    }

    public class DriveStats
    {
        public string Name { get; set; } = "";
        public long TotalBytes { get; set; }
        public long UsedBytes { get; set; }
        public double UsedPercent => TotalBytes > 0 ? (double)UsedBytes / TotalBytes * 100 : 0;
        public string FormattedTotal => FormatBytes(TotalBytes);
        public string FormattedUsed  => FormatBytes(UsedBytes);
        public string FormattedFree  => FormatBytes(TotalBytes - UsedBytes);

        private static string FormatBytes(long b)
        {
            if (b >= 1_099_511_627_776L) return $"{b / 1_099_511_627_776.0:F1} TB";
            if (b >= 1_073_741_824L)     return $"{b / 1_073_741_824.0:F1} GB";
            if (b >= 1_048_576L)         return $"{b / 1_048_576.0:F1} MB";
            return $"{b / 1024.0:F1} KB";
        }
    }

    public class StatisticsData
    {
        public List<FileTypeCategory> Categories     { get; set; } = [];
        public long TotalIndexedFiles                { get; set; }
        public long TotalIndexedFolders              { get; set; }
        public List<DriveStats> Drives               { get; set; } = [];

        // ── New ──────────────────────────────────────────────────────────────
        public List<LargeFileInfo>  LargestFiles     { get; set; } = [];
        public List<ExtensionStat>  TopExtensions    { get; set; } = [];
        public long ModifiedToday                    { get; set; }
        public long ModifiedThisWeek                 { get; set; }
        public long ModifiedThisMonth                { get; set; }
    }
}
