using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
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

    [Fact]
    public async Task Managed_lm_studio_recovers_the_model_and_uses_non_streaming_completion()
    {
        var provider = new ProviderProfile
        {
            Name = "Motor Tinta · DarkIdol",
            Location = ProviderLocation.Local,
            Kind = ProviderKind.LMStudio,
            BaseUrl = "http://127.0.0.1:1234/v1",
            Model = "tinta-viva",
            ContextBudget = 8192
        };
        var handler = new ManagedLmStudioHandler();
        var manager = new FakeLocalEngineManager(() => handler.IsLoaded = true);
        using var gateway = new OpenAiCompatibleTextGateway(
            new MemorySecretStore(),
            handler,
            manager);

        var chunks = new List<string>();
        await foreach (var chunk in gateway.StreamAsync(new TextGenerationRequest(
                           provider,
                           [new ChatEntry("user", "Hola")])))
            chunks.Add(chunk);

        Assert.Equal("Respuesta local", Assert.Single(chunks));
        Assert.True(manager.WasActivated);
        Assert.False(handler.LastStreamValue);
    }

    [Fact]
    public async Task Managed_lm_studio_recovers_a_vulkan_device_loss_on_cpu()
    {
        var provider = new ProviderProfile
        {
            Name = "Motor Tinta · DarkIdol",
            Location = ProviderLocation.Local,
            Kind = ProviderKind.LMStudio,
            BaseUrl = "http://127.0.0.1:1234/v1",
            Model = "tinta-viva",
            ContextBudget = 8192
        };
        var handler = new DeviceLostThenSuccessHandler();
        var manager = new FakeLocalEngineManager(() => handler.CpuRecoveryCompleted = true);
        using var gateway = new OpenAiCompatibleTextGateway(
            new MemorySecretStore(),
            handler,
            manager);

        var chunks = new List<string>();
        await foreach (var chunk in gateway.StreamAsync(new TextGenerationRequest(
                           provider,
                           [new ChatEntry("user", "Hola")])))
            chunks.Add(chunk);

        Assert.Equal("Recuperado por CPU", Assert.Single(chunks));
        Assert.True(manager.WasCpuActivated);
        Assert.Equal(2, handler.CompletionRequests);
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

    private sealed class ManagedLmStudioHandler : HttpMessageHandler
    {
        public bool IsLoaded { get; set; }
        public bool? LastStreamValue { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get)
            {
                var models = IsLoaded ? """{"data":[{"id":"tinta-viva"}]}""" : """{"data":[]}""";
                return JsonResponse(models);
            }

            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(body);
            LastStreamValue = document.RootElement.GetProperty("stream").GetBoolean();
            return JsonResponse("""{"choices":[{"message":{"content":"Respuesta local"}}]}""");
        }

        private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class DeviceLostThenSuccessHandler : HttpMessageHandler
    {
        public bool CpuRecoveryCompleted { get; set; }
        public int CompletionRequests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get)
                return Task.FromResult(JsonResponse("""{"data":[{"id":"tinta-viva"}]}"""));

            CompletionRequests++;
            return Task.FromResult(CpuRecoveryCompleted
                ? JsonResponse("""{"choices":[{"message":{"content":"Recuperado por CPU"}}]}""")
                : JsonResponse(
                    """{"error":"The model has crashed without additional information."}""",
                    HttpStatusCode.BadRequest));
        }

        private static HttpResponseMessage JsonResponse(
            string json,
            HttpStatusCode status = HttpStatusCode.OK) => new(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class FakeLocalEngineManager(Action onActivate) : ILocalEngineManager
    {
        public bool WasActivated { get; private set; }
        public bool WasCpuActivated { get; private set; }

        public Task<LocalEngineInventory> InspectAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new LocalEngineInventory(
                true,
                "Disponible",
                16L * 1024 * 1024 * 1024,
                [new LocalEngineModel("darkidol", "DarkIdol", "8B", 4_000_000_000, "Q4", 8192)]));

        public Task<LocalEngineActivationResult> ActivateAsync(
            string modelKey,
            int contextLength,
            CancellationToken cancellationToken = default)
        {
            WasActivated = true;
            onActivate();
            return Task.FromResult(new LocalEngineActivationResult(true, "Activo"));
        }

        public Task<LocalEngineActivationResult> ActivateCpuOnlyAsync(
            string modelKey,
            int contextLength,
            CancellationToken cancellationToken = default)
        {
            WasCpuActivated = true;
            onActivate();
            return Task.FromResult(new LocalEngineActivationResult(true, "Activo por CPU"));
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
