using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System;
using System.Linq;
using System.Threading.Tasks;
using ClinicalApplications.ViewModels;
{
    
}

namespace ClinicalApplications.Views
{
    public partial class MainView : UserControl
    {
        public MainView()
        {
            InitializeComponent();
        }

        private async void OnUploadCsvClicked(object? sender, RoutedEventArgs e)
        {
            try
            {
                var window = this.VisualRoot as Window;
                if (window?.StorageProvider is null)
                    return;

                var files = await window.StorageProvider.OpenFilePickerAsync(
                    new FilePickerOpenOptions
                    {
                        AllowMultiple = false,
                        Title = "Select patient CSV file",
                        FileTypeFilter = new[]
                        {
                            new FilePickerFileType("CSV files")
                            {
                                Patterns = new[] { "*.csv" }
                            }
                        }
                    });

                var file = files?.FirstOrDefault();
                if (file != null && DataContext is ClinicalApplications.ViewModels.MainViewViewModel vm)
                {
                    var path = file.TryGetLocalPath();
                    if (!string.IsNullOrEmpty(path))
                    {
                        await vm.LoadPatientFromCsvAsync(path);
                    }
                    else
                    {
                        await ShowMessageBox(window, "Error", "Could not resolve file path.");
                    }
                }
            }
            catch (Exception ex)
            {
                var window = this.VisualRoot as Window;
                await ShowMessageBox(window, "Error", $"Failed to load CSV file:\n{ex.Message}");
            }
        }

        private static async Task ShowMessageBox(Window? window, string title, string message)
        {
            if (window is null) return;

            var dialog = new Window
            {
                Title = title,
                Width = 400,
                Height = 200,
                Content = new TextBlock
                {
                    Text = message,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    Margin = new Thickness(16)
                }
            };

            await dialog.ShowDialog(window);
        }
    }
}
