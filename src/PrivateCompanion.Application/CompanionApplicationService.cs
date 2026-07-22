using System.Runtime.CompilerServices;
using System.Text;
using PrivateCompanion.Domain;

namespace PrivateCompanion.Application;

public sealed class CompanionApplicationService
{
    private readonly ICompanionRepository _repository;
    private readonly ITextGenerationGateway _textGateway;
    private readonly IPromptComposer _promptComposer;

    public CompanionApplicationService(ICompanionRepository repository, ITextGenerationGateway textGateway, IPromptComposer promptComposer)
    {
        _repository = repository;
        _textGateway = textGateway;
        _promptComposer = promptComposer;
    }

    public ICompanionRepository Repository => _repository;

    public async Task<Conversation> CreateConversationAsync(
        Guid characterId,
        Guid userPersonaId,
        string? title = null,
        SceneMode mode = SceneMode.Canonical,
        CancellationToken cancellationToken = default)
    {
        var conversation = new Conversation
        {
            CharacterId = characterId,
            UserPersonaId = userPersonaId,
            Title = string.IsNullOrWhiteSpace(title) ? "Nueva conversación" : title.Trim(),
            SceneMode = mode
        };
        await _repository.SaveConversationAsync(conversation, cancellationToken);
        return conversation;
    }

