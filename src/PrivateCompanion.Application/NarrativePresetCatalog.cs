using PrivateCompanion.Domain;

namespace PrivateCompanion.Application;

public sealed record NarrativePreset(
    NarrativeStyle Style,
    string Label,
    string Description,
    double Temperature,
    int MaxOutputTokens,
    int ContextBudget);

public static class NarrativePresetCatalog
{
    public static IReadOnlyList<NarrativePreset> All { get; } =
    [
        new(
            NarrativeStyle.Conversation,
            "Conversación ágil",
            "Respuestas más breves y directas, con prioridad al diálogo.",
            0.78,
            420,
            6144),
        new(
            NarrativeStyle.Roleplay,
            "Roleplay equilibrado",
            "Combina diálogo, acciones y atmósfera sin volver cada turno una novela.",
            0.90,
            760,
            8192),
        new(
            NarrativeStyle.Novel,
            "Narración extensa",
            "Prosa más desarrollada, sensorial y pausada para escenas largas.",
            0.86,
            1200,
            12288)
    ];

    public static NarrativePreset For(NarrativeStyle style) =>
        All.FirstOrDefault(x => x.Style == style) ?? All[1];
}
