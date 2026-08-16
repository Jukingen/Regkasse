using System.Net;
using System.Text;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Tse.Fiskaly;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FiskalyConnectionProbeTests
{
    [Fact]
    public async Task ProbeAsync_Disabled_FailsWithoutHttp()
    {
        var handler = new SequenceHandler();
        var probe = CreateProbe(handler, enabled: false);

        var result = await probe.ProbeAsync(new FiskalyConnectionProbeRequest { CreateResources = true });

        Assert.False(result.Success);
        Assert.Equal("Failed", result.Authentication.Status);
        Assert.Contains("disabled", result.Authentication.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Skipped", result.ScuCreation.Status);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ProbeAsync_MissingCredentials_FailsAuthenticationWithoutHttp()
    {
        var handler = new SequenceHandler();
        var probe = CreateProbe(handler, apiKey: "", apiSecret: "");

        var result = await probe.ProbeAsync(new FiskalyConnectionProbeRequest { CreateResources = true });

        Assert.False(result.Success);
        Assert.Equal("Failed", result.Authentication.Status);
        Assert.Contains("not configured", result.Authentication.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Skipped", result.ScuCreation.Status);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ProbeAsync_AuthOnly_DoesNotCreateResources()
    {
        var handler = new SequenceHandler
        {
            Responders =
            {
                req =>
                {
                    Assert.Equal(HttpMethod.Post, req.Method);
                    Assert.EndsWith("/auth", req.RequestUri!.AbsolutePath, StringComparison.OrdinalIgnoreCase);
                    return Json(HttpStatusCode.OK, """{"access_token":"tok-test","expires_at":"2026-08-16T15:00:00Z"}""");
                }
            }
        };
        var probe = CreateProbe(handler);

        var result = await probe.ProbeAsync(new FiskalyConnectionProbeRequest { CreateResources = false });

        Assert.True(result.Success);
        Assert.Equal("Succeeded", result.Authentication.Status);
        Assert.Equal("Skipped", result.ScuCreation.Status);
        Assert.Equal("Skipped", result.CashRegisterCreation.Status);
        Assert.DoesNotContain("tok-test", result.Authentication.Message);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ProbeAsync_AuthThenScuAndCashRegister_Succeeds()
    {
        var scuId = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var crId = Guid.Parse("22222222-2222-4222-8222-222222222222");
        var handler = new SequenceHandler
        {
            Responders =
            {
                req =>
                {
                    Assert.Equal("auth", req.RequestUri!.Segments[^1].TrimEnd('/'));
                    return Json(HttpStatusCode.OK, """{"access_token":"tok-live","expires_at":"2026-08-16T15:00:00Z"}""");
                },
                req =>
                {
                    Assert.Equal(HttpMethod.Put, req.Method);
                    Assert.Contains("/signature-creation-unit/", req.RequestUri!.AbsolutePath, StringComparison.OrdinalIgnoreCase);
                    Assert.Equal("Bearer", req.Headers.Authorization?.Scheme);
                    Assert.Equal("tok-live", req.Headers.Authorization?.Parameter);
                    return Json(HttpStatusCode.OK, $$"""{"_id":"{{scuId:D}}","state":"CREATED"}""");
                },
                req =>
                {
                    Assert.Equal(HttpMethod.Put, req.Method);
                    Assert.Contains("/cash-register/", req.RequestUri!.AbsolutePath, StringComparison.OrdinalIgnoreCase);
                    return Json(HttpStatusCode.OK, $$"""{"_id":"{{crId:D}}","state":"CREATED"}""");
                }
            }
        };
        var probe = CreateProbe(handler);

        var result = await probe.ProbeAsync(new FiskalyConnectionProbeRequest
        {
            CreateResources = true,
            VatId = "ATU73948115"
        });

        Assert.True(result.Success);
        Assert.Equal(scuId.ToString("D"), result.ScuId);
        Assert.Equal(crId.ToString("D"), result.CashRegisterId);
        Assert.Equal("ATU73948115", result.VatIdUsed);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task ProbeAsync_AuthUnauthorized_SkipsResourceCreation()
    {
        var handler = new SequenceHandler
        {
            Responders =
            {
                _ => Json(HttpStatusCode.Unauthorized, """{"message":"invalid api key"}""")
            }
        };
        var probe = CreateProbe(handler);

        var result = await probe.ProbeAsync(new FiskalyConnectionProbeRequest { CreateResources = true });

        Assert.False(result.Success);
        Assert.Equal("Failed", result.Authentication.Status);
        Assert.Equal(401, result.Authentication.HttpStatus);
        Assert.Equal("Skipped", result.ScuCreation.Status);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ProbeAsync_InvalidVatId_FailsScuButStillAttemptsCashRegister()
    {
        var handler = new SequenceHandler
        {
            Responders =
            {
                _ => Json(HttpStatusCode.OK, """{"access_token":"tok"}"""),
                req =>
                {
                    Assert.Contains("/cash-register/", req.RequestUri!.AbsolutePath, StringComparison.OrdinalIgnoreCase);
                    return Json(HttpStatusCode.OK, """{"_id":"cr-1","state":"CREATED"}""");
                }
            }
        };
        var probe = CreateProbe(handler);

        var result = await probe.ProbeAsync(new FiskalyConnectionProbeRequest
        {
            CreateResources = true,
            VatId = "not-a-vat"
        });

        Assert.False(result.Success);
        Assert.Equal("Failed", result.ScuCreation.Status);
        Assert.Equal("Succeeded", result.CashRegisterCreation.Status);
        Assert.Equal(2, handler.Requests.Count);
    }

    private static FiskalyConnectionProbe CreateProbe(
        SequenceHandler handler,
        string apiKey = "test-key",
        string apiSecret = "test-secret",
        bool enabled = true)
    {
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://rksv.fiskaly.com/api/v1/")
        };
        var options = new FiskalyOptions
        {
            Enabled = enabled,
            ApiBaseUrl = "https://rksv.fiskaly.com/api/v1",
            ApiKey = apiKey,
            ApiSecret = apiSecret
        };
        return new FiskalyConnectionProbe(
            http,
            new StaticOptionsMonitor<FiskalyOptions>(options),
            NullLogger<FiskalyConnectionProbe>.Instance);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class SequenceHandler : HttpMessageHandler
    {
        public List<Func<HttpRequestMessage, HttpResponseMessage>> Responders { get; } = new();
        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (Requests.Count > Responders.Count)
            {
                throw new InvalidOperationException(
                    $"Unexpected HTTP call #{Requests.Count} to {request.Method} {request.RequestUri}");
            }

            return Task.FromResult(Responders[Requests.Count - 1](request));
        }
    }

    private sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T> where T : class
    {
        public StaticOptionsMonitor(T value) => CurrentValue = value;
        public T CurrentValue { get; }
        public T Get(string? name) => CurrentValue;
        public IDisposable OnChange(Action<T, string?> listener) => NullDisposable.Instance;

        private sealed class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new();
            public void Dispose() { }
        }
    }
}
