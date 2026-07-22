using System.ComponentModel.DataAnnotations;

namespace PrivateCompanion.Domain;

public enum MessageRole { System, User, Assistant }
public enum MessageStatus { Pending, Streaming, Completed, Cancelled, Failed }
public enum SceneMode { Canonical, Sandbox }
public enum MemoryKind { Episodic, Personal, Emotional, Identity }
public enum MemoryStatus { Candidate, Confirmed, Rejected, Superseded }
public enum ProviderKind { LMStudio, Ollama, LlamaCpp, OpenAICompatible }

public sealed class StoryUniverse
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(160)] public string Name { get; set; } = "Universo principal";
    [MaxLength(4000)] public string Summary { get; set; } = string.Empty;
    [MaxLength(30000)] public string Canon { get; set; } = string.Empty;
    [MaxLength(12000)] public string Rules { get; set; } = string.Empty;
    [MaxLength(1000)] public string Tone { get; set; } = string.Empty;
    [MaxLength(1000)] public string Source { get; set; } = "Creado en Tinta Viva";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name)) throw new ValidationException("El universo necesita un nombre.");
    }
}

public sealed class Character
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UniverseId { get; set; }
    [MaxLength(120)] public string Name { get; set; } = string.Empty;
    public int Age { get; set; } = 25;
    [MaxLength(4000)] public string Appearance { get; set; } = string.Empty;
    [MaxLength(8000)] public string Backstory { get; set; } = string.Empty;
    [MaxLength(2000)] public string PersonalityNotes { get; set; } = string.Empty;
    [MaxLength(500)] public string Profession { get; set; } = string.Empty;
    [MaxLength(2000)] public string Likes { get; set; } = string.Empty;
    [MaxLength(2000)] public string SpeechStyle { get; set; } = string.Empty;
    [MaxLength(2000)] public string Goals { get; set; } = string.Empty;
    [MaxLength(1000)] public string AffectiveStyle { get; set; } = string.Empty;
    [MaxLength(1000)] public string RomanticStyle { get; set; } = string.Empty;
    [MaxLength(500)] public string HumorStyle { get; set; } = string.Empty;
    [MaxLength(50)] public string Language { get; set; } = "Español rioplatense";
    public double Romanticism { get; set; } = 0.75;
    public double Extroversion { get; set; } = 0.55;
    public double Humor { get; set; } = 0.60;
    public double Calm { get; set; } = 0.65;
    public double Confidence { get; set; } = 0.70;
    public double Affection { get; set; } = 0.80;
    public double Independence { get; set; } = 0.55;
    public double Initiative { get; set; } = 0.70;
    public double Formality { get; set; } = 0.20;
    public double EmotionalIntensity { get; set; } = 0.75;
    public int Revision { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name)) throw new ValidationException("El personaje necesita un nombre.");
        if (Age < 18) throw new ValidationException("La edad mínima es 18 años.");
        foreach (var value in TraitValues())
            if (value is < 0 or > 1) throw new ValidationException("Los rasgos deben estar entre 0 y 1.");
    }

    public IEnumerable<double> TraitValues()
    {
        yield return Romanticism; yield return Extroversion; yield return Humor; yield return Calm;
        yield return Confidence; yield return Affection; yield return Independence; yield return Initiative;
        yield return Formality; yield return EmotionalIntensity;
    }
}

public sealed class UserPersona
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(120)] public string Name { get; set; } = string.Empty;
    public int Age { get; set; } = 25;
    [MaxLength(120)] public string Pronouns { get; set; } = string.Empty;
    [MaxLength(2000)] public string Appearance { get; set; } = string.Empty;
    [MaxLength(4000)] public string About { get; set; } = string.Empty;
    [MaxLength(500)] public string Profession { get; set; } = string.Empty;
    [MaxLength(2000)] public string Likes { get; set; } = string.Empty;
    [MaxLength(1500)] public string CommunicationStyle { get; set; } = string.Empty;
    [MaxLength(1000)] public string PortraitRelativePath { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name)) throw new ValidationException("Tu identidad necesita un nombre.");
        if (Age < 18) throw new ValidationException("La edad mínima es 18 años.");
    }
}

