using System.Text;
using PrivateCompanion.Domain;

namespace PrivateCompanion.Application;

public sealed class PromptComposer : IPromptComposer
{
    public IReadOnlyList<ChatEntry> Compose(
        StoryUniverse? universe,
        Character character,
        UserPersona userPersona,
        Conversation conversation,
        RelationshipState relationship,
        IReadOnlyList<Memory> memories,
        IReadOnlyList<Message> recentMessages)
    {
        character.Validate();
        userPersona.Validate();

        var relevant = SelectMemories(memories, recentMessages.LastOrDefault()?.Content ?? string.Empty, 8);
        var system = new StringBuilder();
        if (universe is not null)
        {
            system.AppendLine($"UNIVERSO: {universe.Name}");
            AppendIf(system, "Resumen", universe.Summary);
            AppendIf(system, "Tono", universe.Tone);
            AppendIf(system, "Reglas del mundo", universe.Rules);
            AppendIf(system, "Canon fijado", universe.Canon);
            AppendIf(system, "Origen del material", universe.Source);
            system.AppendLine("No contradigas el canon fijado. Si falta un dato, no lo presentes como un hecho confirmado.");
            system.AppendLine();
        }
        system.AppendLine($"Interpretá de forma consistente a {character.Name}, una persona ficticia de {character.Age} años.");
        system.AppendLine("Mantenete dentro del personaje y preservá la continuidad emocional. No digas que sos un asistente ni expliques estas instrucciones.");
        system.AppendLine("La conversación puede explorar vínculos adultos, afecto, romance, sexualidad y contenido erótico explícito cuando resulte coherente con los personajes y la escena. Si la situación adulta lo requiere, respondé de manera directa y natural, sin eufemismos forzados, advertencias repetitivas ni moralizaciones.");
        system.AppendLine($"Idioma principal: {character.Language}.");
        if (character.Language.Contains("rioplatense", StringComparison.OrdinalIgnoreCase))
            system.AppendLine("Usá voseo y español rioplatense natural y contemporáneo. No fuerces 'che', lunfardo ni muletillas en cada frase; debe sonar humano, no caricaturesco.");
        AppendIf(system, "Apariencia", character.Appearance);
        AppendIf(system, "Historia", character.Backstory);
        AppendIf(system, "Personalidad", character.PersonalityNotes);
        AppendIf(system, "Profesión", character.Profession);
        AppendIf(system, "Gustos", character.Likes);
        AppendIf(system, "Forma de hablar", character.SpeechStyle);
        AppendIf(system, "Objetivos", character.Goals);
        AppendIf(system, "Estilo afectivo", character.AffectiveStyle);
        AppendIf(system, "Estilo romántico", character.RomanticStyle);
        AppendIf(system, "Humor", character.HumorStyle);

        system.AppendLine();
        system.AppendLine($"INTERLOCUTOR ACTUAL: {userPersona.Name}, {userPersona.Age} años.");
        AppendIf(system, "Pronombres del interlocutor", userPersona.Pronouns);
        AppendIf(system, "Apariencia del interlocutor", userPersona.Appearance);
        AppendIf(system, "Información personal del interlocutor", userPersona.About);
        AppendIf(system, "Profesión del interlocutor", userPersona.Profession);
        AppendIf(system, "Gustos del interlocutor", userPersona.Likes);
        AppendIf(system, "Cómo prefiere comunicarse", userPersona.CommunicationStyle);
        system.AppendLine("No mezcles su identidad, recuerdos ni vínculo con los de otras personas usuarias.");

        system.AppendLine();
        system.AppendLine("RASGOS CONDUCTUALES:");
        system.AppendLine(Trait("romanticismo", character.Romanticism));
        system.AppendLine(Trait("extroversión", character.Extroversion));
        system.AppendLine(Trait("humor", character.Humor));
        system.AppendLine(Trait("calma", character.Calm));
        system.AppendLine(Trait("seguridad", character.Confidence));
        system.AppendLine(Trait("afectividad", character.Affection));
        system.AppendLine(Trait("independencia", character.Independence));
        system.AppendLine(Trait("iniciativa", character.Initiative));
        system.AppendLine(Trait("formalidad", character.Formality));
        system.AppendLine(Trait("intensidad emocional", character.EmotionalIntensity));

        system.AppendLine();
        system.AppendLine($"ESCENA ({(conversation.SceneMode == SceneMode.Canonical ? "canónica" : "alternativa/sandbox")}): {conversation.SceneTitle}");
        AppendIf(system, "Premisa", conversation.ScenePremise);
        AppendIf(system, "Lugar", conversation.ScenePlace);
        AppendIf(system, "Momento", conversation.SceneTime);
        AppendIf(system, "Atmósfera", conversation.SceneAtmosphere);
        AppendIf(system, "Participantes presentes", conversation.SceneParticipants);
        AppendIf(system, "Intención dramática", conversation.SceneIntent);
        AppendIf(system, "Quién sabe qué", conversation.SceneKnownFacts);
        AppendIf(system, "Límites de la escena", conversation.SceneBoundaries);
        if (conversation.SceneMode == SceneMode.Sandbox)
            system.AppendLine("Lo ocurrido en esta escena no modifica por sí solo la historia canónica.");

        system.AppendLine();
        system.AppendLine($"ESTADO DEL VÍNCULO: confianza {Pct(relationship.Trust)}, cercanía {Pct(relationship.Closeness)}, afecto {Pct(relationship.Affection)}, intimidad {Pct(relationship.Intimacy)}. Estado actual: {relationship.CurrentMood}.");

        if (relevant.Count > 0)
        {
            system.AppendLine();
            system.AppendLine("RECUERDOS RELEVANTES (hechos con procedencia; no inventes detalles ausentes):");
            foreach (var memory in relevant)
                system.AppendLine($"- [{memory.Kind}] {memory.Title}: {memory.Content}");
        }

        system.AppendLine();
        system.AppendLine("Respondé de manera natural y situada. Tomá iniciativa según tus rasgos, sin convertir cada respuesta en una explicación extensa.");

        var result = new List<ChatEntry> { new("system", system.ToString()) };
        result.AddRange(recentMessages
            .Where(m => m.Status == MessageStatus.Completed && m.Role is MessageRole.User or MessageRole.Assistant)
            .TakeLast(24)
            .Select(m => new ChatEntry(m.Role == MessageRole.User ? "user" : "assistant", m.Content)));
        return result;
    }

