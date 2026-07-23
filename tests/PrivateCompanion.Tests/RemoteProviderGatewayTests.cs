using System.Net;
using System.Net.Http.Headers;
using System.Text;
using PrivateCompanion.Application;
using PrivateCompanion.Domain;
using PrivateCompanion.Infrastructure.Providers;

namespace PrivateCompanion.Tests;

public sealed class RemoteProviderGatewayTests
{
    [Fact]
    public async Task Windows_credential_store_round_trips_and_removes_a_provider_token()
    {
        if (!OperatingSystem.IsWindows()) return;
        var providerId = Guid.NewGuid();
        var store = new WindowsCredentialProviderSecretStore();
        try
        {
            await store.SaveAsync(providerId, "temporary-test-token");

            Assert.True(await store.ExistsAsync(providerId));
            Assert.Equal("temporary-test-token", await store.GetAsync(providerId));
        }
        finally
        {
            await store.DeleteAsync(providerId);
        }
        Assert.False(await store.ExistsAsync(providerId));
    }

    [Fact]
    public async Task Probes_an_https_remote_provider_with_its_optional_bearer_token()
    {
        var provider = new ProviderProfile
        {
            Location = ProviderLocation.Remote,
            Kind = ProviderKind.OpenAICompatible,
            BaseUrl = "https://independent.example/v1"
        };
        var secrets = new MemorySecretStore();
        await secrets.SaveAsync(provider.Id, "private-test-token");
        var handler = new RecordingHandler();
        using var gateway = new OpenAiCompatibleTextGateway(secrets, handler);

        var result = await gateway.ProbeAsync(provider, false);

        Assert.True(result.IsAvailable);
        Assert.Equal("open-model", Assert.Single(result.Models));
        Assert.Equal("Bearer", handler.Authorization?.Scheme);
        Assert.Equal("private-test-token", handler.Authorization?.Parameter);
        Assert.Equal("https://independent.example/v1/models", handler.RequestUri?.ToString());
    }

    [Fact]
    public async Task Rejects_insecure_remote_and_non_loopback_local_endpoints()
    {
        using var gateway = new OpenAiCompatibleTextGateway(new MemorySecretStore(), new RecordingHandler());
        var insecureRemote = new ProviderProfile
        {
            Location = ProviderLocation.Remote,
            BaseUrl = "http://independent.example/v1"
        };
        var externalLocal = new ProviderProfile
        {
            Location = ProviderLocation.Local,
            BaseUrl = "https://independent.example/v1"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.ProbeAsync(insecureRemote, false));
        await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.ProbeAsync(externalLocal, false));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public AuthenticationHeaderValue? Authorization { get; private set; }
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization;
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"data":[{"id":"open-model"}]}""", Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class MemorySecretStore : IProviderSecretStore
    {
        private readonly Dictionary<Guid, string> _items = [];

        public Task<bool> ExistsAsync(Guid providerId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.ContainsKey(providerId));

        public Task<string?> GetAsync(Guid providerId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.GetValueOrDefault(providerId));

        public Task SaveAsync(Guid providerId, string secret, CancellationToken cancellationToken = default)
        {
            _items[providerId] = secret;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid providerId, CancellationToken cancellationToken = default)
        {
            _items.Remove(providerId);
            return Task.CompletedTask;
        }
    }
}
