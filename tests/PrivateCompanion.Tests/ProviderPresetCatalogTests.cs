using PrivateCompanion.Application;

namespace PrivateCompanion.Tests;

public sealed class ProviderPresetCatalogTests
{
    [Fact]
    public void Online_presets_use_https_and_do_not_point_to_openai()
    {
        var hosted = ProviderPresetCatalog.Online.Where(x => x.Id != "custom").ToList();

        Assert.NotEmpty(hosted);
        Assert.All(hosted, preset => Assert.StartsWith("https://", preset.BaseUrl));
        Assert.DoesNotContain(hosted, preset =>
            preset.BaseUrl.Contains("api.openai.com", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("https://api.venice.ai/api/v1/", "venice")]
    [InlineData("https://openrouter.ai/api/v1", "openrouter")]
    [InlineData("https://my-server.example/v1", "custom")]
    public void Matches_known_and_custom_remote_endpoints(string baseUrl, string expectedId)
    {
        Assert.Equal(expectedId, ProviderPresetCatalog.Match(baseUrl)?.Id);
    }
}
