using System.Security.Cryptography;
using PrivateCompanion.Application;
using PrivateCompanion.Domain;

namespace PrivateCompanion.Infrastructure.Media;

public sealed class LocalMediaLibrary : IMediaLibrary
{
    private const long MaximumFileSize = 100L * 1024 * 1024;
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif" };

    private readonly ICompanionRepository _repository;
    private readonly string _dataRoot;
    private readonly string _originalsDirectory;

    public LocalMediaLibrary(ICompanionRepository repository, string dataRoot)
    {
        _repository = repository;
        _dataRoot = Path.GetFullPath(dataRoot);
        _originalsDirectory = EnsureInsideDataRoot(Path.Combine(_dataRoot, "media", "originals"));
        Directory.CreateDirectory(_originalsDirectory);
    }

    public async Task<MediaImportResult> ImportAsync(
        Guid characterId,
        IReadOnlyList<string> sourcePaths,
        CancellationToken cancellationToken = default)
    {
        var imported = 0;
        var skipped = 0;
        var errors = new List<string>();
        var knownHashes = (await _repository.GetMediaAsync(characterId, cancellationToken))
            .Select(x => x.Sha256)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var sourcePath in sourcePaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var source = new FileInfo(sourcePath);
                if (!source.Exists) throw new FileNotFoundException("El archivo ya no existe.", source.FullName);
                if (!SupportedExtensions.Contains(source.Extension)) throw new InvalidDataException("Formato no admitido.");
                if (source.Length == 0) throw new InvalidDataException("El archivo está vacío.");
                if (source.Length > MaximumFileSize) throw new InvalidDataException("La imagen supera el límite de 100 MB.");

                string hash;
                await using (var input = source.OpenRead())
                    hash = Convert.ToHexString(await SHA256.HashDataAsync(input, cancellationToken)).ToLowerInvariant();

                if (!knownHashes.Add(hash))
                {
                    skipped++;
                    continue;
                }

                var fileName = $"{hash}{source.Extension.ToLowerInvariant()}";
                var destination = EnsureInsideDataRoot(Path.Combine(_originalsDirectory, fileName));
                if (!File.Exists(destination))
                {
                    await using var input = new FileStream(source.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
                    await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
                    await input.CopyToAsync(output, cancellationToken);
                }

                await _repository.SaveMediaAsync(new MediaAsset
                {
                    CharacterId = characterId,
                    RelativePath = Path.GetRelativePath(_dataRoot, destination),
                    Sha256 = hash,
                    Prompt = source.Name,
                    WorkflowName = "Importación local",
                    Seed = 0,
                    CreatedAt = DateTimeOffset.UtcNow
                }, cancellationToken);
                imported++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors.Add($"{Path.GetFileName(sourcePath)}: {ex.Message}");
            }
        }

        return new MediaImportResult(imported, skipped, errors);
    }

    public string GetAbsolutePath(MediaAsset asset) =>
        EnsureInsideDataRoot(Path.Combine(_dataRoot, asset.RelativePath));

    private string EnsureInsideDataRoot(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var rootPrefix = _dataRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("La ruta de medios intentó salir del almacenamiento privado.");
        return fullPath;
    }
}
