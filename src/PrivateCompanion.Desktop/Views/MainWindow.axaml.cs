using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PrivateCompanion.Desktop.ViewModels;
using PrivateCompanion.Infrastructure;

namespace PrivateCompanion.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void AddImages_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Agregar imágenes a la galería privada",
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("Imágenes")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp", "*.gif"]
                }
            ]
        });

        var localPaths = files
            .Select(file => file.TryGetLocalPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .ToList();
        await viewModel.ImportImagesAsync(localPaths);
    }

    private void OpenMediaFolder_Click(object? sender, RoutedEventArgs e)
    {
        var directory = Path.Combine(CompanionPaths.DataDirectory, "media", "originals");
        Directory.CreateDirectory(directory);
        var startInfo = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
        startInfo.ArgumentList.Add(directory);
        Process.Start(startInfo);
    }

    private void OpenCurrentImage_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel { AlbumItem: { } item } || !File.Exists(item.AbsolutePath)) return;
        Process.Start(new ProcessStartInfo(item.AbsolutePath) { UseShellExecute = true });
    }

    private void CloseApplication_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
