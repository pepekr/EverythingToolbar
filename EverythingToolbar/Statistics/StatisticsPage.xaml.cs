using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using EverythingToolbar.Search;

namespace EverythingToolbar.Statistics
{
    public partial class StatisticsPage : Page
    {
        // Keep last loaded data for export
        private StatisticsData? _currentData;

        public StatisticsPage()
        {
            Resources.Add("PercentageToWidthConverter", new PercentageToWidthConverter());
            Resources.Add("IndexConverter", new ItemIndexConverter());
            InitializeComponent();
            Loaded += async (_, _) => await LoadDataAsync();
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private async void OnRefreshClicked(object sender, RoutedEventArgs e) =>
            await LoadDataAsync();

        private async void OnApplyFilterClicked(object sender, RoutedEventArgs e) =>
            await LoadDataAsync();

        private async void OnFilterKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) await LoadDataAsync();
        }

        private void OnExportClicked(object sender, RoutedEventArgs e)
        {
            if (_currentData == null) return;
            Clipboard.SetText(BuildExportText(_currentData));
            StatusText.Text = "Copied to clipboard!";
        }

        // ── Data loading ──────────────────────────────────────────────────────

        private async Task LoadDataAsync()
        {
            SetLoading(true);
            ClearDisplay();

            string? path   = string.IsNullOrWhiteSpace(PathFilterBox.Text) ? null : PathFilterBox.Text.Trim();
            long  minSize  = GetSelectedMinSize();

            var progress = new Progress<string>(msg => StatusText.Text = msg);
            _currentData = await DiskUsageAnalyzer.AnalyzeAsync(path, minSize, progress);

            if (_currentData == null)
            {
                StatusText.Text = "Everything is not running";
                SetLoading(false);
                return;
            }

            ApplyData(_currentData);
            StatusText.Text = $"Updated {DateTime.Now:HH:mm:ss}";
            SetLoading(false);
        }

        private void SetLoading(bool loading)
        {
            LoadingBar.Visibility  = loading ? Visibility.Visible  : Visibility.Collapsed;
            RefreshButton.IsEnabled = !loading;
            ExportButton.IsEnabled  = !loading;
        }

