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
        public SettingsWindow()
        {
            InitializeComponent();

            // Default navigation to the 'About' (Home) page when the window loads
            Loaded += (_, _) => Dispatcher.BeginInvoke(() => ThisNavigationView.Navigate(typeof(About)));
        }

        // Handles the Navigation View's Back Button
        private void OnBackRequested(object sender, RoutedEventArgs e)
        {
            // Closes the settings window, returning the user to their normal workflow
            this.Close();
            EverythingToolbar.SearchWindow.Instance.Show();
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