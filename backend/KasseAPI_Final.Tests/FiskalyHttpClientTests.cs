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
    public async Task AuthenticateAsync_Disabled_ReturnsUnsuccessfulWithoutHttp()
    {
        var handler = new SequenceHandler();
        var client = CreateClient(handler, new FiskalyAccessTokenCache(), enabled: false);

        var result = await client.AuthenticateAsync();

        Assert.False(result.Success);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task CreateSignatureCreationUnit_Disabled_ReturnsMockWithoutHttp()
    {
        var scuId = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var handler = new SequenceHandler();
        var client = CreateClient(handler, new FiskalyAccessTokenCache(), enabled: false);

        var scu = await client.CreateSignatureCreationUnitAsync(scuId, "ATU73948115");

        Assert.True(scu.IsMock);
        Assert.Equal("CREATED", scu.State);
        Assert.Equal(scuId.ToString("D"), scu.Id);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SignReceiptAsync_Disabled_ThrowsWithoutHttp()
    {
        var handler = new SequenceHandler();
        var client = CreateClient(handler, new FiskalyAccessTokenCache(), enabled: false);

        var ex = await Assert.ThrowsAsync<FiskalyApiException>(() => client.SignReceiptAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new FiskalyTransactionData { TotalAmount = 1m }));
        Assert.Contains("disabled", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(handler.Requests);
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
                    return Json(HttpStatusCode.OK, $$"""{"_id":"{{rxId:D}}","state":"SIGNED","signed":true,"_env":"TEST","qr_code_data":"_R1-AT1_test","receipt_number":"42","time_signature":1577833200}""");
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
        Assert.True(signed.Signed);
        Assert.Equal("42", signed.ReceiptNumber);
        Assert.Equal(1577833200, signed.TimeSignature);
    }

    [Fact]
    public async Task SignReceiptAsync_PostsMixedVatRates()
    {
        var crId = Guid.Parse("33333333-3333-4333-8333-333333333333");
        var rxId = Guid.Parse("55555555-5555-4555-8555-555555555555");
        string? body = null;
        var handler = new SequenceHandler
        {
            Responders =
            {
                _ => Json(HttpStatusCode.OK, """{"access_token":"tok"}"""),
                req =>
                {
                    body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                    return Json(HttpStatusCode.OK, $$"""{"_id":"{{rxId:D}}","state":"SIGNED","signed":true}""");
                }
            }
        };
        var client = CreateClient(handler, new FiskalyAccessTokenCache());

        await client.SignReceiptAsync(crId, rxId, new FiskalyTransactionData
        {
            ReceiptType = "NORMAL",
            AmountsPerVatRate =
            [
                new FiskalyVatAmount { VatRate = "STANDARD", Amount = 10.00m },
                new FiskalyVatAmount { VatRate = "REDUCED_1", Amount = 5.50m }
            ]
        });

        Assert.Contains("\"vat_rate\":\"STANDARD\"", body, StringComparison.Ordinal);
        Assert.Contains("\"amount\":\"10.00\"", body, StringComparison.Ordinal);
        Assert.Contains("\"vat_rate\":\"REDUCED_1\"", body, StringComparison.Ordinal);
        Assert.Contains("\"amount\":\"5.50\"", body, StringComparison.Ordinal);
        Assert.Contains("\"amount\":\"15.50\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetReceiptAsync_GetsByReceiptNumber()
    {
        var crId = Guid.Parse("33333333-3333-4333-8333-333333333333");
        var handler = new SequenceHandler
        {
            Responders =
            {
                _ => Json(HttpStatusCode.OK, """{"access_token":"tok"}"""),
                req =>
                {
                    Assert.Equal(HttpMethod.Get, req.Method);
                    Assert.Contains($"/cash-register/{crId:D}/receipt/42", req.RequestUri!.AbsolutePath);
                    return Json(HttpStatusCode.OK, """{"_id":"aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa","state":"SIGNED","signed":true,"receipt_number":"42","qr_code_data":"_R1-AT3_x"}""");
                }
            }
        };
        var client = CreateClient(handler, new FiskalyAccessTokenCache());

        var receipt = await client.GetReceiptAsync(crId, "42");

        Assert.Equal("42", receipt.ReceiptNumber);
        Assert.True(receipt.Signed);
        Assert.Equal("_R1-AT3_x", receipt.QrCodeData);
    }

    [Fact]
    public async Task GetReceiptAsync_Disabled_ThrowsWithoutHttp()
    {
        var handler = new SequenceHandler();
        var client = CreateClient(handler, new FiskalyAccessTokenCache(), enabled: false);

        var ex = await Assert.ThrowsAsync<FiskalyApiException>(() => client.GetReceiptAsync(Guid.NewGuid(), "42"));
        Assert.Contains("disabled", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task AuthenticateFonAsync_PutsFonAuth()
    {
        var handler = new SequenceHandler
        {
            Responders =
            {
                _ => Json(HttpStatusCode.OK, """{"access_token":"tok"}"""),
                req =>
                {
                    Assert.Equal(HttpMethod.Put, req.Method);
                    Assert.Contains("/fon/auth", req.RequestUri!.AbsolutePath, StringComparison.OrdinalIgnoreCase);
                    return Json(HttpStatusCode.OK, """{"fon_participant_id":"12345678","fon_user_id":"user1","authentication_status":"AUTHENTICATED","time_authentication":1700000000}""");
                }
            }
        };
        var client = CreateClient(handler, new FiskalyAccessTokenCache());

        var result = await client.AuthenticateFonAsync(new FiskalyFonAuthRequest("12345678", "user1", "secret-pin"));

        Assert.True(result.IsAuthenticated);
        Assert.Equal("AUTHENTICATED", result.AuthenticationStatus);
        Assert.Equal("12345678", result.ParticipantId);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task AuthenticateFonAsync_Disabled_ReturnsUnsuccessfulWithoutHttp()
    {
        var handler = new SequenceHandler();
        var client = CreateClient(handler, new FiskalyAccessTokenCache(), enabled: false);

        var result = await client.AuthenticateFonAsync(new FiskalyFonAuthRequest("12345678", "user1", "pin12"));

        Assert.False(result.IsAuthenticated);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task UpdateSignatureCreationUnitStateAsync_PatchesInitialized()
    {
        var scuId = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var handler = new SequenceHandler
        {
            Responders =
            {
                _ => Json(HttpStatusCode.OK, """{"access_token":"tok"}"""),
                req =>
                {
                    Assert.Equal(HttpMethod.Patch, req.Method);
                    Assert.Contains($"/signature-creation-unit/{scuId:D}", req.RequestUri!.AbsolutePath);
                    return Json(HttpStatusCode.OK, $$"""{"_id":"{{scuId:D}}","state":"INITIALIZED"}""");
                }
            }
        };
        var client = CreateClient(handler, new FiskalyAccessTokenCache());

        var scu = await client.UpdateSignatureCreationUnitStateAsync(scuId.ToString("D"), "INITIALIZED");

        Assert.Equal("INITIALIZED", scu.State);
        Assert.Equal(scuId.ToString("D"), scu.Id);
    }

    [Fact]
    public async Task UpdateCashRegisterStateAsync_Disabled_ThrowsWithoutHttp()
    {
        var handler = new SequenceHandler();
        var client = CreateClient(handler, new FiskalyAccessTokenCache(), enabled: false);

        var ex = await Assert.ThrowsAsync<FiskalyApiException>(() =>
            client.UpdateCashRegisterStateAsync(Guid.NewGuid(), "INITIALIZED"));
        Assert.Contains("disabled", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(handler.Requests);
    }

    private static FiskalyHttpClient CreateClient(
        SequenceHandler handler,
        FiskalyAccessTokenCache cache,
        string apiKey = "test-key",
        string apiSecret = "test-secret",
        bool enabled = true)
    {
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://rksv.fiskaly.com/api/v1/")
        };
        var options = Options.Create(new FiskalyOptions
        {
            Enabled = enabled,
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
