namespace PrivateCompanion.Application;

public sealed record ProviderPreset(
    string Id,
    string Label,
    string ProfileName,
    string BaseUrl,
    string Description,
    string HelpUrl);

public static class ProviderPresetCatalog
{
    public static IReadOnlyList<ProviderPreset> Online { get; } =
    [
        new(
            "custom",
            "Servidor propio o compatible",
            "Servidor online propio",
            "https://",
            "Para una instancia tuya o cualquier servicio que ofrezca una API OpenAI-compatible por HTTPS.",
            ""),
        new(
            "venice",
            "Venice AI — independiente",
            "Venice AI",
            "https://api.venice.ai/api/v1",
            "Servicio externo centrado en modelos variados y privacidad. Requiere una cuenta, una clave y revisar sus costos y condiciones vigentes.",
            "https://venice.ai/settings/api"),
        new(
            "openrouter",
            "OpenRouter — catálogo amplio",
            "OpenRouter",
            "https://openrouter.ai/api/v1",
            "Servicio externo con un catálogo amplio de modelos y proveedores. Requiere una cuenta, una clave y revisar costos y políticas por modelo.",
            "https://openrouter.ai/settings/keys")
    ];

    public static ProviderPreset? Match(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)) return null;
        return Online.FirstOrDefault(x =>
            x.Id != "custom" &&
            string.Equals(x.BaseUrl.TrimEnd('/'), baseUrl.Trim().TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
            ?? Online.First(x => x.Id == "custom");
    }
}
