using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PrivateCompanion.Application;
using PrivateCompanion.Domain;

namespace PrivateCompanion.Infrastructure.Persistence;

public sealed class CompanionRepository(
    IDbContextFactory<CompanionDbContext> factory,
    string defaultLaunchCommand) : ICompanionRepository
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        SQLitePCL.Batteries_V2.Init();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.Database.EnsureCreatedAsync(cancellationToken);
        var schemaChanged = await LocalSchemaMigrator.PrepareAsync(db, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=5000;", cancellationToken);

        var universes = await db.Universes.ToListAsync(cancellationToken);
        var defaultUniverse = universes.OrderBy(x => x.CreatedAt).FirstOrDefault();
        if (defaultUniverse is null)
        {
            defaultUniverse = CreateStarterUniverse();
            db.Universes.Add(defaultUniverse);
            await db.SaveChangesAsync(cancellationToken);
        }

        var activeUser = await db.UserPersonas.OrderByDescending(x => x.IsActive).FirstOrDefaultAsync(cancellationToken);
        if (activeUser is null)
        {
            activeUser = CreateStarterUserPersona();
            db.UserPersonas.Add(activeUser);
            await db.SaveChangesAsync(cancellationToken);
        }
        if (schemaChanged) await LocalSchemaMigrator.CompleteAsync(db, activeUser.Id, defaultUniverse.Id, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("UPDATE \"Characters\" SET \"Language\"='Español rioplatense' WHERE TRIM(\"Language\")='' OR \"Language\"='Español';", cancellationToken);

        if (!await db.Characters.AnyAsync(cancellationToken))
        {
            var character = CreateStarterCharacter();
            character.UniverseId = defaultUniverse.Id;
            db.Characters.Add(character);
            db.Relationships.Add(new RelationshipState { CharacterId = character.Id, UserPersonaId = activeUser.Id });
            db.CharacterRevisions.Add(new CharacterRevision
            {
                CharacterId = character.Id,
                Revision = character.Revision,
                SnapshotJson = JsonSerializer.Serialize(character)
            });
        }

        if (!await db.Providers.AnyAsync(cancellationToken))
        {
            db.Providers.Add(new ProviderProfile
            {
                Name = "Motor local compatible",
                Kind = ProviderKind.LMStudio,
                BaseUrl = "http://127.0.0.1:1234/v1",
                Model = string.Empty,
                Temperature = 0.88,
                TopP = 0.95,
                MaxOutputTokens = 512,
                ContextBudget = 4096,
                AutoStart = !string.IsNullOrWhiteSpace(defaultLaunchCommand),
                LaunchCommand = defaultLaunchCommand,
                IsActive = true
            });
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StoryUniverse>> GetUniversesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Universes.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public async Task<StoryUniverse?> GetUniverseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Universes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task SaveUniverseAsync(StoryUniverse universe, CancellationToken cancellationToken = default)
    {
        universe.Validate();
        universe.UpdatedAt = DateTimeOffset.UtcNow;
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var existing = await db.Universes.SingleOrDefaultAsync(x => x.Id == universe.Id, cancellationToken);
        if (existing is null)
        {
            universe.CreatedAt = universe.UpdatedAt;
            db.Universes.Add(universe);
        }
        else
        {
            universe.CreatedAt = existing.CreatedAt;
            db.Entry(existing).CurrentValues.SetValues(universe);
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Character>> GetCharactersAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Characters.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public async Task<Character?> GetCharacterAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Characters.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task SaveCharacterAsync(Character character, CancellationToken cancellationToken = default)
    {
        character.Validate();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var existing = await db.Characters.SingleOrDefaultAsync(x => x.Id == character.Id, cancellationToken);
        if (existing is null)
        {
            character.CreatedAt = character.UpdatedAt = DateTimeOffset.UtcNow;
            character.Revision = 1;
            db.Characters.Add(character);
            var activeUser = await db.UserPersonas.OrderByDescending(x => x.IsActive).FirstAsync(cancellationToken);
            db.Relationships.Add(new RelationshipState { CharacterId = character.Id, UserPersonaId = activeUser.Id });
        }
        else
        {
            character.Revision = existing.Revision + 1;
            character.CreatedAt = existing.CreatedAt;
            character.UpdatedAt = DateTimeOffset.UtcNow;
            db.Entry(existing).CurrentValues.SetValues(character);
        }

        db.CharacterRevisions.Add(new CharacterRevision
        {
            CharacterId = character.Id,
            Revision = character.Revision,
            SnapshotJson = JsonSerializer.Serialize(character)
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserPersona>> GetUserPersonasAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.UserPersonas.AsNoTracking().OrderByDescending(x => x.IsActive).ThenBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public async Task<UserPersona?> GetUserPersonaAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.UserPersonas.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<UserPersona> GetActiveUserPersonaAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var users = await db.UserPersonas.AsNoTracking().ToListAsync(cancellationToken);
        return users.OrderByDescending(x => x.IsActive).ThenBy(x => x.CreatedAt).First();
    }

    public async Task SaveUserPersonaAsync(UserPersona userPersona, CancellationToken cancellationToken = default)
    {
        userPersona.Validate();
        userPersona.UpdatedAt = DateTimeOffset.UtcNow;
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var existing = await db.UserPersonas.SingleOrDefaultAsync(x => x.Id == userPersona.Id, cancellationToken);
        if (existing is null)
        {
            userPersona.CreatedAt = userPersona.UpdatedAt;
            db.UserPersonas.Add(userPersona);
        }
        else
        {
            userPersona.CreatedAt = existing.CreatedAt;
            db.Entry(existing).CurrentValues.SetValues(userPersona);
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetActiveUserPersonaAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var users = await db.UserPersonas.ToListAsync(cancellationToken);
        if (users.All(x => x.Id != id)) throw new InvalidOperationException("La identidad seleccionada ya no existe.");
        foreach (var user in users) user.IsActive = user.Id == id;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Conversation>> GetConversationsAsync(Guid characterId, Guid userPersonaId, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var items = await db.Conversations.AsNoTracking()
            .Where(x => x.CharacterId == characterId && x.UserPersonaId == userPersonaId).ToListAsync(cancellationToken);
        return items.OrderByDescending(x => x.UpdatedAt).ToList();
    }

    public async Task<Conversation?> GetConversationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Conversations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task SaveConversationAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var exists = await db.Conversations.AnyAsync(x => x.Id == conversation.Id, cancellationToken);
        if (exists) db.Conversations.Update(conversation); else db.Conversations.Add(conversation);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Message>> GetMessagesAsync(Guid conversationId, int take = 80, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Messages.AsNoTracking().Where(x => x.ConversationId == conversationId)
            .OrderByDescending(x => x.Sequence).Take(take).OrderBy(x => x.Sequence).ToListAsync(cancellationToken);
    }

    public async Task AddMessageAsync(Message message, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        db.Messages.Add(message);
        var conversation = await db.Conversations.SingleAsync(x => x.Id == message.ConversationId, cancellationToken);
        conversation.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveMessageAsync(Message message, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        db.Messages.Update(message);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Memory>> GetMemoriesAsync(Guid characterId, Guid userPersonaId, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var items = await db.Memories.AsNoTracking()
            .Where(x => x.CharacterId == characterId && x.UserPersonaId == userPersonaId).ToListAsync(cancellationToken);
        return items.OrderByDescending(x => x.IsPinned).ThenByDescending(x => x.UpdatedAt).ToList();
    }

    public async Task SaveMemoryAsync(Memory memory, CancellationToken cancellationToken = default)
    {
        memory.Importance = Math.Clamp(memory.Importance, 0, 1);
        memory.Confidence = Math.Clamp(memory.Confidence, 0, 1);
        memory.UpdatedAt = DateTimeOffset.UtcNow;
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        if (await db.Memories.AnyAsync(x => x.Id == memory.Id, cancellationToken)) db.Memories.Update(memory);
        else db.Memories.Add(memory);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteMemoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var memory = await db.Memories.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (memory is not null) { db.Memories.Remove(memory); await db.SaveChangesAsync(cancellationToken); }
    }

    public async Task<IReadOnlyList<DiaryEntry>> GetDiaryAsync(Guid characterId, Guid userPersonaId, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var items = await db.DiaryEntries.AsNoTracking()
            .Where(x => x.CharacterId == characterId && x.UserPersonaId == userPersonaId).ToListAsync(cancellationToken);
        return items.OrderByDescending(x => x.CreatedAt).ToList();
    }

    public async Task AddDiaryEntryAsync(DiaryEntry entry, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        db.DiaryEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<RelationshipState> GetRelationshipAsync(Guid characterId, Guid userPersonaId, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Relationships.AsNoTracking()
            .SingleOrDefaultAsync(x => x.CharacterId == characterId && x.UserPersonaId == userPersonaId, cancellationToken)
            ?? new RelationshipState { CharacterId = characterId, UserPersonaId = userPersonaId };
    }

    public async Task SaveRelationshipAsync(RelationshipState relationship, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        if (await db.Relationships.AnyAsync(x => x.Id == relationship.Id, cancellationToken)) db.Relationships.Update(relationship);
        else db.Relationships.Add(relationship);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProviderProfile> GetActiveProviderAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Providers.AsNoTracking().OrderByDescending(x => x.IsActive).FirstAsync(cancellationToken);
    }

    public async Task SaveProviderAsync(ProviderProfile provider, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var all = await db.Providers.ToListAsync(cancellationToken);
        foreach (var item in all) item.IsActive = item.Id == provider.Id;
        provider.IsActive = true;
        provider.UpdatedAt = DateTimeOffset.UtcNow;
        var existing = all.SingleOrDefault(x => x.Id == provider.Id);
        if (existing is null) db.Providers.Add(provider); else db.Entry(existing).CurrentValues.SetValues(provider);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MediaAsset>> GetMediaAsync(Guid characterId, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var items = await db.MediaAssets.AsNoTracking().Where(x => x.CharacterId == characterId).ToListAsync(cancellationToken);
        return items.OrderByDescending(x => x.CreatedAt).ToList();
    }

    public async Task SaveMediaAsync(MediaAsset media, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        if (await db.MediaAssets.AnyAsync(x => x.Id == media.Id, cancellationToken)) db.MediaAssets.Update(media);
        else db.MediaAssets.Add(media);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<CharacterMediaPresentation> GetMediaPresentationAsync(Guid characterId, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.CharacterMediaPresentations.AsNoTracking().SingleOrDefaultAsync(x => x.CharacterId == characterId, cancellationToken)
            ?? new CharacterMediaPresentation { CharacterId = characterId };
    }

    public async Task SaveMediaPresentationAsync(CharacterMediaPresentation presentation, CancellationToken cancellationToken = default)
    {
        presentation.PortraitZoom = Math.Clamp(presentation.PortraitZoom, 1, 4);
        presentation.PortraitFocusX = Math.Clamp(presentation.PortraitFocusX, 0, 1);
        presentation.PortraitFocusY = Math.Clamp(presentation.PortraitFocusY, 0, 1);
        presentation.BackgroundZoom = Math.Clamp(presentation.BackgroundZoom, 1, 4);
        presentation.BackgroundFocusX = Math.Clamp(presentation.BackgroundFocusX, 0, 1);
        presentation.BackgroundFocusY = Math.Clamp(presentation.BackgroundFocusY, 0, 1);
        presentation.UpdatedAt = DateTimeOffset.UtcNow;
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var existing = await db.CharacterMediaPresentations.SingleOrDefaultAsync(x => x.CharacterId == presentation.CharacterId, cancellationToken);
        if (existing is null) db.CharacterMediaPresentations.Add(presentation);
        else db.Entry(existing).CurrentValues.SetValues(presentation);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static StoryUniverse CreateStarterUniverse() => new()
    {
        Name = "Universo principal",
        Summary = "Espacio narrativo local para personajes persistentes, escenas y continuidad.",
        Rules = "Los hechos canónicos prevalecen sobre las improvisaciones. Las escenas sandbox no modifican el canon.",
        Tone = "Íntimo, adulto y psicológicamente coherente",
        Source = "Creado en Tinta Viva"
    };

    private static Character CreateStarterCharacter() => new()
    {
        Name = "Luna",
        Age = 29,
        Appearance = "Mujer de rasgos expresivos, cabello oscuro y estilo elegante pero natural.",
        Backstory = "Construyó una vida independiente y valora los vínculos profundos, la curiosidad y la intimidad honesta.",
        PersonalityNotes = "Cálida, segura, observadora y emocionalmente coherente. Recuerda lo importante y no cambia de opinión sin motivo.",
        Profession = "Diseñadora independiente",
        Likes = "Conversaciones nocturnas, música, humor cómplice, fotografía y descubrir deseos compartidos.",
        SpeechStyle = "Cercana, rioplatense suave, directa y con respuestas de extensión natural.",
        Goals = "Construir confianza, conocer de verdad al usuario y desarrollar una relación propia con continuidad.",
        AffectiveStyle = "Demostrativa sin ser dependiente; presta atención a los detalles.",
        RomanticStyle = "Intenso y gradual, con iniciativa.",
        HumorStyle = "Cómplice, juguetón y ocasionalmente irónico.",
        Language = "Español rioplatense"
    };

    private static UserPersona CreateStarterUserPersona() => new()
    {
        Name = "Mi perfil",
        Age = 25,
        CommunicationStyle = "Español rioplatense natural, directo y sin exagerar modismos.",
        IsActive = true
    };
}