        private long GetSelectedMinSize()
        {
            if (SizeFilterBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
                if (long.TryParse(tag, out long v)) return v;
            return 0;
        }

        private void ClearDisplay()
        {
            TotalFilesText.Text    = "—";
            TotalFoldersText.Text  = "";
            TopCategoryName.Text   = "—";
            TopCategoryCount.Text  = "";
            DriveCountText.Text    = "—";
            DriveNamesText.Text    = "";
            ModifiedTodayText.Text  = "—";
            ModifiedWeekText.Text   = "—";
            ModifiedMonthText.Text  = "—";
            PieCanvas.Children.Clear();
            CategoryList.ItemsSource   = null;
            LargestFilesList.ItemsSource = null;
            ExtensionList.ItemsSource  = null;
            DriveList.ItemsSource      = null;
        }

        private void ApplyData(StatisticsData data)
        {
            // Summary
            TotalFilesText.Text   = FormatNum(data.TotalIndexedFiles);
            TotalFoldersText.Text = $"{FormatNum(data.TotalIndexedFolders)} folders";

            var top = data.Categories.FirstOrDefault(c => c.Name != "Other" && c.FileCount > 0);
            if (top != null)
            {
                TopCategoryName.Text  = top.Name;
                TopCategoryCount.Text = $"{top.FormattedCount} files  ·  {top.FormattedPercentage}";
            }

            DriveCountText.Text = data.Drives.Count.ToString();
            DriveNamesText.Text = string.Join("   ", data.Drives.Select(d => d.Name));

            // Activity
            ModifiedTodayText.Text  = FormatNum(data.ModifiedToday);
            ModifiedWeekText.Text   = FormatNum(data.ModifiedThisWeek);
            ModifiedMonthText.Text  = FormatNum(data.ModifiedThisMonth);

            // Charts
            DrawPieChart(data.Categories);
            CategoryList.ItemsSource     = data.Categories;
            LargestFilesList.ItemsSource = data.LargestFiles;
            ExtensionList.ItemsSource    = data.TopExtensions;
            DriveList.ItemsSource        = data.Drives;
        }

        // ── Donut chart (click = open search) ────────────────────────────────

        private void DrawPieChart(IList<FileTypeCategory> categories)
        {
            PieCanvas.Children.Clear();

            var visible = categories.Where(c => c.FileCount > 0).ToList();
            if (visible.Count == 0) return;

            double cx = PieCanvas.Width / 2;
            double cy = PieCanvas.Height / 2;
            double outerR = Math.Min(cx, cy) - 4;
            double innerR = outerR * 0.45;
            double startDeg = -90.0;

            foreach (var cat in visible)
            {
                double sweepDeg = cat.Percentage / 100.0 * 360.0;
                if (sweepDeg < 0.5) { startDeg += sweepDeg; continue; }

                // ArcSegment cannot draw a full 360° arc (start == end point).
                // Split into two 180° halves when the slice fills the whole chart.
                var color = (Color)ColorConverter.ConvertFromString(cat.HexColor);
                if (sweepDeg >= 359.9)
                {
                    var half1 = CreateDonutSlice(cx, cy, innerR, outerR, startDeg,         179.9, color);
                    var half2 = CreateDonutSlice(cx, cy, innerR, outerR, startDeg + 179.9, 179.9, color);
                    half1.ToolTip = half2.ToolTip = $"{cat.Name}: {cat.FormattedCount} files ({cat.FormattedPercentage})\nClick to search";
                    half1.Cursor = half2.Cursor = Cursors.Hand;
                    var captured2 = cat;
                    half1.MouseLeftButtonUp += (_, _) => OpenSearch(captured2);
                    half2.MouseLeftButtonUp += (_, _) => OpenSearch(captured2);
                    PieCanvas.Children.Add(half1);
                    PieCanvas.Children.Add(half2);
                    startDeg += sweepDeg;
                    continue;
                }

                var slice = CreateDonutSlice(cx, cy, innerR, outerR, startDeg, sweepDeg, color);
                slice.ToolTip = $"{cat.Name}: {cat.FormattedCount} files ({cat.FormattedPercentage})\nClick to search";
                slice.Cursor  = Cursors.Hand;

                // Click → open search with this file type
                var captured = cat;
                slice.MouseLeftButtonUp += (_, _) => OpenSearch(captured);

                PieCanvas.Children.Add(slice);
                startDeg += sweepDeg;
            }
        }

        private void OpenSearch(FileTypeCategory cat)
        {
            if (cat.Extensions.Length == 0) return;
            string query = "ext:" + string.Join(";", cat.Extensions);
            if (!string.IsNullOrWhiteSpace(PathFilterBox.Text))
                query = $"path:\"{PathFilterBox.Text.Trim()}\" {query}";
            SearchState.Instance.SearchTerm = query;
            SearchWindow.Instance.Show();
        }

        private static Path CreateDonutSlice(
            double cx, double cy, double innerR, double outerR,
            double startDeg, double sweepDeg, Color color)
        {
            double startRad = DegToRad(startDeg);
            double endRad   = DegToRad(startDeg + sweepDeg);

            bool large = sweepDeg > 180;
            var fig = new PathFigure { StartPoint = Pt(cx, cy, outerR, startRad), IsClosed = true };
            fig.Segments.Add(new ArcSegment(Pt(cx, cy, outerR, endRad), new Size(outerR, outerR), 0, large, SweepDirection.Clockwise, true));
            fig.Segments.Add(new LineSegment(Pt(cx, cy, innerR, endRad), true));
            fig.Segments.Add(new ArcSegment(Pt(cx, cy, innerR, startRad), new Size(innerR, innerR), 0, large, SweepDirection.Counterclockwise, true));

            var geo = new PathGeometry();
            geo.Figures.Add(fig);

            return new Path
            {
                Data = geo,
                Fill = new SolidColorBrush(color),
                Stroke = new SolidColorBrush(Colors.Transparent),
                StrokeThickness = 2
            };
        }

        // ── Export ────────────────────────────────────────────────────────────

        private static string BuildExportText(StatisticsData d)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== EverythingToolbar Statistics ===");
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            sb.AppendLine($"Indexed Files:   {d.TotalIndexedFiles:N0}");
            sb.AppendLine($"Indexed Folders: {d.TotalIndexedFolders:N0}");
            sb.AppendLine();

            sb.AppendLine("--- Recent Activity ---");
            sb.AppendLine($"Modified today:       {d.ModifiedToday:N0}");
            sb.AppendLine($"Modified this week:   {d.ModifiedThisWeek:N0}");
            sb.AppendLine($"Modified this month:  {d.ModifiedThisMonth:N0}");
            sb.AppendLine();

            sb.AppendLine("--- File Type Distribution ---");
            foreach (var c in d.Categories.Where(c => c.FileCount > 0))
                sb.AppendLine($"  {c.Name,-14} {c.FileCount,10:N0}  ({c.Percentage:F1}%)");
            sb.AppendLine();

            sb.AppendLine("--- Top 10 Largest Files ---");
            for (int i = 0; i < d.LargestFiles.Count; i++)
            {
                var f = d.LargestFiles[i];
                sb.AppendLine($"  {i + 1,2}. {f.FormattedSize,10}  {f.FullPath}");
            }
            sb.AppendLine();

            sb.AppendLine("--- Top Extensions ---");
            foreach (var e in d.TopExtensions)
                sb.AppendLine($"  .{e.Extension,-12} {e.Count,10:N0}  ({e.Percentage:F1}%)");
            sb.AppendLine();

            sb.AppendLine("--- Drives ---");
            foreach (var dr in d.Drives)
                sb.AppendLine($"  {dr.Name}  Used: {dr.FormattedUsed} / {dr.FormattedTotal}  ({dr.UsedPercent:F0}%)");

            return sb.ToString();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static Point  Pt(double cx, double cy, double r, double rad) =>
            new(cx + r * Math.Cos(rad), cy + r * Math.Sin(rad));

        private static double DegToRad(double deg) => deg * Math.PI / 180.0;

        private static string FormatNum(long n) =>
            n >= 1_000_000 ? $"{n / 1_000_000.0:F2}M"
            : n >= 1_000   ? $"{n / 1_000.0:F1}K"
            : n.ToString();
    }

    // ── Converters ────────────────────────────────────────────────────────────

    public class PercentageToWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2) return 0.0;
            if (values[0] is not double pct || values[1] is not double w) return 0.0;
            return Math.Max(0.0, Math.Min(w, w * pct / 100.0));
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>Converts a ContentPresenter to its 1-based index inside the parent ItemsControl.</summary>
    public class ItemIndexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not ContentPresenter cp) return "?";
            if (ItemsControl.ItemsControlFromItemContainer(cp) is not ItemsControl ic) return "?";
            int idx = ic.ItemContainerGenerator.IndexFromContainer(cp);
            return idx >= 0 ? (idx + 1).ToString() : "?";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