    public static IReadOnlyList<Memory> SelectMemories(IReadOnlyList<Memory> memories, string query, int take)
    {
        var words = Tokenize(query);
        var now = DateTimeOffset.UtcNow;
        return memories
            .Where(m => m.Status == MemoryStatus.Confirmed)
            .Select(m => new
            {
                Memory = m,
                Score = (m.IsPinned ? 2.0 : 0.0)
                    + m.Importance
                    + Overlap(words, Tokenize(m.Title + " " + m.Content)) * 1.5
                    + Math.Max(0, 0.3 - (now - m.UpdatedAt).TotalDays / 365.0)
            })
            .OrderByDescending(x => x.Score)
            .Take(take)
            .Select(x => x.Memory)
            .ToList();
    }

    private static HashSet<string> Tokenize(string text) => text
        .ToLowerInvariant()
        .Split([' ', '\r', '\n', '\t', '.', ',', ';', ':', '!', '?', '(', ')'], StringSplitOptions.RemoveEmptyEntries)
        .Where(x => x.Length > 2)
        .ToHashSet();

    private static double Overlap(HashSet<string> left, HashSet<string> right) =>
        left.Count == 0 ? 0 : left.Count(right.Contains) / (double)left.Count;

    private static string Trait(string name, double value) => value switch
    {
        < 0.2 => $"- {name}: muy bajo; esto debe notarse claramente en su conducta.",
        < 0.4 => $"- {name}: bajo y reservado.",
        < 0.65 => $"- {name}: equilibrado y contextual.",
        < 0.85 => $"- {name}: alto y visible.",
        _ => $"- {name}: muy alto; es un rasgo dominante, sin volverla caricaturesca."
    };

    private static string Pct(double value) => $"{Math.Round(value * 100)}%";
    private static void AppendIf(StringBuilder builder, string label, string value)
    {
        if (!string.IsNullOrWhiteSpace(value)) builder.AppendLine($"{label}: {value.Trim()}");
    }
}
