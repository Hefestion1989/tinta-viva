using PrivateCompanion.Domain;
using PrivateCompanion.Infrastructure.Persistence;

namespace PrivateCompanion.Tests;

public sealed class RepositoryIntegrationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "PrivateCompanion.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Creates_character_conversation_memory_and_reads_them_back()
    {
        Directory.CreateDirectory(_directory);
        var factory = new CompanionDbContextFactory(Path.Combine(_directory, "test.db"));
        var repository = new CompanionRepository(factory, string.Empty);
        await repository.InitializeAsync();

        var characters = await repository.GetCharactersAsync();
        var character = Assert.Single(characters);
        var userPersona = await repository.GetActiveUserPersonaAsync();
        Assert.True(character.Age >= 18);

        var conversation = new Conversation { CharacterId = character.Id, UserPersonaId = userPersona.Id, Title = "Prueba" };
        await repository.SaveConversationAsync(conversation);
        await repository.AddMessageAsync(new Message
        {
            ConversationId = conversation.Id,
            Role = MessageRole.User,
            Sequence = 1,
            Content = "Hola"
        });
        await repository.SaveMemoryAsync(new Memory
        {
            CharacterId = character.Id,
            UserPersonaId = userPersona.Id,
            Kind = MemoryKind.Personal,
            Title = "Dato",
            Content = "Prefiere conversar de noche"
        });

        Assert.Single(await repository.GetConversationsAsync(character.Id, userPersona.Id));
        Assert.Single(await repository.GetMessagesAsync(conversation.Id));
        Assert.Single(await repository.GetMemoriesAsync(character.Id, userPersona.Id));
    }

    [Fact]
    public async Task Keeps_conversations_memories_and_relationships_separate_per_user_persona()
    {
        Directory.CreateDirectory(_directory);
        var factory = new CompanionDbContextFactory(Path.Combine(_directory, "isolation.db"));
        var repository = new CompanionRepository(factory, string.Empty);
        await repository.InitializeAsync();
        var character = Assert.Single(await repository.GetCharactersAsync());
        var firstUser = await repository.GetActiveUserPersonaAsync();
        var secondUser = new UserPersona { Name = "Segundo perfil", Age = 30 };
        await repository.SaveUserPersonaAsync(secondUser);

        await repository.SaveConversationAsync(new Conversation { CharacterId = character.Id, UserPersonaId = firstUser.Id, Title = "Primera" });
        await repository.SaveConversationAsync(new Conversation { CharacterId = character.Id, UserPersonaId = secondUser.Id, Title = "Segunda" });
        await repository.SaveMemoryAsync(new Memory { CharacterId = character.Id, UserPersonaId = firstUser.Id, Title = "A", Content = "Solo del primero" });
        await repository.SaveMemoryAsync(new Memory { CharacterId = character.Id, UserPersonaId = secondUser.Id, Title = "B", Content = "Solo del segundo" });

        Assert.Equal("Primera", Assert.Single(await repository.GetConversationsAsync(character.Id, firstUser.Id)).Title);
        Assert.Equal("Segunda", Assert.Single(await repository.GetConversationsAsync(character.Id, secondUser.Id)).Title);
        Assert.Equal("A", Assert.Single(await repository.GetMemoriesAsync(character.Id, firstUser.Id)).Title);
        Assert.Equal("B", Assert.Single(await repository.GetMemoriesAsync(character.Id, secondUser.Id)).Title);
        Assert.NotEqual((await repository.GetRelationshipAsync(character.Id, firstUser.Id)).Id,
            (await repository.GetRelationshipAsync(character.Id, secondUser.Id)).Id);
    }

    [Fact]
    public void Rejects_underage_characters()
    {
        var character = new Character { Name = "Inválido", Age = 17 };
        Assert.Throws<System.ComponentModel.DataAnnotations.ValidationException>(character.Validate);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
