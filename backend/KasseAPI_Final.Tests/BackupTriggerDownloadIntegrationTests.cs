using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// HTTP integration: Manager triggers tenant-scoped manual backup, worker completes it, artifact download succeeds.
/// </summary>
[Collection("BackupHttpIntegration")]
public sealed class BackupTriggerDownloadIntegrationTests
{
    private readonly BackupImportWebApplicationFactory _factory;

    public BackupTriggerDownloadIntegrationTests(BackupImportWebApplicationFactory factory) =>
        _factory = factory;

    [Fact]
    public async Task Manager_trigger_orchestrator_succeed_download_returns_blob()
    {
        var client = await CreateAuthenticatedManagerClientAsync(BackupImportWebApplicationFactory.TenantASlug);

        var triggerResponse = await client.PostAsJsonAsync("/api/admin/backup/trigger", new { });
        Assert.Equal(HttpStatusCode.Accepted, triggerResponse.StatusCode);

        var triggered = await triggerResponse.Content.ReadFromJsonAsync<BackupTriggerResponseDto>();
        Assert.NotNull(triggered);
        Assert.True(triggered!.NewQueuedRunCreated);
        var runId = triggered.Run.Id;
        Assert.NotEqual(Guid.Empty, runId);
        Assert.Equal(BackupRunStatus.Queued, triggered.Run.Status);

        await _factory.ProcessNextQueuedBackupRunAsync();

        var runResponse = await client.GetAsync($"/api/admin/backup/runs/{runId}");
        runResponse.EnsureSuccessStatusCode();
        var runDto = await runResponse.Content.ReadFromJsonAsync<BackupRunResponseDto>();
        Assert.NotNull(runDto);
        Assert.Equal(BackupRunStatus.Succeeded, runDto!.Status);
        Assert.NotNull(runDto.Artifacts);
        var dumpArtifact = Assert.Single(runDto.Artifacts!, a => a.ArtifactType == BackupArtifactType.LogicalDump);
        Assert.True(dumpArtifact.IsFilePresentForDownload);

        var downloadResponse = await client.GetAsync(
            $"/api/admin/backup/runs/{runId}/artifacts/{dumpArtifact.Id}/download");
        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        var bytes = await downloadResponse.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(bytes);
        Assert.Contains($"fake-bytes-{runId:N}", System.Text.Encoding.UTF8.GetString(bytes));

        Assert.Contains(
            Directory.GetFiles(_factory.StagingRoot, "*.dump"),
            path => Path.GetFileName(path).StartsWith("backup_tenant-a_", StringComparison.OrdinalIgnoreCase));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var run = await db.BackupRuns.AsNoTracking().SingleAsync(r => r.Id == runId);
        Assert.Equal(BackupImportWebApplicationFactory.TenantAId, run.TenantId);
        Assert.NotNull(run.IdempotencyKey);
        Assert.Contains(
            $"manual-tenant-{BackupImportWebApplicationFactory.TenantAId}",
            run.IdempotencyKey,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Manager_cannot_download_other_tenant_run_returns_not_found()
    {
        var clientA = await CreateAuthenticatedManagerClientAsync(BackupImportWebApplicationFactory.TenantASlug);

        var triggerResponse = await clientA.PostAsJsonAsync("/api/admin/backup/trigger", new { });
        Assert.Equal(HttpStatusCode.Accepted, triggerResponse.StatusCode);
        var triggered = await triggerResponse.Content.ReadFromJsonAsync<BackupTriggerResponseDto>();
        Assert.NotNull(triggered);
        var runId = triggered!.Run.Id;

        await _factory.ProcessNextQueuedBackupRunAsync();

        var runResponse = await clientA.GetAsync($"/api/admin/backup/runs/{runId}");
        runResponse.EnsureSuccessStatusCode();
        var runDto = await runResponse.Content.ReadFromJsonAsync<BackupRunResponseDto>();
        Assert.NotNull(runDto);
        var artifactId = Assert.Single(runDto!.Artifacts!, a => a.ArtifactType == BackupArtifactType.LogicalDump).Id;

        var clientB = await CreateAuthenticatedManagerClientAsync(BackupImportWebApplicationFactory.TenantBSlug);
        var downloadResponse = await clientB.GetAsync(
            $"/api/admin/backup/runs/{runId}/artifacts/{artifactId}/download");

        Assert.Equal(HttpStatusCode.NotFound, downloadResponse.StatusCode);
        var body = await downloadResponse.Content.ReadAsStringAsync();
        Assert.Contains("BACKUP_RUN_NOT_FOUND", body, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<HttpClient> CreateAuthenticatedManagerClientAsync(string tenantSlug)
    {
        var client = _factory.CreateTenantClient(tenantSlug);
        var token = await LoginAsManagerAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<string> LoginAsManagerAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            loginIdentifier = BackupImportWebApplicationFactory.ManagerEmail,
            password = BackupImportWebApplicationFactory.ManagerPassword,
            clientApp = "admin",
        });

        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("token").GetString()
            ?? throw new InvalidOperationException("Login response missing token.");
    }
}
