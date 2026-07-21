using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// HTTP integration: Manager imports a dump for own tenant; list + tenant-scoped audit are updated.
/// </summary>
[Collection("BackupHttpIntegration")]
public sealed class BackupArtifactImportIntegrationTests
{
    private readonly BackupImportWebApplicationFactory _factory;

    public BackupArtifactImportIntegrationTests(BackupImportWebApplicationFactory factory) =>
        _factory = factory;

    [Fact]
    public async Task Manager_imports_dump_registers_list_entry_and_tenant_audit()
    {
        var client = await CreateAuthenticatedManagerClientAsync(BackupImportWebApplicationFactory.TenantASlug);

        using var form = new MultipartFormDataContent();
        var dumpBytes = Encoding.UTF8.GetBytes("-- PostgreSQL custom dump stub\nSELECT 1;\n");
        form.Add(new ByteArrayContent(dumpBytes), "dumpFile", "backup_tenant-a_20260101_120000.dump");

        var importResponse = await client.PostAsync("/api/admin/backup/artifacts/import", form);
        Assert.Equal(HttpStatusCode.Created, importResponse.StatusCode);

        var imported = await importResponse.Content.ReadFromJsonAsync<BackupArtifactImportResponseDto>();
        Assert.NotNull(imported);
        Assert.NotEqual(Guid.Empty, imported!.BackupRunId);
        Assert.NotEqual(Guid.Empty, imported.ArtifactId);
        Assert.True(imported.ByteSize > 0);
        Assert.Contains("backup_", imported.FileName, StringComparison.OrdinalIgnoreCase);

        var dumpPath = Path.Combine(_factory.StagingRoot, imported.FileName);
        Assert.True(File.Exists(dumpPath), "Imported dump should exist on staging disk.");

        var listResponse = await client.GetAsync("/api/admin/backup/list");
        listResponse.EnsureSuccessStatusCode();
        var list = await listResponse.Content.ReadFromJsonAsync<List<BackupListItemResponseDto>>();
        Assert.NotNull(list);
        Assert.Contains(list!, row => row.BackupRunId == imported.BackupRunId && row.ArtifactId == imported.ArtifactId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var run = await db.BackupRuns.AsNoTracking()
            .Include(r => r.Artifacts)
            .FirstAsync(r => r.Id == imported.BackupRunId);
        Assert.Equal(BackupRunStatus.Succeeded, run.Status);
        Assert.Equal(BackupTriggerSource.OperatorApi, run.TriggerSource);
        Assert.Equal(BackupArtifactImportService.ImportedAdapterKind, run.AdapterKind);
        Assert.Contains($"import-tenant-{BackupImportWebApplicationFactory.TenantAId}", run.IdempotencyKey!, StringComparison.OrdinalIgnoreCase);

        var audit = await db.AuditLogs.IgnoreQueryFilters()
            .Where(a => a.Action == "BACKUP_ARTIFACT_IMPORTED" && a.TenantId == BackupImportWebApplicationFactory.TenantAId)
            .OrderByDescending(a => a.Timestamp)
            .FirstOrDefaultAsync();
        Assert.NotNull(audit);
        Assert.Equal("BackupRun", audit!.EntityType);
        Assert.Equal(imported.BackupRunId, audit.EntityId);
    }

    [Fact]
    public async Task Import_without_dump_file_returns_bad_request()
    {
        var client = await CreateAuthenticatedManagerClientAsync(BackupImportWebApplicationFactory.TenantASlug);

        using var form = new MultipartFormDataContent();
        var response = await client.PostAsync("/api/admin/backup/artifacts/import", form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Import_with_optional_manifest_registers_both_artifacts()
    {
        var client = await CreateAuthenticatedManagerClientAsync(BackupImportWebApplicationFactory.TenantASlug);

        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent("-- dump\n"u8.ToArray()), "dumpFile", "backup_tenant-a_20260102_120000.dump");
        form.Add(new ByteArrayContent("{\"tables\":[]}"u8.ToArray()), "manifestFile", "backup_tenant-a_20260102_120000_manifest.json");

        var importResponse = await client.PostAsync("/api/admin/backup/artifacts/import", form);
        Assert.Equal(HttpStatusCode.Created, importResponse.StatusCode);

        var imported = await importResponse.Content.ReadFromJsonAsync<BackupArtifactImportResponseDto>();
        Assert.NotNull(imported);
        Assert.NotNull(imported!.ManifestArtifactId);
        Assert.False(string.IsNullOrWhiteSpace(imported.ManifestFileName));
        Assert.True(File.Exists(Path.Combine(_factory.StagingRoot, imported.ManifestFileName!)));
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
