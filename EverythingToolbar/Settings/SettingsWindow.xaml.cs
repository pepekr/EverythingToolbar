using System;
using System.Diagnostics;
using System.Reflection;
using System.Web;
using System.Windows;
using EverythingToolbar.Search;

namespace EverythingToolbar.Settings
{
    public partial class SettingsWindow
    {
        public SettingsWindow(Type? initialPage = null)
        {
            InitializeComponent();

            Loaded += (_, _) => Dispatcher.BeginInvoke(() =>
                ThisNavigationView.Navigate(initialPage ?? typeof(About)));
        }

        private void OnReportABugClicked(object sender, RoutedEventArgs e)
        {
            string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "";
            string everythingVersion = SearchResultProvider.GetEverythingVersion().ToString();
            string osVersion = Environment.OSVersion.ToString();

            string url =
                $"https://github.com/srwi/EverythingToolbar/issues/new?template=bug_report.yml"
                + $"&version={HttpUtility.UrlEncode(version)}"
                + $"&et_version={HttpUtility.UrlEncode(everythingVersion)}"
                + $"&windows_version={HttpUtility.UrlEncode(osVersion)}";

            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
    }
}
