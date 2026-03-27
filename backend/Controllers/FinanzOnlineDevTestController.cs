using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Services.FinanzOnlineIntegration;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Yalnızca Development: TEST modunda sentetik FinanzOnline outbox kuyruğu (fatura/ödeme akışına dokunmaz).
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/finanzonline-dev-test")]
[Produces("application/json")]
public sealed class FinanzOnlineDevTestController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly IOptionsMonitor<FinanzOnlineDevTestOptions> _options;
    private readonly IFinanzOnlineOutboxService _outbox;
    private readonly ILogger<FinanzOnlineDevTestController> _logger;

    public FinanzOnlineDevTestController(
        IWebHostEnvironment env,
        IOptionsMonitor<FinanzOnlineDevTestOptions> options,
        IFinanzOnlineOutboxService outbox,
        ILogger<FinanzOnlineDevTestController> logger)
    {
        _env = env;
        _options = options;
        _outbox = outbox;
        _logger = logger;
    }

    /// <summary>
    /// POST: Geçerli <c>belegpruefung</c> (sentetik DEP) ile TEST outbox satırı oluşturur.
    /// <c>FinanzOnline:DevTest:AllowEnqueueSmokeTest</c> = true ve host Development olmalı; aksi halde 404.
    /// </summary>
    [HttpPost("enqueue-smoke")]
    [HasPermission(AppPermissions.FinanzOnlineSubmit)]
    public async Task<ActionResult<FinanzOnlineDevTestEnqueueResponse>> EnqueueSmokeAsync(
        [FromBody] FinanzOnlineDevTestEnqueueRequest? body,
        CancellationToken cancellationToken)
    {
        if (!_env.IsDevelopment())
            return NotFound();

        if (!_options.CurrentValue.AllowEnqueueSmokeTest)
        {
            _logger.LogWarning("FinanzOnline dev-test enqueue blocked: AllowEnqueueSmokeTest is false.");
            return NotFound();
        }

        var aggregateId = Guid.NewGuid();
        var registerId = string.IsNullOrWhiteSpace(body?.RegisterId) ? "DEV-RK-SMOKE" : body!.RegisterId!.Trim();
        if (registerId.Length > 256)
            return BadRequest(new { message = "registerId too long." });

        var requestPayload = JsonSerializer.Serialize(new { devTestSmoke = true, aggregateId });
        var payloadHash = ComputeSha256Hex(requestPayload);
        var businessKey = $"dev-smoke-{aggregateId:N}";
        var beleg = FinanzOnlineDevTestSmoke.BuildSyntheticDepBeleg();

        if (!FinanzOnlineRkdbBelegpruefungValidator.IsValidDepCandidate(beleg))
            return StatusCode(500, new { message = "Synthetic DEP invariant failed.", code = "FINANZONLINE_DEVTEST_INVARIANT" });

        var cmd = new FinanzOnlineRkdbBelegpruefungCommand
        {
            Beleg = beleg,
            PaketNr = 1,
            SatzNr = 1,
            TsErstellungUtc = DateTimeOffset.UtcNow
        };

        var payload = new FinanzOnlineOutboxPayload
        {
            Mode = FinanzOnlineIntegrationMode.TEST,
            Scope = new FinanzOnlineScope { RegisterId = registerId },
            Correlation = new FinanzOnlineCorrelationContext
            {
                BusinessKey = businessKey,
                PayloadHash = payloadHash,
                CorrelationId = aggregateId.ToString("N")
            },
            SubmissionKind = FinanzOnlineSubmissionKind.Register,
            PayloadJson = requestPayload,
            RkdbBelegpruefung = cmd
        };

        var msg = await _outbox.EnqueueSubmissionAsync(
            aggregateType: "DevTestSmoke",
            aggregateId: aggregateId,
            messageType: "RegistrierkassenSubmission",
            businessKey: businessKey,
            payload: payload,
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "FinanzOnline dev-test smoke enqueued: aggregateId={AggregateId}, outboxId={OutboxId}",
            aggregateId, msg.Id);

        return Ok(new FinanzOnlineDevTestEnqueueResponse
        {
            OutboxMessageId = msg.Id,
            AggregateId = aggregateId,
            BusinessKey = businessKey,
            IdempotencyKey = msg.IdempotencyKey
        });
    }

    private static string ComputeSha256Hex(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}

public sealed class FinanzOnlineDevTestEnqueueRequest
{
    public string? RegisterId { get; set; }
}

public sealed class FinanzOnlineDevTestEnqueueResponse
{
    public Guid OutboxMessageId { get; set; }
    public Guid AggregateId { get; set; }
    public string BusinessKey { get; set; } = "";
    public string IdempotencyKey { get; set; } = "";
}