public sealed class CharacterRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CharacterId { get; set; }
    public int Revision { get; set; }
    public string SnapshotJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Conversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CharacterId { get; set; }
    public Guid UserPersonaId { get; set; }
    [MaxLength(200)] public string Title { get; set; } = "Nueva conversación";
    [MaxLength(200)] public string SceneTitle { get; set; } = "Encuentro cotidiano";
    [MaxLength(6000)] public string ScenePremise { get; set; } = string.Empty;
    public SceneMode SceneMode { get; set; } = SceneMode.Canonical;
    [MaxLength(500)] public string ScenePlace { get; set; } = string.Empty;
    [MaxLength(300)] public string SceneTime { get; set; } = string.Empty;
    [MaxLength(1000)] public string SceneAtmosphere { get; set; } = string.Empty;
    [MaxLength(1000)] public string SceneParticipants { get; set; } = string.Empty;
    [MaxLength(2000)] public string SceneIntent { get; set; } = string.Empty;
    [MaxLength(3000)] public string SceneKnownFacts { get; set; } = string.Empty;
    [MaxLength(2000)] public string SceneBoundaries { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ClosedAt { get; set; }
}

public sealed class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public MessageRole Role { get; set; }
    public MessageStatus Status { get; set; } = MessageStatus.Completed;
    public string Content { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Memory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CharacterId { get; set; }
    public Guid UserPersonaId { get; set; }
    public Guid? SourceConversationId { get; set; }
    public MemoryKind Kind { get; set; }
    public MemoryStatus Status { get; set; } = MemoryStatus.Confirmed;
    [MaxLength(300)] public string Title { get; set; } = string.Empty;
    [MaxLength(8000)] public string Content { get; set; } = string.Empty;
    public double Importance { get; set; } = 0.5;
    public double Confidence { get; set; } = 1.0;
    public bool IsPinned { get; set; }
    public bool IsCanonical { get; set; } = true;
    public DateTimeOffset? HappenedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class DiaryEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CharacterId { get; set; }
    public Guid UserPersonaId { get; set; }
    public Guid ConversationId { get; set; }
    [MaxLength(300)] public string Title { get; set; } = string.Empty;
    [MaxLength(8000)] public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class RelationshipState
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CharacterId { get; set; }
    public Guid UserPersonaId { get; set; }
    public double Trust { get; set; } = 0.35;
    public double Closeness { get; set; } = 0.30;
    public double Affection { get; set; } = 0.35;
    public double Intimacy { get; set; } = 0.20;
    public double Tension { get; set; } = 0.10;
    [MaxLength(300)] public string CurrentMood { get; set; } = "Curiosidad y calidez";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ProviderProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(100)] public string Name { get; set; } = "Motor local";
    public ProviderKind Kind { get; set; } = ProviderKind.LMStudio;
    [MaxLength(500)] public string BaseUrl { get; set; } = "http://127.0.0.1:1234/v1";
    [MaxLength(300)] public string Model { get; set; } = string.Empty;
    public double Temperature { get; set; } = 0.85;
    public double TopP { get; set; } = 0.95;
    public int MaxOutputTokens { get; set; } = 512;
    public int ContextBudget { get; set; } = 4096;
    public bool AutoStart { get; set; }
    [MaxLength(2000)] public string LaunchCommand { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class MediaAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CharacterId { get; set; }
    [MaxLength(1000)] public string RelativePath { get; set; } = string.Empty;
    [MaxLength(128)] public string Sha256 { get; set; } = string.Empty;
    [MaxLength(8000)] public string Prompt { get; set; } = string.Empty;
    [MaxLength(500)] public string WorkflowName { get; set; } = string.Empty;
    public long Seed { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CharacterMediaPresentation
{
    public Guid CharacterId { get; set; }
    public Guid? PortraitMediaAssetId { get; set; }
    public Guid? BackgroundMediaAssetId { get; set; }
    public bool BackgroundEnabled { get; set; } = true;
    public double PortraitZoom { get; set; } = 1.0;
    public double PortraitFocusX { get; set; } = 0.5;
    public double PortraitFocusY { get; set; } = 0.5;
    public double BackgroundZoom { get; set; } = 1.0;
    public double BackgroundFocusX { get; set; } = 0.5;
    public double BackgroundFocusY { get; set; } = 0.5;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
