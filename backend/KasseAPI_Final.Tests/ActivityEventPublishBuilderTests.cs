using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class ActivityEventPublishBuilderTests
{
    [Fact]
    public void FromMetadata_maps_user_created_fields()
    {
        var request = ActivityEventPublishBuilder.FromMetadata(
            Guid.NewGuid(),
            ActivityEventType.UserCreated,
            new { UserId = "u1", UserEmail = "a@b.c", Role = "Admin", ActorId = "actor" },
            actorUserId: "actor");

        Assert.Equal("User created", request.Title);
        Assert.Equal("user", request.EntityType);
        Assert.Equal("u1", request.EntityId);
        Assert.Equal("actor", request.ActorUserId);
        Assert.NotNull(request.Metadata);
        Assert.Equal("a@b.c", request.Metadata!["UserEmail"].ToString());
    }

    [Fact]
    public void FromMetadata_maps_backup_succeeded_metadata()
    {
        var runId = Guid.NewGuid();
        var request = ActivityEventPublishBuilder.FromMetadata(
            Guid.NewGuid(),
            ActivityEventType.BackupSucceeded,
            new { BackupRunId = runId, DurationSeconds = 12, ArtifactSize = 1024L });

        Assert.Equal("Backup succeeded", request.Title);
        Assert.Equal("backup_run", request.EntityType);
        Assert.Equal(runId.ToString(), request.EntityId);
    }

    [Fact]
    public void FromMetadata_maps_role_permission_update_with_what_changed()
    {
        var request = ActivityEventPublishBuilder.FromMetadata(
            Guid.NewGuid(),
            ActivityEventType.RolePermissionsUpdated,
            new
            {
                RoleName = "Cashier",
                ActorName = "Admin",
                WhatChanged = "Added: reports.view · Removed: settings.manage",
            },
            actorUserId: "actor");

        Assert.Equal("Admin updated role 'Cashier'", request.Title);
        Assert.Equal("Added: reports.view · Removed: settings.manage", request.Description);
        Assert.Equal("role", request.EntityType);
        Assert.Equal("Cashier", request.EntityId);
    }

    [Fact]
    public void FromMetadata_maps_role_deleted_title()
    {
        var request = ActivityEventPublishBuilder.FromMetadata(
            Guid.NewGuid(),
            ActivityEventType.RoleDeleted,
            new { RoleName = "TempRole", ActorName = "Admin", WhatChanged = "Role deleted." },
            actorUserId: "actor");

        Assert.Equal("Admin deleted role 'TempRole'", request.Title);
        Assert.Equal(ActivityEventType.RoleDeleted, request.Type);
    }
}
