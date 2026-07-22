using PrivateCompanion.Infrastructure.Media;
using PrivateCompanion.Infrastructure.Persistence;

namespace PrivateCompanion.Tests;

public sealed class MediaLibraryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "PrivateCompanion.MediaTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Imports_a_copy_and_skips_a_duplicate_for_the_same_character()
    {
        Directory.CreateDirectory(_directory);
        var source = Path.Combine(_directory, "retrato.png");
        await File.WriteAllBytesAsync(source, [137, 80, 78, 71, 13, 10, 26, 10, 1, 2, 3, 4]);

        var factory = new CompanionDbContextFactory(Path.Combine(_directory, "test.db"));
        var repository = new CompanionRepository(factory, string.Empty);
        await repository.InitializeAsync();
        var character = Assert.Single(await repository.GetCharactersAsync());
        var library = new LocalMediaLibrary(repository, Path.Combine(_directory, "data"));

        var first = await library.ImportAsync(character.Id, [source]);
        var second = await library.ImportAsync(character.Id, [source]);
        var stored = Assert.Single(await repository.GetMediaAsync(character.Id));

        Assert.Equal(1, first.Imported);
        Assert.Empty(first.Errors);
        Assert.Equal(1, second.Skipped);
        Assert.True(File.Exists(library.GetAbsolutePath(stored)));
        Assert.True(File.Exists(source));

        await repository.SaveMediaPresentationAsync(new()
        {
            CharacterId = character.Id,
            PortraitMediaAssetId = stored.Id,
            BackgroundMediaAssetId = stored.Id,
            PortraitZoom = 2.25,
            PortraitFocusX = 0.35,
            PortraitFocusY = 0.7,
            BackgroundZoom = 1.4,
            BackgroundFocusX = 0.6,
            BackgroundFocusY = 0.2
        });
        var presentation = await repository.GetMediaPresentationAsync(character.Id);
        Assert.Equal(stored.Id, presentation.PortraitMediaAssetId);
        Assert.Equal(stored.Id, presentation.BackgroundMediaAssetId);
        Assert.Equal(2.25, presentation.PortraitZoom);
        Assert.Equal(0.7, presentation.PortraitFocusY);
        Assert.Equal(1.4, presentation.BackgroundZoom);
        Assert.Equal(0.2, presentation.BackgroundFocusY);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
