using PrivateCompanion.Domain;

namespace PrivateCompanion.Application;

public sealed record ChatEntry(string Role, string Content);
public sealed record TextGenerationRequest(
    ProviderProfile Provider,
    IReadOnlyList<ChatEntry> Messages,
    double? Temperature = null,
    int? MaxOutputTokens = null);

public sealed record ProviderProbe(bool IsAvailable, string Message, IReadOnlyList<string> Models);

public interface ITextGenerationGateway
{
    IAsyncEnumerable<string> StreamAsync(TextGenerationRequest request, CancellationToken cancellationToken = default);
    Task<ProviderProbe> ProbeAsync(ProviderProfile provider, bool allowAutoStart, CancellationToken cancellationToken = default);
}

public interface ICompanionRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StoryUniverse>> GetUniversesAsync(CancellationToken cancellationToken = default);
    Task<StoryUniverse?> GetUniverseAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveUniverseAsync(StoryUniverse universe, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Character>> GetCharactersAsync(CancellationToken cancellationToken = default);
    Task<Character?> GetCharacterAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveCharacterAsync(Character character, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserPersona>> GetUserPersonasAsync(CancellationToken cancellationToken = default);
    Task<UserPersona?> GetUserPersonaAsync(Guid id, CancellationToken cancellationToken = default);
    Task<UserPersona> GetActiveUserPersonaAsync(CancellationToken cancellationToken = default);
    Task SaveUserPersonaAsync(UserPersona userPersona, CancellationToken cancellationToken = default);
    Task SetActiveUserPersonaAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Conversation>> GetConversationsAsync(Guid characterId, Guid userPersonaId, CancellationToken cancellationToken = default);
    Task<Conversation?> GetConversationAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveConversationAsync(Conversation conversation, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Message>> GetMessagesAsync(Guid conversationId, int take = 80, CancellationToken cancellationToken = default);
    Task AddMessageAsync(Message message, CancellationToken cancellationToken = default);
    Task SaveMessageAsync(Message message, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Memory>> GetMemoriesAsync(Guid characterId, Guid userPersonaId, CancellationToken cancellationToken = default);
    Task SaveMemoryAsync(Memory memory, CancellationToken cancellationToken = default);
    Task DeleteMemoryAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DiaryEntry>> GetDiaryAsync(Guid characterId, Guid userPersonaId, CancellationToken cancellationToken = default);
    Task AddDiaryEntryAsync(DiaryEntry entry, CancellationToken cancellationToken = default);

    Task<RelationshipState> GetRelationshipAsync(Guid characterId, Guid userPersonaId, CancellationToken cancellationToken = default);
    Task SaveRelationshipAsync(RelationshipState relationship, CancellationToken cancellationToken = default);

    Task<ProviderProfile> GetActiveProviderAsync(CancellationToken cancellationToken = default);
    Task SaveProviderAsync(ProviderProfile provider, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MediaAsset>> GetMediaAsync(Guid characterId, CancellationToken cancellationToken = default);
    Task SaveMediaAsync(MediaAsset media, CancellationToken cancellationToken = default);
    Task<CharacterMediaPresentation> GetMediaPresentationAsync(Guid characterId, CancellationToken cancellationToken = default);
    Task SaveMediaPresentationAsync(CharacterMediaPresentation presentation, CancellationToken cancellationToken = default);
}

public sealed record MediaImportResult(int Imported, int Skipped, IReadOnlyList<string> Errors);

public interface IMediaLibrary
{
    Task<MediaImportResult> ImportAsync(
        Guid characterId,
        IReadOnlyList<string> sourcePaths,
        CancellationToken cancellationToken = default);

    Task<string> ImportUserPortraitAsync(
        string sourcePath,
        CancellationToken cancellationToken = default);

    string GetAbsolutePath(MediaAsset asset);
    string GetAbsolutePath(string relativePath);
}

public interface IPromptComposer
{
    IReadOnlyList<ChatEntry> Compose(
        StoryUniverse? universe,
        Character character,
        UserPersona userPersona,
        Conversation conversation,
        RelationshipState relationship,
        IReadOnlyList<Memory> memories,
        IReadOnlyList<Message> recentMessages);
}
