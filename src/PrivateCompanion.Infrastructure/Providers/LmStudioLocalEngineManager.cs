using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using PrivateCompanion.Application;

namespace PrivateCompanion.Infrastructure.Providers;

public sealed class LmStudioLocalEngineManager : ILocalEngineManager
{
    private readonly string? _cliPath;

    public LmStudioLocalEngineManager(string? cliPath = null)
    {
        _cliPath = string.IsNullOrWhiteSpace(cliPath) ? FindCli() : cliPath;
    }

    public async Task<LocalEngineInventory> InspectAsync(CancellationToken cancellationToken = default)
    {
        var memory = GetTotalMemoryBytes();
        if (string.IsNullOrWhiteSpace(_cliPath))
        {
            return new LocalEngineInventory(
                false,
                "Motor Tinta necesita LM Studio para administrar modelos locales. No encontré su herramienta de línea de comandos.",
                memory,
                []);
        }

        var result = await RunAsync(["ls", "--json"], TimeSpan.FromSeconds(20), cancellationToken);
        if (result.ExitCode != 0)
        {
            return new LocalEngineInventory(
                false,
                $"Encontré LM Studio, pero no pude leer sus modelos: {ShortError(result)}",
                memory,
                []);
        }

        try
        {
            using var document = JsonDocument.Parse(result.StandardOutput);
            var models = document.RootElement
                .EnumerateArray()
                .Where(x => GetString(x, "type") == "llm")
                .Select(x => new LocalEngineModel(
                    GetString(x, "modelKey"),
                    GetString(x, "displayName"),
                    GetString(x, "paramsString"),
                    GetLong(x, "sizeBytes"),
                    x.TryGetProperty("quantization", out var quantization)
                        ? GetString(quantization, "name")
                        : string.Empty,
                    GetInt(x, "maxContextLength")))
                .Where(x => !string.IsNullOrWhiteSpace(x.ModelKey))
                .OrderBy(x => x.SizeBytes)
                .ToList();

            var message = models.Count == 0
                ? "LM Studio está instalado, pero todavía no hay modelos de texto descargados."
                : $"Encontré {models.Count} modelos locales. Motor Tinta puede prepararlos sin pedirte URLs ni identificadores.";
            return new LocalEngineInventory(true, message, memory, models);
        }
        catch (JsonException ex)
        {
            return new LocalEngineInventory(
                false,
                $"LM Studio respondió con un formato que no pude interpretar: {ex.Message}",
                memory,
                []);
        }
    }

    public async Task<LocalEngineActivationResult> ActivateAsync(
        string modelKey,
        int contextLength,
        CancellationToken cancellationToken = default) =>
        await ActivateInternalAsync(modelKey, contextLength, false, cancellationToken);

    public async Task<LocalEngineActivationResult> ActivateCpuOnlyAsync(
        string modelKey,
        int contextLength,
        CancellationToken cancellationToken = default) =>
        await ActivateInternalAsync(modelKey, contextLength, true, cancellationToken);

    private async Task<LocalEngineActivationResult> ActivateInternalAsync(
        string modelKey,
        int contextLength,
        bool cpuOnly,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_cliPath))
            return new LocalEngineActivationResult(false, "No encontré LM Studio en este equipo.");

        var inventory = await InspectAsync(cancellationToken);
        if (!inventory.Models.Any(x => string.Equals(x.ModelKey, modelKey, StringComparison.Ordinal)))
            return new LocalEngineActivationResult(false, "El modelo elegido ya no aparece en la biblioteca local.");

        var server = await RunAsync(
            ["server", "start", "--port", "1234", "--bind", "127.0.0.1"],
            TimeSpan.FromSeconds(25),
            cancellationToken);
        if (server.ExitCode != 0 && !Combined(server).Contains("already", StringComparison.OrdinalIgnoreCase))
            return new LocalEngineActivationResult(false, $"No pude iniciar el servidor local: {ShortError(server)}");

        // Un único modelo evita que Tinta Viva y otras sesiones locales compitan silenciosamente por la RAM.
        await RunAsync(["unload", "--all"], TimeSpan.FromSeconds(35), cancellationToken);

        var context = Math.Clamp(contextLength, 4096, 32768);
        var loadArguments = new List<string>
        {
            "load",
            modelKey,
            "--context-length",
            context.ToString(),
            "--parallel",
            "1",
            "--identifier",
            "tinta-viva"
        };
        if (cpuOnly)
        {
            loadArguments.Add("--gpu");
            loadArguments.Add("off");
        }
        loadArguments.Add("--yes");
        var load = await RunAsync(loadArguments, TimeSpan.FromMinutes(3), cancellationToken);
        if (load.ExitCode != 0)
            return new LocalEngineActivationResult(false, $"No pude cargar el modelo: {ShortError(load)}");

        return new LocalEngineActivationResult(
            true,
            cpuOnly
                ? $"Motor Tinta quedó activo por CPU con {context:N0} tokens de contexto para evitar una caída de la GPU."
                : $"Motor Tinta quedó activo con {context:N0} tokens de contexto. El modelo se ejecuta en este equipo.");
    }

    private async Task<ProcessResult> RunAsync(
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _cliPath!,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            }
        };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);

        try
        {
            process.Start();
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);
            await process.WaitForExitAsync(timeoutSource.Token);

            // Algunas versiones del CLI dejan los pipes heredados abiertos al iniciar el
            // servidor. El proceso principal ya terminó, por lo que no debemos congelar
            // la interfaz esperando indefinidamente el EOF de esos canales.
            var drain = Task.WhenAll(output, error);
            await Task.WhenAny(drain, Task.Delay(TimeSpan.FromSeconds(2), cancellationToken));
            return new ProcessResult(
                process.ExitCode,
                output.IsCompletedSuccessfully ? output.Result : string.Empty,
                error.IsCompletedSuccessfully ? error.Result : string.Empty);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
            }
            catch { }
            return new ProcessResult(-1, string.Empty, "La operación superó el tiempo de espera.");
        }
        catch (Exception ex)
        {
            return new ProcessResult(-1, string.Empty, ex.Message);
        }
    }

    private static string? FindCli()
    {
        var candidates = new List<string>
        {
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".lmstudio",
                "bin",
                "lms.exe")
        };
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        candidates.AddRange(path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(folder => Path.Combine(folder.Trim(), "lms.exe")));
        return candidates.FirstOrDefault(File.Exists);
    }

    private static long GetTotalMemoryBytes()
    {
        var status = new MemoryStatusEx();
        return GlobalMemoryStatusEx(status) ? checked((long)status.TotalPhysical) : 0;
    }

    private static string GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static long GetLong(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.TryGetInt64(out var result) ? result : 0;

    private static int GetInt(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.TryGetInt32(out var result) ? result : 0;

    private static string Combined(ProcessResult result) => result.StandardOutput + "\n" + result.StandardError;
    private static string ShortError(ProcessResult result)
    {
        var text = Combined(result).Trim();
        if (string.IsNullOrWhiteSpace(text)) text = $"código {result.ExitCode}";
        return text.Length <= 400 ? text : text[..400];
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private sealed class MemoryStatusEx
    {
        public uint Length = checked((uint)Marshal.SizeOf<MemoryStatusEx>());
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx buffer);
}
