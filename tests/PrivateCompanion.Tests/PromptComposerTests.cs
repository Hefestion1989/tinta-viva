using PrivateCompanion.Application;
using PrivateCompanion.Domain;

namespace PrivateCompanion.Tests;

public sealed class PromptComposerTests
{
    [Fact]
    public void Selects_pinned_and_relevant_memories_first()
    {
        var memories = new List<Memory>
        {
            new() { Title = "Café", Content = "Le gusta el café", Importance = .3 },
            new() { Title = "Música", Content = "Su banda favorita es No Te Va Gustar", Importance = .8 },
            new() { Title = "Importante", Content = "Un dato fijado", Importance = .1, IsPinned = true }
        };

        var selected = PromptComposer.SelectMemories(memories, "¿cuál es mi banda favorita?", 2);

        Assert.Equal("Importante", selected[0].Title);
        Assert.Contains(selected, x => x.Title == "Música");
    }

    [Fact]
    public void Marks_sandbox_scene_as_non_canonical_in_prompt()
    {
        var character = new Character { Name = "Luna", Age = 29 };
        var userPersona = new UserPersona { Name = "Alex", Age = 30 };
        var prompt = new PromptComposer().Compose(null, character, userPersona,
            new Conversation { SceneMode = SceneMode.Sandbox, SceneTitle = "Historia alternativa" },
            new RelationshipState { CharacterId = character.Id }, [], []);

        Assert.Contains("sandbox", prompt[0].Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no modifica", prompt[0].Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Requests_natural_rioplatense_without_caricature_and_identifies_the_user()
    {
        var character = new Character { Name = "Tomás", Age = 32, Language = "Español rioplatense" };
        var userPersona = new UserPersona { Name = "Dani", Age = 28, Likes = "Cine" };

        var prompt = new PromptComposer().Compose(null, character, userPersona,
            new Conversation { UserPersonaId = userPersona.Id },
            new RelationshipState { CharacterId = character.Id, UserPersonaId = userPersona.Id }, [], []);

        Assert.Contains("voseo", prompt[0].Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no caricaturesco", prompt[0].Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Dani", prompt[0].Content);
        Assert.Contains("contenido erótico explícito", prompt[0].Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sin eufemismos forzados", prompt[0].Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sin eufemismos forzados, advertencias repetitivas ni moralizaciones", prompt[0].Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Includes_universe_canon_and_structured_scene_without_promoting_sandbox()
    {
        var universe = new StoryUniverse { Name = "Segmentum Solar", Canon = "Terra continúa siendo el centro político.", Rules = "No alterar hechos fijados." };
        var character = new Character { Name = "Iria", Age = 34, UniverseId = universe.Id };
        var userPersona = new UserPersona { Name = "Dami", Age = 30 };
        var scene = new Conversation
        {
            SceneMode = SceneMode.Sandbox,
            SceneTitle = "Audiencia alternativa",
            ScenePlace = "Palacio Imperial",
            SceneParticipants = "Iria y Dami",
            SceneKnownFacts = "Iria desconoce la orden secreta"
        };

        var prompt = new PromptComposer().Compose(universe, character, userPersona, scene,
            new RelationshipState { CharacterId = character.Id, UserPersonaId = userPersona.Id }, [], []);

        Assert.Contains("Segmentum Solar", prompt[0].Content);
        Assert.Contains("Palacio Imperial", prompt[0].Content);
        Assert.Contains("orden secreta", prompt[0].Content);
        Assert.Contains("no modifica", prompt[0].Content, StringComparison.OrdinalIgnoreCase);
    }
}