    public async IAsyncEnumerable<string> SendAsync(
        Guid characterId,
        Guid conversationId,
        string userText,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userText)) yield break;

        var character = await _repository.GetCharacterAsync(characterId, cancellationToken)
            ?? throw new InvalidOperationException("El personaje ya no existe.");
        var conversation = await _repository.GetConversationAsync(conversationId, cancellationToken)
            ?? throw new InvalidOperationException("La conversación ya no existe.");
        if (conversation.CharacterId != characterId) throw new InvalidOperationException("La conversación no pertenece al personaje seleccionado.");
        var userPersona = await _repository.GetUserPersonaAsync(conversation.UserPersonaId, cancellationToken)
            ?? throw new InvalidOperationException("La identidad de usuario de esta conversación ya no existe.");

        var existing = await _repository.GetMessagesAsync(conversationId, 80, cancellationToken);
        var userMessage = new Message
        {
            ConversationId = conversationId,
            Role = MessageRole.User,
            Status = MessageStatus.Completed,
            Content = userText.Trim(),
            Sequence = existing.Count == 0 ? 1 : existing.Max(x => x.Sequence) + 1
        };
        await _repository.AddMessageAsync(userMessage, cancellationToken);

        if (existing.Count == 0)
        {
            conversation.Title = userText.Trim().Length <= 48 ? userText.Trim() : userText.Trim()[..48] + "…";
            conversation.UpdatedAt = DateTimeOffset.UtcNow;
            await _repository.SaveConversationAsync(conversation, cancellationToken);
        }

        var allMessages = existing.Append(userMessage).ToList();
        var memories = await _repository.GetMemoriesAsync(characterId, userPersona.Id, cancellationToken);
        var relationship = await _repository.GetRelationshipAsync(characterId, userPersona.Id, cancellationToken);
        var universe = character.UniverseId is Guid universeId
            ? await _repository.GetUniverseAsync(universeId, cancellationToken)
            : null;
        var provider = await _repository.GetActiveProviderAsync(cancellationToken);
        var prompt = _promptComposer.Compose(universe, character, userPersona, conversation, relationship, memories, allMessages);

        var response = new StringBuilder();
        var assistantMessage = new Message
        {
            ConversationId = conversationId,
            Role = MessageRole.Assistant,
            Status = MessageStatus.Streaming,
            Sequence = userMessage.Sequence + 1
        };
        await _repository.AddMessageAsync(assistantMessage, cancellationToken);
        try
        {
            await foreach (var chunk in _textGateway.StreamAsync(new TextGenerationRequest(provider, prompt), cancellationToken))
            {
                response.Append(chunk);
                yield return chunk;
            }
            assistantMessage.Status = MessageStatus.Completed;
        }
        finally
        {
            assistantMessage.Content = response.ToString().Trim();
            if (assistantMessage.Status != MessageStatus.Completed)
                assistantMessage.Status = cancellationToken.IsCancellationRequested ? MessageStatus.Cancelled : MessageStatus.Failed;
            await _repository.SaveMessageAsync(assistantMessage, CancellationToken.None);
        }
    }

    public Task<ProviderProbe> ProbeProviderAsync(bool allowAutoStart, CancellationToken cancellationToken = default) =>
        ProbeInternalAsync(allowAutoStart, cancellationToken);

    private async Task<ProviderProbe> ProbeInternalAsync(bool allowAutoStart, CancellationToken cancellationToken)
    {
        var provider = await _repository.GetActiveProviderAsync(cancellationToken);
        return await _textGateway.ProbeAsync(provider, allowAutoStart, cancellationToken);
    }

    public async Task<DiaryEntry> CloseSessionAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        var conversation = await _repository.GetConversationAsync(conversationId, cancellationToken)
            ?? throw new InvalidOperationException("La conversación ya no existe.");
        var character = await _repository.GetCharacterAsync(conversation.CharacterId, cancellationToken)
            ?? throw new InvalidOperationException("El personaje ya no existe.");
        var userPersona = await _repository.GetUserPersonaAsync(conversation.UserPersonaId, cancellationToken)
            ?? throw new InvalidOperationException("La identidad de usuario de esta conversación ya no existe.");
        var messages = await _repository.GetMessagesAsync(conversationId, 120, cancellationToken);
        if (messages.Count < 2) throw new InvalidOperationException("Todavía no hay suficiente conversación para cerrar la sesión.");

        var transcript = string.Join("\n", messages.TakeLast(50).Select(m => $"{m.Role}: {m.Content}"));
        var provider = await _repository.GetActiveProviderAsync(cancellationToken);
        var summaryRequest = new TextGenerationRequest(provider,
        [
            new ChatEntry("system", $"Sos el sistema privado de memoria de {character.Name}. La persona interlocutora es {userPersona.Name}. Resumí la sesión en primera persona desde la perspectiva de {character.Name}, en 90 a 160 palabras. Conservá hechos, emociones y cambios en esa relación específica. No inventes ni mezcles otras identidades. Devolvé solamente el texto del diario."),
            new ChatEntry("user", transcript)
        ], Temperature: 0.35, MaxOutputTokens: 260);

        var summary = new StringBuilder();
        await foreach (var chunk in _textGateway.StreamAsync(summaryRequest, cancellationToken)) summary.Append(chunk);
        var content = summary.ToString().Trim();
        if (string.IsNullOrWhiteSpace(content)) content = $"Hoy compartimos una conversación sobre {conversation.Title}.";

        var diary = new DiaryEntry
        {
            CharacterId = character.Id,
            UserPersonaId = userPersona.Id,
            ConversationId = conversation.Id,
            Title = $"Sesión del {DateTime.Now:dd/MM/yyyy HH:mm}",
            Content = content
        };
        await _repository.AddDiaryEntryAsync(diary, cancellationToken);

        if (conversation.SceneMode == SceneMode.Canonical)
        {
            await _repository.SaveMemoryAsync(new Memory
            {
                CharacterId = character.Id,
                UserPersonaId = userPersona.Id,
                SourceConversationId = conversation.Id,
                Kind = MemoryKind.Episodic,
                Title = conversation.Title,
                Content = content,
                Importance = 0.70,
                Confidence = 0.85,
                IsCanonical = true,
                HappenedAt = DateTimeOffset.UtcNow
            }, cancellationToken);

            var relationship = await _repository.GetRelationshipAsync(character.Id, userPersona.Id, cancellationToken);
            var turns = messages.Count(m => m.Role == MessageRole.User);
            relationship.Trust = Clamp(relationship.Trust + Math.Min(0.04, turns * 0.004));
            relationship.Closeness = Clamp(relationship.Closeness + Math.Min(0.05, turns * 0.005));
            relationship.Affection = Clamp(relationship.Affection + Math.Min(0.04, turns * 0.004));
            relationship.Intimacy = Clamp(relationship.Intimacy + Math.Min(0.035, turns * 0.0035));
            relationship.CurrentMood = "Con continuidad fresca de la última sesión";
            relationship.UpdatedAt = DateTimeOffset.UtcNow;
            await _repository.SaveRelationshipAsync(relationship, cancellationToken);
        }

        conversation.ClosedAt = DateTimeOffset.UtcNow;
        conversation.UpdatedAt = DateTimeOffset.UtcNow;
        await _repository.SaveConversationAsync(conversation, cancellationToken);
        return diary;
    }

    private static double Clamp(double value) => Math.Max(0, Math.Min(1, value));
}
