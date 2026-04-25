using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace EverythingToolbar.Statistics
{
    public partial class StatisticsPage : Page
    {
        public StatisticsPage()
        {
            Resources.Add("PercentageToWidthConverter", new PercentageToWidthConverter());
            InitializeComponent();
            Loaded += async (_, _) => await LoadDataAsync();
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private async void OnRefreshClicked(object sender, RoutedEventArgs e) =>
            await LoadDataAsync();

        private async void OnApplyFilterClicked(object sender, RoutedEventArgs e) =>
            await LoadDataAsync();

        // Press Enter in the path box → apply filter
        private async void OnFilterKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                await LoadDataAsync();
        }

        // ── Data loading ──────────────────────────────────────────────────────

        private async Task LoadDataAsync()
        {
            RefreshButton.IsEnabled = false;
            StatusText.Text = "Loading...";
            ClearDisplay();

            string? path = string.IsNullOrWhiteSpace(PathFilterBox.Text) ? null : PathFilterBox.Text.Trim();
            long minSize = GetSelectedMinSize();

            var progress = new Progress<string>(msg => StatusText.Text = msg);
            var data = await DiskUsageAnalyzer.AnalyzeAsync(path, minSize, progress);

            if (data == null)
            {
                StatusText.Text = "Everything is not running";
                RefreshButton.IsEnabled = true;
                return;
            }

            ApplyData(data);
            StatusText.Text = $"Updated {DateTime.Now:HH:mm:ss}";
            RefreshButton.IsEnabled = true;
        }

        private long GetSelectedMinSize()
        {
            if (SizeFilterBox.SelectedItem is ComboBoxItem item && item.Tag is string tagStr)
                if (long.TryParse(tagStr, out long val)) return val;
            return 0;
        }

        private void ClearDisplay()
        {
            TotalFilesText.Text   = "—";
            TotalFoldersText.Text = "";
            TopCategoryName.Text  = "—";
            TopCategoryCount.Text = "";
            DriveCountText.Text   = "—";
            DriveNamesText.Text   = "";
            PieCanvas.Children.Clear();
            CategoryList.ItemsSource = null;
            DriveList.ItemsSource    = null;
        }

        private void ApplyData(StatisticsData data)
        {
            // Summary cards
            TotalFilesText.Text   = FormatLargeNumber(data.TotalIndexedFiles);
            TotalFoldersText.Text = $"{FormatLargeNumber(data.TotalIndexedFolders)} folders";

            var top = data.Categories.FirstOrDefault(c => c.Name != "Other" && c.FileCount > 0);
            if (top != null)
            {
                TopCategoryName.Text  = top.Name;
                TopCategoryCount.Text = $"{top.FormattedCount} files  ·  {top.FormattedPercentage}";
            }

            DriveCountText.Text = data.Drives.Count.ToString();
            DriveNamesText.Text = string.Join("   ", data.Drives.Select(d => d.Name));

            // Charts
            DrawPieChart(data.Categories);
            CategoryList.ItemsSource = data.Categories;
            DriveList.ItemsSource    = data.Drives;
        }

        // ── Donut chart ───────────────────────────────────────────────────────

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

                var color = (Color)ColorConverter.ConvertFromString(cat.HexColor);
                var slice = CreateDonutSlice(cx, cy, innerR, outerR, startDeg, sweepDeg, color);
                slice.ToolTip = $"{cat.Name}: {cat.FormattedCount} files ({cat.FormattedPercentage})";
                PieCanvas.Children.Add(slice);

                startDeg += sweepDeg;
            }
        }

        private static Path CreateDonutSlice(
            double cx, double cy, double innerR, double outerR,
            double startDeg, double sweepDeg, Color color)
        {
            double startRad = DegToRad(startDeg);
            double endRad   = DegToRad(startDeg + sweepDeg);

            var outerStart = Pt(cx, cy, outerR, startRad);
            var outerEnd   = Pt(cx, cy, outerR, endRad);
            var innerStart = Pt(cx, cy, innerR, startRad);
            var innerEnd   = Pt(cx, cy, innerR, endRad);

            bool large = sweepDeg > 180;

            var fig = new PathFigure { StartPoint = outerStart, IsClosed = true };
            fig.Segments.Add(new ArcSegment(outerEnd, new Size(outerR, outerR), 0, large, SweepDirection.Clockwise, true));
            fig.Segments.Add(new LineSegment(innerEnd, true));
            fig.Segments.Add(new ArcSegment(innerStart, new Size(innerR, innerR), 0, large, SweepDirection.Counterclockwise, true));

            var geo = new PathGeometry();
            geo.Figures.Add(fig);

            return new Path
            {
                Data            = geo,
                Fill            = new SolidColorBrush(color),
                Stroke          = new SolidColorBrush(Colors.Transparent),
                StrokeThickness = 2
            };
        }

        private static Point  Pt(double cx, double cy, double r, double rad) =>
            new(cx + r * Math.Cos(rad), cy + r * Math.Sin(rad));

        private static double DegToRad(double deg) => deg * Math.PI / 180.0;

        private static string FormatLargeNumber(long n) =>
            n >= 1_000_000 ? $"{n / 1_000_000.0:F2}M"
            : n >= 1_000   ? $"{n / 1_000.0:F1}K"
            : n.ToString();
    }

    /// <summary>Converts (percentage 0-100, containerWidth) → bar pixel width.</summary>
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
}
