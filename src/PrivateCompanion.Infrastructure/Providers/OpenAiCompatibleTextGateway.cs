using System.Diagnostics;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using PrivateCompanion.Application;
using PrivateCompanion.Domain;

namespace PrivateCompanion.Infrastructure.Providers;

public sealed class OpenAiCompatibleTextGateway : ITextGenerationGateway, IDisposable
{
    private readonly IProviderSecretStore _secretStore;
    private readonly ILocalEngineManager? _localEngineManager;
    private readonly HttpClient _client;
    private Process? _startedProcess;

    public OpenAiCompatibleTextGateway(
        IProviderSecretStore secretStore,
        HttpMessageHandler? handler = null,
        ILocalEngineManager? localEngineManager = null)
    {
        _secretStore = secretStore;
        _localEngineManager = localEngineManager;
        _client = handler is null
            ? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
            : new HttpClient(handler);
        _client.Timeout = Timeout.InfiniteTimeSpan;
    }

    public async IAsyncEnumerable<string> StreamAsync(
        TextGenerationRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await EnsureManagedLmStudioReadyAsync(request.Provider, cancellationToken);
        var probe = await ProbeAsync(request.Provider, request.Provider.AutoStart, cancellationToken);
        if (!probe.IsAvailable) throw new InvalidOperationException(probe.Message);
        var selectedModel = string.IsNullOrWhiteSpace(request.Provider.Model)
            ? probe.Models.FirstOrDefault()
            : request.Provider.Model;
        if (string.IsNullOrWhiteSpace(selectedModel))
            throw new InvalidOperationException("El motor está conectado, pero no informó ningún modelo. Cargá uno y volvé a intentarlo.");

        // Algunas combinaciones de LM Studio y modelos GGUF se cierran al usar SSE
        // aunque completan correctamente la misma solicitud sin streaming.
        if (request.Provider.Kind == ProviderKind.LMStudio &&
            request.Provider.Location == ProviderLocation.Local)
        {
            yield return await CompleteAsync(request, selectedModel, cancellationToken);
            yield break;
        }

        var endpoint = BuildEndpoint(request.Provider.BaseUrl, "chat/completions");
        using var message = CreateChatRequest(request, selectedModel, endpoint, true);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        await AddAuthorizationAsync(message, request.Provider, cancellationToken);
        using var response = await _client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"El motor devolvió {(int)response.StatusCode}: {Trim(error, 500)}");
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase))
        {
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var content = ExtractFullContent(json);
            if (!string.IsNullOrEmpty(content)) yield return content;
            yield break;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null) yield break;
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) continue;
            var payload = line[5..].Trim();
            if (payload == "[DONE]") yield break;
            if (TryExtractError(payload, out var error))
                throw new InvalidOperationException($"El motor interrumpió la generación: {error}");
            var content = ExtractDelta(payload);
            if (!string.IsNullOrEmpty(content)) yield return content;
        }
    }

    private async Task EnsureManagedLmStudioReadyAsync(
        ProviderProfile provider,
        CancellationToken cancellationToken)
    {
        if (_localEngineManager is null || !IsManagedMotorTinta(provider))
            return;

        var probe = await TryProbeAsync(provider, cancellationToken);
        if (probe.IsAvailable &&
            probe.Models.Any(x => string.Equals(x, provider.Model, StringComparison.OrdinalIgnoreCase)))
            return;

        var inventory = await _localEngineManager.InspectAsync(cancellationToken);
        if (!inventory.IsAvailable)
            throw new InvalidOperationException(inventory.Message);

        var configuredName = ManagedModelName(provider);
        var model = FindConfiguredModel(inventory, configuredName);
        if (model is null)
            throw new InvalidOperationException(
                $"No encontré el modelo local configurado «{configuredName}». Abrí Configuración y elegí otro modelo.");

        var activation = await _localEngineManager.ActivateAsync(
            model.ModelKey,
            provider.ContextBudget,
            cancellationToken);
        if (!activation.IsSuccess)
            throw new InvalidOperationException(activation.Message);

        var finalProbe = await TryProbeAsync(provider, cancellationToken);
        if (!finalProbe.IsAvailable ||
            !finalProbe.Models.Any(x => string.Equals(x, provider.Model, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException(
                $"Motor Tinta intentó cargar «{configuredName}», pero el modelo no quedó disponible.");
    }

    private async Task<string> CompleteAsync(
        TextGenerationRequest request,
        string selectedModel,
        CancellationToken cancellationToken)
    {
        var endpoint = BuildEndpoint(request.Provider.BaseUrl, "chat/completions");
        var result = await SendCompleteRequestAsync(request, selectedModel, endpoint, cancellationToken);
        if (!result.IsSuccessStatusCode &&
            IsManagedMotorTinta(request.Provider) &&
            IsVulkanDeviceLost(result.Content))
        {
            await RecoverManagedLmStudioOnCpuAsync(request.Provider, cancellationToken);
            result = await SendCompleteRequestAsync(request, selectedModel, endpoint, cancellationToken);
        }
        if (!result.IsSuccessStatusCode)
            throw new InvalidOperationException($"El motor devolvió {result.StatusCode}: {Trim(result.Content, 500)}");
        if (TryExtractError(result.Content, out var error))
            throw new InvalidOperationException($"El motor no pudo completar la respuesta: {error}");

        var content = ExtractFullContent(result.Content);
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("El motor terminó sin devolver texto.");
        return content;
    }

    private async Task<CompletionResult> SendCompleteRequestAsync(
        TextGenerationRequest request,
        string selectedModel,
        Uri endpoint,
        CancellationToken cancellationToken)
    {
        using var message = CreateChatRequest(request, selectedModel, endpoint, false);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        await AddAuthorizationAsync(message, request.Provider, cancellationToken);
        using var response = await _client.SendAsync(message, HttpCompletionOption.ResponseContentRead, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return new CompletionResult(response.IsSuccessStatusCode, (int)response.StatusCode, json);
    }

    private async Task RecoverManagedLmStudioOnCpuAsync(
        ProviderProfile provider,
        CancellationToken cancellationToken)
    {
        if (_localEngineManager is null) return;
        var inventory = await _localEngineManager.InspectAsync(cancellationToken);
        if (!inventory.IsAvailable)
            throw new InvalidOperationException(inventory.Message);

        var configuredName = ManagedModelName(provider);
        var model = FindConfiguredModel(inventory, configuredName);
        if (model is null)
            throw new InvalidOperationException(
                $"La GPU dejó de responder y no pude encontrar «{configuredName}» para recuperarlo por CPU.");

        var recovery = await _localEngineManager.ActivateCpuOnlyAsync(
            model.ModelKey,
            provider.ContextBudget,
            cancellationToken);
        if (!recovery.IsSuccess)
            throw new InvalidOperationException(
                $"La GPU dejó de responder y la recuperación por CPU falló: {recovery.Message}");
    }

    private static HttpRequestMessage CreateChatRequest(
        TextGenerationRequest request,
        string selectedModel,
        Uri endpoint,
        bool stream)
    {
        var body = new
        {
            model = selectedModel,
            messages = request.Messages.Select(x => new { role = x.Role, content = x.Content }),
            temperature = request.Temperature ?? request.Provider.Temperature,
            top_p = request.Provider.TopP,
            max_tokens = request.MaxOutputTokens ?? request.Provider.MaxOutputTokens,
            stream
        };
        return new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
    }

    public async Task<ProviderProbe> ProbeAsync(
        ProviderProfile provider,
        bool allowAutoStart,
        CancellationToken cancellationToken = default)
    {
        ValidateEndpoint(provider);
        var first = await TryProbeAsync(provider, cancellationToken);
        if (first.IsAvailable || provider.Location == ProviderLocation.Remote || !allowAutoStart || !provider.AutoStart || string.IsNullOrWhiteSpace(provider.LaunchCommand)) return first;

        if (!TryLaunch(provider.LaunchCommand, out var launchError))
            return new ProviderProbe(false, launchError, []);

        for (var attempt = 0; attempt < 24; attempt++)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            var probe = await TryProbeAsync(provider, cancellationToken);
            if (probe.IsAvailable) return probe with { Message = "Motor local iniciado y conectado." };
        }
        return new ProviderProbe(false, "El motor se inició, pero no quedó disponible dentro de 48 segundos. Revisá el modelo o el registro del servidor.", []);
    }

    private async Task<ProviderProbe> TryProbeAsync(ProviderProfile provider, CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(4));
            using var request = new HttpRequestMessage(HttpMethod.Get, BuildEndpoint(provider.BaseUrl, "models"));
            await AddAuthorizationAsync(request, provider, timeout.Token);
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseContentRead, timeout.Token);
            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            if (!response.IsSuccessStatusCode)
                return new ProviderProbe(false, $"Motor no disponible: HTTP {(int)response.StatusCode}.", []);

            var models = ParseModels(body);
            var location = provider.Location == ProviderLocation.Local ? "local" : "remoto";
            return new ProviderProbe(true, $"Conectado al motor {location}.", models);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ProviderProbe(false, "El motor no respondió a tiempo.", []);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            var hint = provider.Location == ProviderLocation.Local
                ? "Podés iniciarlo desde Configuración o enviar un mensaje para el arranque automático."
                : "Revisá la URL HTTPS, el token y el estado del servidor remoto.";
            return new ProviderProbe(false, $"Motor desconectado. {hint}", []);
        }
    }

    private bool TryLaunch(string command, out string error)
    {
        error = string.Empty;
        try
        {
            var expanded = Environment.ExpandEnvironmentVariables(command.Trim().Trim('"'));
            if (!File.Exists(expanded))
            {
                error = $"No se encontró el iniciador configurado: {expanded}";
                return false;
            }
            _startedProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/d /c call \"{expanded}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = Path.GetDirectoryName(expanded) ?? Environment.CurrentDirectory
            });
            return true;
        }
        catch (Exception ex)
        {
            error = $"No se pudo iniciar el motor local: {ex.Message}";
            return false;
        }
    }

    private static Uri BuildEndpoint(string baseUrl, string route) => new($"{baseUrl.TrimEnd('/')}/{route}");

    internal static void ValidateEndpoint(ProviderProfile provider)
    {
        if (!Uri.TryCreate(provider.BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new InvalidOperationException("La URL del motor no es válida.");
        if (provider.Location == ProviderLocation.Local && !uri.IsLoopback)
            throw new InvalidOperationException("Un perfil local solo admite localhost/127.0.0.1.");
        if (provider.Location == ProviderLocation.Remote && uri.Scheme != "https")
            throw new InvalidOperationException("Un motor remoto necesita una URL HTTPS para proteger el contexto enviado.");
    }

    private async Task AddAuthorizationAsync(HttpRequestMessage message, ProviderProfile provider, CancellationToken cancellationToken)
    {
        var secret = await _secretStore.GetAsync(provider.Id, cancellationToken);
        if (!string.IsNullOrWhiteSpace(secret))
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
    }

    private static IReadOnlyList<string> ParseModels(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array) return [];
        return data.EnumerateArray()
            .Select(x => x.TryGetProperty("id", out var id) ? id.GetString() : null)
            .Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().ToList();
    }

    private static string? ExtractDelta(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var choice = document.RootElement.GetProperty("choices")[0];
            if (choice.TryGetProperty("delta", out var delta) && delta.TryGetProperty("content", out var content))
                return content.ValueKind == JsonValueKind.String ? content.GetString() : null;
            if (choice.TryGetProperty("text", out var text)) return text.GetString();
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException) { }
        return null;
    }

    private static string? ExtractFullContent(string json)
    {
        using var document = JsonDocument.Parse(json);
        var choice = document.RootElement.GetProperty("choices")[0];
        if (choice.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var content)) return content.GetString();
        if (choice.TryGetProperty("text", out var text)) return text.GetString();
        return null;
    }

    private static bool TryExtractError(string json, out string message)
    {
        message = string.Empty;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("error", out var error)) return false;
            if (error.ValueKind == JsonValueKind.Object &&
                error.TryGetProperty("message", out var nested) &&
                nested.ValueKind == JsonValueKind.String)
                message = nested.GetString() ?? string.Empty;
            else if (error.ValueKind == JsonValueKind.String)
                message = error.GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(message) &&
                document.RootElement.TryGetProperty("message", out var rootMessage) &&
                rootMessage.ValueKind == JsonValueKind.String)
                message = rootMessage.GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(message)) message = "error no especificado";
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsManagedMotorTinta(ProviderProfile provider) =>
        provider.Kind == ProviderKind.LMStudio &&
        provider.Location == ProviderLocation.Local &&
        provider.Name.StartsWith("Motor Tinta ·", StringComparison.OrdinalIgnoreCase);

    private static string ManagedModelName(ProviderProfile provider) =>
        provider.Name["Motor Tinta ·".Length..].Trim();

    private static LocalEngineModel? FindConfiguredModel(
        LocalEngineInventory inventory,
        string configuredName) =>
        inventory.Models.FirstOrDefault(x =>
            string.Equals(x.DisplayName, configuredName, StringComparison.OrdinalIgnoreCase));

    private static bool IsVulkanDeviceLost(string content) =>
        content.Contains("ErrorDeviceLost", StringComparison.OrdinalIgnoreCase) ||
        content.Contains("vk::Queue::submit", StringComparison.OrdinalIgnoreCase) ||
        content.Contains("model has crashed", StringComparison.OrdinalIgnoreCase);

    private sealed record CompletionResult(bool IsSuccessStatusCode, int StatusCode, string Content);

    private static string Trim(string value, int max) => value.Length <= max ? value : value[..max];
    public void Dispose()
    {
        _client.Dispose();
        try
        {
            if (_startedProcess is { HasExited: false }) _startedProcess.Kill(entireProcessTree: true);
        }
        catch { }
        _startedProcess?.Dispose();
    }
}
