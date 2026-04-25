using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
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
        private static extern void Everything_SetSort(uint dwSortType);

        [DllImport("Everything64.dll")]
        private static extern bool Everything_QueryW(bool bWait);

        [DllImport("Everything64.dll")]
        private static extern uint Everything_GetTotResults();

        [DllImport("Everything64.dll")]
        private static extern uint Everything_GetNumResults();

        [DllImport("Everything64.dll")]
        private static extern uint Everything_GetLastError();

        [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
        private static extern void Everything_GetResultFullPathNameW(uint nIndex, StringBuilder lpString, uint nMaxCount);

        [DllImport("Everything64.dll")]
        private static extern bool Everything_GetResultSize(uint nIndex, out long lpFileSize);

        private const uint RequestFileName     = 0x00000001;
        private const uint RequestFullPath     = 0x00000004;
        private const uint RequestSize         = 0x00000010;
        private const uint SortSizeDescending  = 6;   // EVERYTHING_SORT_SIZE_DESCENDING
        private const uint EverythingErrorIpc  = 2;

        // ── Colour palette for top extensions ───────────────────────────────
        private static readonly string[] Palette =
        [
            "#0078D4","#C50F1F","#498205","#8764B8",
            "#C19C00","#00B7C3","#E74856","#744DA9",
            "#018574","#FF8C00"
        ];

        // ── Common extensions to probe for "Top Extensions" ─────────────────
        private static readonly string[] CommonExtensions =
        [
            "exe","dll","sys","bat","cmd",
            "jpg","jpeg","png","gif","bmp","webp","svg","ico",
            "mp4","avi","mkv","mov","wmv","flv",
            "mp3","flac","wav","aac","ogg","wma",
            "pdf","doc","docx","xls","xlsx","ppt","pptx","txt","rtf","md","csv",
            "zip","rar","7z","tar","gz","iso",
            "cs","py","js","ts","jsx","tsx","html","css","java","cpp","c","h",
            "json","xml","yaml","toml","sql","sh","rb","go","rs","kt","swift","php","log"
        ];

        // ── File-type category definitions ───────────────────────────────────
        private static List<FileTypeCategory> BuildCategories() =>
        [
            new() { Name="Images",    HexColor="#0078D4", Extensions=["jpg","jpeg","png","gif","bmp","webp","tiff","svg","ico","heic","raw"] },
            new() { Name="Video",     HexColor="#C50F1F", Extensions=["mp4","avi","mkv","mov","wmv","flv","m4v","webm","mpg","mpeg","3gp"] },
            new() { Name="Audio",     HexColor="#498205", Extensions=["mp3","flac","wav","aac","ogg","wma","m4a","opus","aiff"] },
            new() { Name="Documents", HexColor="#8764B8", Extensions=["pdf","doc","docx","xls","xlsx","ppt","pptx","txt","rtf","odt","ods","odp","csv","md"] },
            new() { Name="Archives",  HexColor="#C19C00", Extensions=["zip","rar","7z","tar","gz","bz2","xz","iso","cab","dmg"] },
            new() { Name="Code",      HexColor="#00B7C3", Extensions=["cs","py","js","ts","html","css","java","cpp","c","h","php","rb","go","rs","swift","kt","json","xml","yaml","toml"] },
        ];

        // ── Public API ────────────────────────────────────────────────────────
        public static Task<StatisticsData?> AnalyzeAsync(
            string? pathFilter  = null,
            long    minSizeBytes = 0,
            IProgress<string>? progress = null) =>
            Task.Run(() => Analyze(pathFilter?.Trim(), minSizeBytes, progress));

        // ── Core analysis ─────────────────────────────────────────────────────
        private static StatisticsData? Analyze(string? pathFilter, long minSizeBytes, IProgress<string>? progress)
        {
            progress?.Report("Connecting to Everything...");
            Everything_SetSearchW("");
            Everything_SetMax(1);
            Everything_SetRequestFlags(RequestFileName);
            if (!Everything_QueryW(true) || Everything_GetLastError() == EverythingErrorIpc)
                return null;

            string pathPfx = string.IsNullOrEmpty(pathFilter) ? "" : $"path:\"{pathFilter}\" ";
            string sizePfx = minSizeBytes > 0 ? $"size:>={minSizeBytes} " : "";

            var data = new StatisticsData();

            // Totals  ("file:" = files only, excludes folders)
            progress?.Report("Counting indexed files...");
            data.TotalIndexedFiles   = QueryCount($"{pathPfx}{sizePfx}file:");
            data.TotalIndexedFolders = QueryCount($"{pathPfx}folder:");

            // File type categories
            var categories = BuildCategories();
            long categorised = 0;
            foreach (var cat in categories)
            {
                progress?.Report($"Analysing {cat.Name}...");
                cat.FileCount  = QueryCount($"{pathPfx}{sizePfx}file: ext:{string.Join(";", cat.Extensions)}");
                categorised   += cat.FileCount;
            }

            long otherCount = Math.Max(0, data.TotalIndexedFiles - categorised);
            categories.Add(new FileTypeCategory { Name="Other", HexColor="#69797E", Extensions=[], FileCount=otherCount });

            long total = data.TotalIndexedFiles > 0 ? data.TotalIndexedFiles : 1;
            foreach (var cat in categories)
                cat.Percentage = (double)cat.FileCount / total * 100.0;

            data.Categories =
            [
                .. categories.Where(c => c.Name != "Other").OrderByDescending(c => c.FileCount),
                categories.First(c => c.Name == "Other")
            ];

            // Recently modified (ignores size filter — show full activity)
            progress?.Report("Counting recent activity...");
            data.ModifiedToday     = QueryCount($"{pathPfx}dm:today file:");
            data.ModifiedThisWeek  = QueryCount($"{pathPfx}dm:thisweek file:");
            data.ModifiedThisMonth = QueryCount($"{pathPfx}dm:thismonth file:");

            // Top 10 largest files
            progress?.Report("Finding largest files...");
            data.LargestFiles = GetLargestFiles(pathPfx, sizePfx, 10);

            // Top extensions
            progress?.Report("Analysing top extensions...");
            data.TopExtensions = GetTopExtensions(pathPfx, sizePfx, data.TotalIndexedFiles);

            // Drive stats
            progress?.Report("Reading drive information...");
            data.Drives = GetDriveStats();

            return data;
        }

        // ── Largest files ─────────────────────────────────────────────────────
        private static List<LargeFileInfo> GetLargestFiles(string pathPfx, string sizePfx, int count)
        {
            var query = $"{pathPfx}{sizePfx}file:";
            Everything_SetSearchW(query);
            Everything_SetMax((uint)count);
            Everything_SetRequestFlags(RequestFullPath | RequestSize);
            Everything_SetSort(SortSizeDescending);

            if (!Everything_QueryW(true)) return [];

            var sb = new StringBuilder(4096);
            var results = new List<LargeFileInfo>();
            uint num = Everything_GetNumResults();

            for (uint i = 0; i < num; i++)
            {
                sb.Clear();
                Everything_GetResultFullPathNameW(i, sb, 4096);
                string fullPath = sb.ToString();

                if (!Everything_GetResultSize(i, out long size)) continue;

                results.Add(new LargeFileInfo
                {
                    FullPath  = fullPath,
                    FileName  = Path.GetFileName(fullPath),
                    SizeBytes = size
                });
            }

            // Reset sort to default (name ascending = 1)
            Everything_SetSort(1);
            return results;
        }

        // ── Top extensions ────────────────────────────────────────────────────
        private static List<ExtensionStat> GetTopExtensions(string pathPfx, string sizePfx, long totalFiles)
        {
            var counts = new List<(string ext, long count)>();

            foreach (var ext in CommonExtensions)
            {
                long c = QueryCount($"{pathPfx}{sizePfx}ext:{ext}");
                if (c > 0) counts.Add((ext, c));
            }

            long denom = totalFiles > 0 ? totalFiles : 1;

            return counts
                .OrderByDescending(x => x.count)
                .Take(10)
                .Select((x, idx) => new ExtensionStat
                {
                    Extension  = x.ext,
                    Count      = x.count,
                    Percentage = (double)x.count / denom * 100.0,
                    HexColor   = Palette[idx % Palette.Length]
                })
                .ToList();
        }

        // ── Helpers ───────────────────────────────────────────────────────────
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
                catch { /* skip */ }
            }
            return result;
        }
    }
}
