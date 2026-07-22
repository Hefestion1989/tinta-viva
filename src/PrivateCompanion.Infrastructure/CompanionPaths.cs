namespace PrivateCompanion.Infrastructure;

public static class CompanionPaths
{
    public static string DataDirectory
    {
        get
        {
            var configuredPath = Environment.GetEnvironmentVariable("PRIVATE_COMPANION_DATA_DIR");
            var path = string.IsNullOrWhiteSpace(configuredPath)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PrivateCompanion")
                : Path.GetFullPath(configuredPath);
            Directory.CreateDirectory(path);
            Directory.CreateDirectory(Path.Combine(path, "media"));
            Directory.CreateDirectory(Path.Combine(path, "backups"));
            Directory.CreateDirectory(Path.Combine(path, "logs"));
            return path;
        }
    }

    public static string DatabasePath => Path.Combine(DataDirectory, "companion.db");

    public static string FindDefaultModelLauncher()
    {
        var candidates = new[]
        {
            Path.Combine(DataDirectory, "run-model.bat"),
            Path.Combine(AppContext.BaseDirectory, "run-model.bat"),
            Path.Combine(AppContext.BaseDirectory, "scripts", "run-model.bat")
        };
        return candidates.FirstOrDefault(File.Exists) ?? string.Empty;
    }
}
