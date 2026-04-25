using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace EverythingToolbar.Statistics
{
    public static class DiskUsageAnalyzer
    {
        // ── Everything SDK P/Invoke ──────────────────────────────────────────
        [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
        private static extern uint Everything_SetSearchW(string lpSearchString);

        [DllImport("Everything64.dll")]
        private static extern void Everything_SetRequestFlags(uint dwRequestFlags);

        [DllImport("Everything64.dll")]
        private static extern void Everything_SetMax(uint dwMax);

        [DllImport("Everything64.dll")]
        private static extern bool Everything_QueryW(bool bWait);

        [DllImport("Everything64.dll")]
        private static extern uint Everything_GetTotResults();

        [DllImport("Everything64.dll")]
        private static extern uint Everything_GetLastError();

        private const uint RequestFileName  = 0x00000001;
        private const uint EverythingErrorIpc = 2;

        // ── File-type category definitions ───────────────────────────────────
        private static List<FileTypeCategory> BuildCategories() =>
        [
            new FileTypeCategory
            {
                Name = "Images",
                Extensions = ["jpg", "jpeg", "png", "gif", "bmp", "webp", "tiff", "svg", "ico", "heic", "raw"],
                HexColor = "#0078D4"
            },
            new FileTypeCategory
            {
                Name = "Video",
                Extensions = ["mp4", "avi", "mkv", "mov", "wmv", "flv", "m4v", "webm", "mpg", "mpeg", "3gp"],
                HexColor = "#C50F1F"
            },
            new FileTypeCategory
            {
                Name = "Audio",
                Extensions = ["mp3", "flac", "wav", "aac", "ogg", "wma", "m4a", "opus", "aiff"],
                HexColor = "#498205"
            },
            new FileTypeCategory
            {
                Name = "Documents",
                Extensions = ["pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx", "txt", "rtf", "odt", "ods", "odp", "csv", "md"],
                HexColor = "#8764B8"
            },
            new FileTypeCategory
            {
                Name = "Archives",
                Extensions = ["zip", "rar", "7z", "tar", "gz", "bz2", "xz", "iso", "cab", "dmg"],
                HexColor = "#C19C00"
            },
            new FileTypeCategory
            {
                Name = "Code",
                Extensions = ["cs", "py", "js", "ts", "html", "css", "java", "cpp", "c", "h", "php", "rb", "go", "rs", "swift", "kt", "json", "xml", "yaml", "toml"],
                HexColor = "#00B7C3"
            },
        ];

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Runs analysis on a background thread.
        /// </summary>
        /// <param name="pathFilter">Optional folder path, e.g. "C:\Projects". Empty = all drives.</param>
        /// <param name="minSizeBytes">Only count files >= this size. 0 = no filter.</param>
        /// <param name="progress">Optional progress reporter.</param>
        public static Task<StatisticsData?> AnalyzeAsync(
            string? pathFilter = null,
            long minSizeBytes = 0,
            IProgress<string>? progress = null) =>
            Task.Run(() => Analyze(pathFilter?.Trim(), minSizeBytes, progress));

        // ── Internal logic ────────────────────────────────────────────────────
        private static StatisticsData? Analyze(string? pathFilter, long minSizeBytes, IProgress<string>? progress)
        {
            // Connectivity check
            progress?.Report("Connecting to Everything...");
            Everything_SetSearchW("");
            Everything_SetMax(1);
            Everything_SetRequestFlags(RequestFileName);
            if (!Everything_QueryW(true) || Everything_GetLastError() == EverythingErrorIpc)
                return null;

            // Build optional prefix modifiers
            // path: restricts search to a specific folder tree
            // size:>=N restricts to files of minimum size
            string pathPrefix = string.IsNullOrEmpty(pathFilter)
                ? ""
                : $"path:\"{pathFilter}\" ";

            string sizePrefix = minSizeBytes > 0
                ? $"size:>={minSizeBytes} "
                : "";

            var data = new StatisticsData();

            // Total file / folder counts
            progress?.Report("Counting indexed files...");
            data.TotalIndexedFiles   = QueryCount($"{pathPrefix}{sizePrefix}!folder ");
            data.TotalIndexedFolders = QueryCount($"{pathPrefix}folder:");

            // Per-category counts
            var categories = BuildCategories();
            long categorised = 0;

            foreach (var cat in categories)
            {
                progress?.Report($"Analysing {cat.Name}...");
                string extFilter = "ext:" + string.Join("|", cat.Extensions);
                cat.FileCount = QueryCount($"{pathPrefix}{sizePrefix}{extFilter}");
                categorised += cat.FileCount;
            }

            // "Other" bucket
            long otherCount = Math.Max(0, data.TotalIndexedFiles - categorised);
            categories.Add(new FileTypeCategory
            {
                Name = "Other",
                Extensions = [],
                HexColor = "#69797E",
                FileCount = otherCount
            });

            // Percentages
            long total = data.TotalIndexedFiles > 0 ? data.TotalIndexedFiles : 1;
            foreach (var cat in categories)
                cat.Percentage = (double)cat.FileCount / total * 100.0;

            // Sort: descending by count, "Other" always last
            data.Categories =
            [
                .. categories.Where(c => c.Name != "Other").OrderByDescending(c => c.FileCount),
                categories.First(c => c.Name == "Other")
            ];

            // Drive statistics (independent of Everything)
            progress?.Report("Reading drive information...");
            data.Drives = GetDriveStats();

            return data;
        }

        private static long QueryCount(string query)
        {
            Everything_SetSearchW(query);
            Everything_SetMax(1);
            Everything_SetRequestFlags(RequestFileName);
            return Everything_QueryW(true) ? (long)Everything_GetTotResults() : 0;
        }

        private static List<DriveStats> GetDriveStats()
        {
            var result = new List<DriveStats>();
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (!drive.IsReady) continue;
                    if (drive.DriveType is DriveType.CDRom or DriveType.Unknown) continue;

                    result.Add(new DriveStats
                    {
                        Name       = drive.Name.TrimEnd('\\'),
                        TotalBytes = drive.TotalSize,
                        UsedBytes  = drive.TotalSize - drive.AvailableFreeSpace
                    });
                }
                catch { /* skip inaccessible drives */ }
            }
            return result;
        }
    }
}
