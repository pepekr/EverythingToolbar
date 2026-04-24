using System;
using System.IO;
using System.IO.Compression;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Win32;
using System.Windows;
using EverythingToolbar.Controls;
using EverythingToolbar.Helpers;
using EverythingToolbar.Search;

namespace EverythingToolbar.Settings
{
    public partial class Advanced : INotifyPropertyChanged
    {
        private bool _downloadUpdateButtonVisible;
        private bool _checkingForUpdatesVisible;
        private bool _noUpdatesBannerOpen;
        private string _latestVersionUrl;

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool IsLauncher => Application.Current != null;

        public int WindowsBuildVersion => Environment.OSVersion.Version.Build;

        public bool CheckingForUpdatesVisible
        {
            get => _checkingForUpdatesVisible;
            set
            {
                _checkingForUpdatesVisible = value;
                OnPropertyChanged();
            }
        }

        public bool DownloadUpdateButtonVisible
        {
            get => _downloadUpdateButtonVisible;
            set
            {
                _downloadUpdateButtonVisible = value;
                OnPropertyChanged();
            }
        }

        public bool NoUpdatesBannerOpen
        {
            get => _noUpdatesBannerOpen;
            set
            {
                // Setting the margin should be done using a style and trigger, but it's currently
                // hard to do while WPF UI styles are loaded as dynamic resources.
                NoUpdatesInfoBar.Margin = value ? new Thickness(0, 15, 0, 0) : new Thickness(0);

                _noUpdatesBannerOpen = value;
                OnPropertyChanged();
            }
        }

        private bool _isWindowsSearchHidden = !Utils.GetWindowsSearchEnabledState();
        public bool IsWindowsSearchHidden
        {
            get => _isWindowsSearchHidden;
            set
            {
                if (_isWindowsSearchHidden != value)
                {
                    _isWindowsSearchHidden = value;
                    Utils.SetWindowsSearchEnabledState(!value);
                    OnPropertyChanged();
                }
            }
        }

        public Advanced()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            SearchResultProvider.SetInstanceName(ToolbarSettings.User.InstanceName);
        }

        private async void OnCheckForUpdatesClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                CheckingForUpdatesVisible = true;
                NoUpdatesBannerOpen = false;
                DownloadUpdateButtonVisible = false;

                Version? latestVersion = await UpdateBanner.CheckForUpdateAsync();
                CheckingForUpdatesVisible = false;

                if (latestVersion != null)
                {
                    _latestVersionUrl = "https://github.com/srwi/EverythingToolbar/releases/latest";
                    DownloadUpdateButtonVisible = true;
                }
                else
                {
                    NoUpdatesBannerOpen = true;
                }
            }
            catch
            {
                CheckingForUpdatesVisible = false;
                NoUpdatesBannerOpen = false;
                DownloadUpdateButtonVisible = false;
            }
        }

        private void OnDownloadUpdateClicked(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_latestVersionUrl))
            {
                Process.Start(new ProcessStartInfo { FileName = _latestVersionUrl, UseShellExecute = true });
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private void ExportSettings_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "ZIP Archive (*.zip)|*.zip",
                FileName = "EverythingToolbar_Backup.zip",
                Title = "Export Settings"
            };

            if (dialog.ShowDialog() == true)
            {
                string configPath = Utils.GetConfigDirectory();

                // delete zip if it already exists to avoid issues with ZipFile.CreateFromDirectory
                if (File.Exists(dialog.FileName))
                {
                    File.Delete(dialog.FileName);
                }

                // create a zip file from the config directory
                // why zip? cause theres usually multiple files in the config directory and its easier to manage them as a single zipped folder
                ZipFile.CreateFromDirectory(configPath, dialog.FileName);

                MessageBox.Show("Settings exported successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ImportSettings_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "ZIP Archive (*.zip)|*.zip",
                Title = "Import Settings"
            };

            if (dialog.ShowDialog() == true)
            {
                string configPath = Utils.GetConfigDirectory();

                try
                {
                    // extracting with change
                    ZipFile.ExtractToDirectory(dialog.FileName, configPath, overwriteFiles: true);

                    MessageBox.Show("Settings imported successfully! Please restart EverythingToolbar to apply the changes.",
                                    "Restart Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show($"Failed to import settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
