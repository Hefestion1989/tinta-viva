using Microsoft.EntityFrameworkCore;
using PrivateCompanion.Domain;

namespace PrivateCompanion.Infrastructure.Persistence;

public sealed class CompanionDbContext(DbContextOptions<CompanionDbContext> options) : DbContext(options)
{
    public DbSet<StoryUniverse> Universes => Set<StoryUniverse>();
    public DbSet<Character> Characters => Set<Character>();
    public DbSet<CharacterRevision> CharacterRevisions => Set<CharacterRevision>();
    public DbSet<UserPersona> UserPersonas => Set<UserPersona>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Memory> Memories => Set<Memory>();
    public DbSet<DiaryEntry> DiaryEntries => Set<DiaryEntry>();
    public DbSet<RelationshipState> Relationships => Set<RelationshipState>();
    public DbSet<ProviderProfile> Providers => Set<ProviderProfile>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<CharacterMediaPresentation> CharacterMediaPresentations => Set<CharacterMediaPresentation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StoryUniverse>().HasIndex(x => x.Name);
        modelBuilder.Entity<Character>().HasIndex(x => x.Name);
        modelBuilder.Entity<Character>().HasIndex(x => x.UniverseId);
        modelBuilder.Entity<CharacterRevision>().HasIndex(x => new { x.CharacterId, x.Revision }).IsUnique();
        modelBuilder.Entity<UserPersona>().HasIndex(x => x.IsActive);
        modelBuilder.Entity<Conversation>().HasIndex(x => new { x.CharacterId, x.UserPersonaId, x.UpdatedAt });
        modelBuilder.Entity<Message>().HasIndex(x => new { x.ConversationId, x.Sequence }).IsUnique();
        modelBuilder.Entity<Memory>().HasIndex(x => new { x.CharacterId, x.UserPersonaId, x.Kind, x.Status });
        modelBuilder.Entity<DiaryEntry>().HasIndex(x => new { x.CharacterId, x.UserPersonaId, x.CreatedAt });
        modelBuilder.Entity<RelationshipState>().HasIndex(x => new { x.CharacterId, x.UserPersonaId }).IsUnique();
        modelBuilder.Entity<ProviderProfile>().HasIndex(x => x.IsActive);
        modelBuilder.Entity<MediaAsset>().HasIndex(x => new { x.CharacterId, x.CreatedAt });
        modelBuilder.Entity<CharacterMediaPresentation>().HasKey(x => x.CharacterId);

        modelBuilder.Entity<Conversation>()
            .HasOne<Character>()
            .WithMany()
            .HasForeignKey(x => x.CharacterId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Character>()
            .HasOne<StoryUniverse>()
            .WithMany()
            .HasForeignKey(x => x.UniverseId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Conversation>()
            .HasOne<UserPersona>()
            .WithMany()
            .HasForeignKey(x => x.UserPersonaId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Message>()
            .HasOne<Conversation>()
            .WithMany()
            .HasForeignKey(x => x.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Memory>()
            .HasOne<Character>()
            .WithMany()
            .HasForeignKey(x => x.CharacterId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Memory>()
            .HasOne<UserPersona>()
            .WithMany()
            .HasForeignKey(x => x.UserPersonaId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<DiaryEntry>()
            .HasOne<Character>()
            .WithMany()
            .HasForeignKey(x => x.CharacterId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<DiaryEntry>()
            .HasOne<UserPersona>()
            .WithMany()
            .HasForeignKey(x => x.UserPersonaId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<RelationshipState>()
            .HasOne<Character>()
            .WithMany()
            .HasForeignKey(x => x.CharacterId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RelationshipState>()
            .HasOne<UserPersona>()
            .WithMany()
            .HasForeignKey(x => x.UserPersonaId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CharacterRevision>()
            .HasOne<Character>()
            .WithMany()
            .HasForeignKey(x => x.CharacterId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CharacterMediaPresentation>()
            .HasOne<Character>()
            .WithOne()
            .HasForeignKey<CharacterMediaPresentation>(x => x.CharacterId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CharacterMediaPresentation>()
            .HasOne<MediaAsset>()
            .WithMany()
            .HasForeignKey(x => x.PortraitMediaAssetId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<CharacterMediaPresentation>()
            .HasOne<MediaAsset>()
            .WithMany()
            .HasForeignKey(x => x.BackgroundMediaAssetId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class CompanionDbContextFactory(string databasePath) : IDbContextFactory<CompanionDbContext>
{
    private readonly DbContextOptions<CompanionDbContext> _options = new DbContextOptionsBuilder<CompanionDbContext>()
        .UseSqlite($"Data Source={databasePath};Cache=Shared;Pooling=False")
        .EnableDetailedErrors()
        .Options;

    public CompanionDbContext CreateDbContext() => new(_options);
    public Task<CompanionDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(CreateDbContext());
}
