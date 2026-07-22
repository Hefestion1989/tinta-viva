using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace PrivateCompanion.Infrastructure.Persistence;

internal static class LocalSchemaMigrator
{
    private const int CurrentVersion = 6;

    public static async Task<bool> PrepareAsync(CompanionDbContext db, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var closeWhenDone = connection.State != ConnectionState.Open;
        if (closeWhenDone) await connection.OpenAsync(cancellationToken);
        try
        {
            var version = Convert.ToInt32(await ScalarAsync(connection, "PRAGMA user_version;", cancellationToken));
            if (version >= CurrentVersion) return false;

            await CreateBackupAsync(connection, cancellationToken);
            await ExecuteAsync(connection, """
                CREATE TABLE IF NOT EXISTS "Universes" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_Universes" PRIMARY KEY,
                    "Name" TEXT NOT NULL,
                    "Summary" TEXT NOT NULL,
                    "Canon" TEXT NOT NULL,
                    "Rules" TEXT NOT NULL,
                    "Tone" TEXT NOT NULL,
                    "Source" TEXT NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS "IX_Universes_Name" ON "Universes" ("Name");

                CREATE TABLE IF NOT EXISTS "UserPersonas" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_UserPersonas" PRIMARY KEY,
                    "Name" TEXT NOT NULL,
                    "Age" INTEGER NOT NULL,
                    "Pronouns" TEXT NOT NULL,
                    "Appearance" TEXT NOT NULL,
                    "About" TEXT NOT NULL,
                    "Profession" TEXT NOT NULL,
                    "Likes" TEXT NOT NULL,
                    "CommunicationStyle" TEXT NOT NULL,
                    "IsActive" INTEGER NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS "IX_UserPersonas_IsActive" ON "UserPersonas" ("IsActive");

                CREATE TABLE IF NOT EXISTS "CharacterMediaPresentations" (
                    "CharacterId" TEXT NOT NULL CONSTRAINT "PK_CharacterMediaPresentations" PRIMARY KEY,
                    "PortraitMediaAssetId" TEXT NULL,
                    "BackgroundMediaAssetId" TEXT NULL,
                    "BackgroundEnabled" INTEGER NOT NULL DEFAULT 1,
                    "PortraitZoom" REAL NOT NULL DEFAULT 1.0,
                    "PortraitFocusX" REAL NOT NULL DEFAULT 0.5,
                    "PortraitFocusY" REAL NOT NULL DEFAULT 0.5,
                    "BackgroundZoom" REAL NOT NULL DEFAULT 1.0,
                    "BackgroundFocusX" REAL NOT NULL DEFAULT 0.5,
                    "BackgroundFocusY" REAL NOT NULL DEFAULT 0.5,
                    "UpdatedAt" TEXT NOT NULL,
                    CONSTRAINT "FK_CharacterMediaPresentations_Characters_CharacterId" FOREIGN KEY ("CharacterId") REFERENCES "Characters" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_CharacterMediaPresentations_MediaAssets_PortraitMediaAssetId" FOREIGN KEY ("PortraitMediaAssetId") REFERENCES "MediaAssets" ("Id") ON DELETE SET NULL,
                    CONSTRAINT "FK_CharacterMediaPresentations_MediaAssets_BackgroundMediaAssetId" FOREIGN KEY ("BackgroundMediaAssetId") REFERENCES "MediaAssets" ("Id") ON DELETE SET NULL
                );
                CREATE INDEX IF NOT EXISTS "IX_CharacterMediaPresentations_PortraitMediaAssetId" ON "CharacterMediaPresentations" ("PortraitMediaAssetId");
                CREATE INDEX IF NOT EXISTS "IX_CharacterMediaPresentations_BackgroundMediaAssetId" ON "CharacterMediaPresentations" ("BackgroundMediaAssetId");
                """, cancellationToken);

            await AddColumnIfMissingAsync(connection, "Conversations", "UserPersonaId", cancellationToken);
            await AddColumnIfMissingAsync(connection, "Characters", "UniverseId", cancellationToken);
            await AddColumnIfMissingAsync(connection, "Conversations", "ScenePlace", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await AddColumnIfMissingAsync(connection, "Conversations", "SceneTime", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await AddColumnIfMissingAsync(connection, "Conversations", "SceneAtmosphere", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await AddColumnIfMissingAsync(connection, "Conversations", "SceneParticipants", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await AddColumnIfMissingAsync(connection, "Conversations", "SceneIntent", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await AddColumnIfMissingAsync(connection, "Conversations", "SceneKnownFacts", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await AddColumnIfMissingAsync(connection, "Conversations", "SceneBoundaries", "TEXT NOT NULL DEFAULT ''", cancellationToken);
            await AddColumnIfMissingAsync(connection, "Memories", "UserPersonaId", cancellationToken);
            await AddColumnIfMissingAsync(connection, "DiaryEntries", "UserPersonaId", cancellationToken);
            await AddColumnIfMissingAsync(connection, "Relationships", "UserPersonaId", cancellationToken);
            await AddColumnIfMissingAsync(connection, "CharacterMediaPresentations", "PortraitZoom", "REAL NOT NULL DEFAULT 1.0", cancellationToken);
            await AddColumnIfMissingAsync(connection, "CharacterMediaPresentations", "PortraitFocusX", "REAL NOT NULL DEFAULT 0.5", cancellationToken);
            await AddColumnIfMissingAsync(connection, "CharacterMediaPresentations", "PortraitFocusY", "REAL NOT NULL DEFAULT 0.5", cancellationToken);
            await AddColumnIfMissingAsync(connection, "CharacterMediaPresentations", "BackgroundZoom", "REAL NOT NULL DEFAULT 1.0", cancellationToken);
            await AddColumnIfMissingAsync(connection, "CharacterMediaPresentations", "BackgroundFocusX", "REAL NOT NULL DEFAULT 0.5", cancellationToken);
            await AddColumnIfMissingAsync(connection, "CharacterMediaPresentations", "BackgroundFocusY", "REAL NOT NULL DEFAULT 0.5", cancellationToken);
            await AddColumnIfMissingAsync(connection, "UserPersonas", "PortraitRelativePath", "TEXT NOT NULL DEFAULT ''", cancellationToken);

            await ExecuteAsync(connection, """
                DROP INDEX IF EXISTS "IX_Relationships_CharacterId";
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_Relationships_CharacterId_UserPersonaId" ON "Relationships" ("CharacterId", "UserPersonaId");
                CREATE INDEX IF NOT EXISTS "IX_Conversations_CharacterId_UserPersonaId_UpdatedAt" ON "Conversations" ("CharacterId", "UserPersonaId", "UpdatedAt");
                CREATE INDEX IF NOT EXISTS "IX_Memories_CharacterId_UserPersonaId_Kind_Status" ON "Memories" ("CharacterId", "UserPersonaId", "Kind", "Status");
                CREATE INDEX IF NOT EXISTS "IX_DiaryEntries_CharacterId_UserPersonaId_CreatedAt" ON "DiaryEntries" ("CharacterId", "UserPersonaId", "CreatedAt");
                CREATE INDEX IF NOT EXISTS "IX_Characters_UniverseId" ON "Characters" ("UniverseId");
                """, cancellationToken);
            return true;
        }
        finally
        {
            if (closeWhenDone) await connection.CloseAsync();
        }
    }

    public static async Task CompleteAsync(CompanionDbContext db, Guid defaultUserPersonaId, Guid defaultUniverseId, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var closeWhenDone = connection.State != ConnectionState.Open;
        if (closeWhenDone) await connection.OpenAsync(cancellationToken);
        try
        {
            foreach (var table in new[] { "Conversations", "Memories", "DiaryEntries", "Relationships" })
            {
                await using var command = connection.CreateCommand();
                command.CommandText = $"UPDATE \"{table}\" SET \"UserPersonaId\" = $userId WHERE \"UserPersonaId\" IS NULL OR TRIM(\"UserPersonaId\") = '';";
                var parameter = command.CreateParameter();
                parameter.ParameterName = "$userId";
                parameter.Value = defaultUserPersonaId;
                command.Parameters.Add(parameter);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var universeCommand = connection.CreateCommand())
            {
                universeCommand.CommandText = "UPDATE \"Characters\" SET \"UniverseId\" = $universeId WHERE \"UniverseId\" IS NULL OR TRIM(\"UniverseId\") = '';";
                AddParameter(universeCommand, "$universeId", defaultUniverseId);
                await universeCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await ExecuteAsync(connection, $"PRAGMA user_version={CurrentVersion};", cancellationToken);
        }
        finally
        {
            if (closeWhenDone) await connection.CloseAsync();
        }
    }

    private static Task AddColumnIfMissingAsync(DbConnection connection, string table, string column, CancellationToken cancellationToken) =>
        AddColumnIfMissingAsync(connection, table, column, "TEXT NULL", cancellationToken);

    private static async Task AddColumnIfMissingAsync(DbConnection connection, string table, string column, string definition, CancellationToken cancellationToken)
    {
        await using var query = connection.CreateCommand();
        query.CommandText = "SELECT COUNT(*) FROM pragma_table_info($table) WHERE name = $column;";
        AddParameter(query, "$table", table);
        AddParameter(query, "$column", column);
        var exists = Convert.ToInt32(await query.ExecuteScalarAsync(cancellationToken)) > 0;
        if (!exists)
            await ExecuteAsync(connection, $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {definition};", cancellationToken);
    }

    private static async Task CreateBackupAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(connection.DataSource) || connection.DataSource == ":memory:") return;
        var databasePath = Path.GetFullPath(connection.DataSource);
        if (!File.Exists(databasePath) || new FileInfo(databasePath).Length == 0) return;

        var backupDirectory = Path.Combine(Path.GetDirectoryName(databasePath)!, "backups");
        Directory.CreateDirectory(backupDirectory);
        var backupPath = Path.Combine(backupDirectory, $"tinta-viva-pre-v{CurrentVersion}-{DateTime.Now:yyyyMMdd-HHmmssfff}.db");
        await using var command = connection.CreateCommand();
        command.CommandText = "VACUUM INTO $backupPath;";
        AddParameter(command, "$backupPath", backupPath);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<object?> ScalarAsync(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task ExecuteAsync(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
