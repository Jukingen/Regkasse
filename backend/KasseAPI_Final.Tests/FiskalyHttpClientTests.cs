using System.Net;
using System.Text;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Tse.Fiskaly;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FiskalyHttpClientTests
{
    [Fact]
    public async Task AuthenticateAsync_CachesTokenForSubsequentCalls()
    {
        var cache = new FiskalyAccessTokenCache();
        var handler = new SequenceHandler
        {
            Responders =
            {
                req =>
                {
                    Assert.Equal(HttpMethod.Post, req.Method);
                    return Json(HttpStatusCode.OK, """{"access_token":"tok-cached"}""");
                }
            }
        };
        var client = CreateClient(handler, cache);

        var first = await client.AuthenticateAsync();
        var second = await client.AuthenticateAsync();

        Assert.True(first.Success);
        Assert.Equal(10, first.AccessTokenLength);
        Assert.Equal(first.ExpiresAt, second.ExpiresAt);
        Assert.Single(handler.Requests);
        Assert.True(cache.TryGet(out var token, out _));
        Assert.Equal("tok-cached", token);
    }

    [Fact]
    public async Task AuthenticateAsync_MissingCredentials_ThrowsWithoutHttp()
    {
        var handler = new SequenceHandler();
        var client = CreateClient(handler, new FiskalyAccessTokenCache(), apiKey: "", apiSecret: "");

        var ex = await Assert.ThrowsAsync<FiskalyApiException>(() => client.AuthenticateAsync());
        Assert.Contains("not configured", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task CreateSignatureCreationUnitAndCashRegister_Succeed()
    {
        var scuId = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var crId = Guid.Parse("22222222-2222-4222-8222-222222222222");
        var handler = new SequenceHandler
        {
            Responders =
            {
                _ => Json(HttpStatusCode.OK, """{"access_token":"tok"}"""),
                req =>
                {
                    Assert.Contains("/signature-creation-unit/", req.RequestUri!.AbsolutePath, StringComparison.OrdinalIgnoreCase);
                    return Json(HttpStatusCode.OK, $$"""{"_id":"{{scuId:D}}","state":"CREATED"}""");
                },
                req =>
                {
                    Assert.Contains("/cash-register/", req.RequestUri!.AbsolutePath, StringComparison.OrdinalIgnoreCase);
                    return Json(HttpStatusCode.OK, $$"""{"_id":"{{crId:D}}","state":"CREATED"}""");
                }
            }
        };
        var client = CreateClient(handler, new FiskalyAccessTokenCache());

        var scu = await client.CreateSignatureCreationUnitAsync(scuId, "ATU73948115");
        var cr = await client.CreateCashRegisterAsync(crId, "Main POS");

        Assert.Equal(scuId.ToString("D"), scu.Id);
        Assert.Equal("CREATED", scu.State);
        Assert.Equal(crId.ToString("D"), cr.Id);
        Assert.Equal("CREATED", cr.State);
    }

    [Fact]
    public async Task SignReceiptAsync_PostsStandardV1Schema()
    {
        var crId = Guid.Parse("33333333-3333-4333-8333-333333333333");
        var rxId = Guid.Parse("44444444-4444-4444-8444-444444444444");
        var handler = new SequenceHandler
        {
            Responders =
            {
                _ => Json(HttpStatusCode.OK, """{"access_token":"tok"}"""),
                req =>
                {
                    Assert.Equal(HttpMethod.Put, req.Method);
                    Assert.Contains($"/cash-register/{crId:D}/receipt/{rxId:D}", req.RequestUri!.AbsolutePath);
                    return Json(HttpStatusCode.OK, $$"""{"_id":"{{rxId:D}}","state":"SIGNED","_env":"TEST","qr_code_data":"_R1-AT1_test"}""");
                }
            }
        };
        var client = CreateClient(handler, new FiskalyAccessTokenCache());

        var signed = await client.SignReceiptAsync(crId, rxId, new FiskalyTransactionData
        {
            CashRegisterId = crId.ToString("D"),
            TotalAmount = 12.00m
        });

        Assert.Equal("SIGNED", signed.State);
        Assert.Equal("TEST", signed.Environment);
        Assert.Equal("_R1-AT1_test", signed.QrCodeData);
    }

    private static FiskalyHttpClient CreateClient(
        SequenceHandler handler,
        FiskalyAccessTokenCache cache,
        string apiKey = "test-key",
        string apiSecret = "test-secret")
    {
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://rksv.fiskaly.com/api/v1/")
        };
        var options = Options.Create(new FiskalyOptions
        {
            Enabled = true,
            ApiKey = apiKey,
            ApiSecret = apiSecret,
            ApiBaseUrl = "https://rksv.fiskaly.com/api/v1",
            TokenCacheHours = 24
        });
        return new FiskalyHttpClient(http, options, cache, NullLogger<FiskalyHttpClient>.Instance);
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
}
