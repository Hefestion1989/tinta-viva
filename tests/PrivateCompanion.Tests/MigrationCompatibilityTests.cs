using Microsoft.Data.Sqlite;
using PrivateCompanion.Domain;
using PrivateCompanion.Infrastructure.Persistence;

namespace PrivateCompanion.Tests;

public sealed class MigrationCompatibilityTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "PrivateCompanion.MigrationTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Upgrades_v02_data_and_assigns_it_to_the_initial_user_without_losing_history()
    {
        SQLitePCL.Batteries_V2.Init();
        Directory.CreateDirectory(_directory);
        var databasePath = Path.Combine(_directory, "legacy.db");
        var characterId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var relationshipId = Guid.NewGuid();
        await using (var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False"))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = LegacySchema + $"""
                INSERT INTO Characters VALUES ('{characterId.ToString().ToUpperInvariant()}', 'Luna', 29, '', '', '', '', '', '', '', '', '', '', 'Español', .7, .5, .5, .5, .6, .7, .5, .6, .3, .7, 1, '2026-01-01', '2026-01-01');
                INSERT INTO Providers VALUES ('{Guid.NewGuid().ToString().ToUpperInvariant()}', 'Local', 2, 'http://127.0.0.1:5014/v1', 'model', .8, .9, 512, 4096, 0, '', 1, '2026-01-01');
                INSERT INTO Conversations VALUES ('{conversationId.ToString().ToUpperInvariant()}', '{characterId.ToString().ToUpperInvariant()}', 'Historia anterior', 'Escena', '', 0, '2026-01-01', '2026-01-01', NULL);
                INSERT INTO Memories VALUES ('{Guid.NewGuid().ToString().ToUpperInvariant()}', '{characterId.ToString().ToUpperInvariant()}', '{conversationId.ToString().ToUpperInvariant()}', 0, 1, 'Recuerdo anterior', 'No debe perderse', .8, 1, 0, 1, NULL, '2026-01-01', '2026-01-01');
                INSERT INTO DiaryEntries VALUES ('{Guid.NewGuid().ToString().ToUpperInvariant()}', '{characterId.ToString().ToUpperInvariant()}', '{conversationId.ToString().ToUpperInvariant()}', 'Diario anterior', 'Persistente', '2026-01-01');
                INSERT INTO Relationships VALUES ('{relationshipId.ToString().ToUpperInvariant()}', '{characterId.ToString().ToUpperInvariant()}', .5, .4, .6, .3, .1, 'Continuidad', '2026-01-01');
                """;
            await command.ExecuteNonQueryAsync();
        }

        var repository = new CompanionRepository(new CompanionDbContextFactory(databasePath), string.Empty);
        await repository.InitializeAsync();
        var user = await repository.GetActiveUserPersonaAsync();

        Assert.Equal("Historia anterior", Assert.Single(await repository.GetConversationsAsync(characterId, user.Id)).Title);
        Assert.Equal("Recuerdo anterior", Assert.Single(await repository.GetMemoriesAsync(characterId, user.Id)).Title);
        Assert.Equal(relationshipId, (await repository.GetRelationshipAsync(characterId, user.Id)).Id);
        Assert.Equal("Español rioplatense", (await repository.GetCharacterAsync(characterId))!.Language);
        Assert.NotNull((await repository.GetCharacterAsync(characterId))!.UniverseId);
        Assert.Single(await repository.GetUniversesAsync());
        Assert.Equal(ProviderLocation.Local, (await repository.GetActiveProviderAsync()).Location);
        Assert.Single(Directory.GetFiles(Path.Combine(_directory, "backups"), "tinta-viva-pre-v7-*.db"));
    }

    [Fact]
    public async Task Upgrades_v03_media_presentation_with_non_destructive_framing_columns()
    {
        SQLitePCL.Batteries_V2.Init();
        Directory.CreateDirectory(_directory);
        var databasePath = Path.Combine(_directory, "v03.db");
        var characterId = Guid.NewGuid();
        await using (var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False"))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = LegacySchema + V03Additions + $"""
                INSERT INTO Characters VALUES ('{characterId.ToString().ToUpperInvariant()}', 'Luna', 29, '', '', '', '', '', '', '', '', '', '', 'Español rioplatense', .7, .5, .5, .5, .6, .7, .5, .6, .3, .7, 1, '2026-01-01', '2026-01-01');
                INSERT INTO Providers VALUES ('{Guid.NewGuid().ToString().ToUpperInvariant()}', 'Local', 2, 'http://127.0.0.1:5014/v1', 'model', .8, .9, 512, 4096, 0, '', 1, '2026-01-01');
                PRAGMA user_version=3;
                """;
            await command.ExecuteNonQueryAsync();
        }

        var repository = new CompanionRepository(new CompanionDbContextFactory(databasePath), string.Empty);
        await repository.InitializeAsync();
        var presentation = await repository.GetMediaPresentationAsync(characterId);
        presentation.PortraitZoom = 2.5;
        presentation.PortraitFocusX = 0.25;
        presentation.BackgroundZoom = 1.75;
        await repository.SaveMediaPresentationAsync(presentation);
        var stored = await repository.GetMediaPresentationAsync(characterId);

        Assert.Equal(2.5, stored.PortraitZoom);
        Assert.Equal(0.25, stored.PortraitFocusX);
        Assert.Equal(1.75, stored.BackgroundZoom);
        var user = await repository.GetActiveUserPersonaAsync();
        Assert.Equal(string.Empty, user.PortraitRelativePath);
        Assert.Single(Directory.GetFiles(Path.Combine(_directory, "backups"), "tinta-viva-pre-v7-*.db"));
    }

    private const string LegacySchema = """
        CREATE TABLE Characters (Id TEXT PRIMARY KEY NOT NULL, Name TEXT NOT NULL, Age INTEGER NOT NULL, Appearance TEXT NOT NULL, Backstory TEXT NOT NULL, PersonalityNotes TEXT NOT NULL, Profession TEXT NOT NULL, Likes TEXT NOT NULL, SpeechStyle TEXT NOT NULL, Goals TEXT NOT NULL, AffectiveStyle TEXT NOT NULL, RomanticStyle TEXT NOT NULL, HumorStyle TEXT NOT NULL, Language TEXT NOT NULL, Romanticism REAL NOT NULL, Extroversion REAL NOT NULL, Humor REAL NOT NULL, Calm REAL NOT NULL, Confidence REAL NOT NULL, Affection REAL NOT NULL, Independence REAL NOT NULL, Initiative REAL NOT NULL, Formality REAL NOT NULL, EmotionalIntensity REAL NOT NULL, Revision INTEGER NOT NULL, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL);
        CREATE TABLE Providers (Id TEXT PRIMARY KEY NOT NULL, Name TEXT NOT NULL, Kind INTEGER NOT NULL, BaseUrl TEXT NOT NULL, Model TEXT NOT NULL, Temperature REAL NOT NULL, TopP REAL NOT NULL, MaxOutputTokens INTEGER NOT NULL, ContextBudget INTEGER NOT NULL, AutoStart INTEGER NOT NULL, LaunchCommand TEXT NOT NULL, IsActive INTEGER NOT NULL, UpdatedAt TEXT NOT NULL);
        CREATE TABLE MediaAssets (Id TEXT PRIMARY KEY NOT NULL, CharacterId TEXT NOT NULL, RelativePath TEXT NOT NULL, Sha256 TEXT NOT NULL, Prompt TEXT NOT NULL, WorkflowName TEXT NOT NULL, Seed INTEGER NOT NULL, CreatedAt TEXT NOT NULL);
        CREATE TABLE Conversations (Id TEXT PRIMARY KEY NOT NULL, CharacterId TEXT NOT NULL, Title TEXT NOT NULL, SceneTitle TEXT NOT NULL, ScenePremise TEXT NOT NULL, SceneMode INTEGER NOT NULL, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, ClosedAt TEXT NULL);
        CREATE TABLE Memories (Id TEXT PRIMARY KEY NOT NULL, CharacterId TEXT NOT NULL, SourceConversationId TEXT NULL, Kind INTEGER NOT NULL, Status INTEGER NOT NULL, Title TEXT NOT NULL, Content TEXT NOT NULL, Importance REAL NOT NULL, Confidence REAL NOT NULL, IsPinned INTEGER NOT NULL, IsCanonical INTEGER NOT NULL, HappenedAt TEXT NULL, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL);
        CREATE TABLE DiaryEntries (Id TEXT PRIMARY KEY NOT NULL, CharacterId TEXT NOT NULL, ConversationId TEXT NOT NULL, Title TEXT NOT NULL, Content TEXT NOT NULL, CreatedAt TEXT NOT NULL);
        CREATE TABLE Relationships (Id TEXT PRIMARY KEY NOT NULL, CharacterId TEXT NOT NULL, Trust REAL NOT NULL, Closeness REAL NOT NULL, Affection REAL NOT NULL, Intimacy REAL NOT NULL, Tension REAL NOT NULL, CurrentMood TEXT NOT NULL, UpdatedAt TEXT NOT NULL);
        CREATE UNIQUE INDEX IX_Relationships_CharacterId ON Relationships (CharacterId);
        """;

    private const string V03Additions = """
        CREATE TABLE UserPersonas (Id TEXT PRIMARY KEY NOT NULL, Name TEXT NOT NULL, Age INTEGER NOT NULL, Pronouns TEXT NOT NULL, Appearance TEXT NOT NULL, About TEXT NOT NULL, Profession TEXT NOT NULL, Likes TEXT NOT NULL, CommunicationStyle TEXT NOT NULL, IsActive INTEGER NOT NULL, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL);
        CREATE TABLE CharacterMediaPresentations (CharacterId TEXT PRIMARY KEY NOT NULL, PortraitMediaAssetId TEXT NULL, BackgroundMediaAssetId TEXT NULL, BackgroundEnabled INTEGER NOT NULL DEFAULT 1, UpdatedAt TEXT NOT NULL);
        ALTER TABLE Conversations ADD COLUMN UserPersonaId TEXT NULL;
        ALTER TABLE Memories ADD COLUMN UserPersonaId TEXT NULL;
        ALTER TABLE DiaryEntries ADD COLUMN UserPersonaId TEXT NULL;
        ALTER TABLE Relationships ADD COLUMN UserPersonaId TEXT NULL;
        DROP INDEX IX_Relationships_CharacterId;
        CREATE UNIQUE INDEX IX_Relationships_CharacterId_UserPersonaId ON Relationships (CharacterId, UserPersonaId);
        """;

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
