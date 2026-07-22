using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PrivateCompanion.Application;
using PrivateCompanion.Desktop.ViewModels;
using PrivateCompanion.Desktop.Views;
using PrivateCompanion.Infrastructure;
using PrivateCompanion.Infrastructure.Media;
using PrivateCompanion.Infrastructure.Persistence;
using PrivateCompanion.Infrastructure.Providers;

namespace PrivateCompanion.Desktop;

public partial class App : Avalonia.Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var factory = new CompanionDbContextFactory(CompanionPaths.DatabasePath);
            var repository = new CompanionRepository(factory, CompanionPaths.FindDefaultModelLauncher());
            var gateway = new OpenAiCompatibleTextGateway();
            var service = new CompanionApplicationService(repository, gateway, new PromptComposer());
            var mediaLibrary = new LocalMediaLibrary(repository, CompanionPaths.DataDirectory);
            var viewModel = new MainViewModel(service, mediaLibrary);
            await viewModel.InitializeAsync();

            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel,
            };
            desktop.Exit += (_, _) => gateway.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
