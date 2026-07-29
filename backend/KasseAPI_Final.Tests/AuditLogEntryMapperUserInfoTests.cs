using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using Xunit;

namespace KasseAPI_Final.Tests;

public class AuditLogEntryMapperUserInfoTests
{
    [Fact]
    public void ToDto_WhenUserNavigationLoaded_PopulatesUserAndFlatFields()
    {
        var userId = Guid.NewGuid().ToString();
        var log = new AuditLog
        {
            Id = Guid.NewGuid(),
            SessionId = "s1",
            UserId = userId,
            UserRole = "Manager",
            Action = AuditLogActions.USER_UPDATE,
            EntityType = AuditLogEntityTypes.USER,
            Status = AuditLogStatus.Success,
            Timestamp = DateTime.UtcNow,
            ActorDisplayName = "Stale Name",
            User = new ApplicationUser
            {
                Id = userId,
                UserName = "manager1",
                Email = "manager1@example.com",
                FirstName = "Anna",
                LastName = "Schmidt",
                Role = "Manager",
            },
        };

        var dto = AuditLogEntryMapper.ToDto(log);

        Assert.NotNull(dto.User);
        Assert.Equal(userId, dto.User!.Id);
        Assert.Equal("manager1", dto.User.UserName);
        Assert.Equal("manager1@example.com", dto.User.Email);
        Assert.Equal("Anna Schmidt", dto.User.DisplayName);
        Assert.Equal("Manager", dto.User.Role);
        Assert.Equal("manager1", dto.UserName);
        Assert.Equal("manager1@example.com", dto.UserEmail);
        Assert.Equal("Anna Schmidt", dto.UserDisplayName);
        Assert.Equal("Anna Schmidt", dto.ActorDisplayName);
    }

    [Fact]
    public void ToDto_WhenUserMissing_FallsBackToActorDisplayName()
    {
        var log = new AuditLog
        {
            Id = Guid.NewGuid(),
            SessionId = "s1",
            UserId = "deleted-user",
            UserRole = "Cashier",
            Action = AuditLogActions.USER_LOGIN,
            EntityType = AuditLogEntityTypes.USER,
            Status = AuditLogStatus.Success,
            Timestamp = DateTime.UtcNow,
            ActorDisplayName = "Former Cashier",
            User = null,
        };

        var dto = AuditLogEntryMapper.ToDto(log);

        Assert.Null(dto.User);
        Assert.Null(dto.UserName);
        Assert.Null(dto.UserEmail);
        Assert.Equal("Former Cashier", dto.ActorDisplayName);
        Assert.Equal("Former Cashier", dto.UserDisplayName);
    }

    [Fact]
    public void ToDto_WhenOverrideProvided_PrefersOverrideDisplayName()
    {
        var userId = Guid.NewGuid().ToString();
        var log = new AuditLog
        {
            Id = Guid.NewGuid(),
            SessionId = "s1",
            UserId = userId,
            UserRole = "Cashier",
            Action = AuditLogActions.USER_LOGIN,
            EntityType = AuditLogEntityTypes.USER,
            Status = AuditLogStatus.Success,
            Timestamp = DateTime.UtcNow,
            User = new ApplicationUser
            {
                Id = userId,
                UserName = "c1",
                FirstName = "Max",
                LastName = "Muster",
                Role = "Cashier",
            },
        };

        var dto = AuditLogEntryMapper.ToDto(log, actorDisplayName: "Override Name");

        Assert.Equal("Override Name", dto.ActorDisplayName);
        Assert.Equal("Override Name", dto.UserDisplayName);
        Assert.Equal("Max Muster", dto.User!.DisplayName);
    }
}
