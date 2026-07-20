using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.Auth;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AccountLockoutServiceTests
{
    [Fact]
    public void RecordFailedAttempt_locks_after_max_attempts()
    {
        var service = CreateService(maxAttempts: 3);

        Assert.False(service.IsLockedOut("cashier1"));
        service.RecordFailedAttempt("cashier1");
        service.RecordFailedAttempt("Cashier1");
        Assert.False(service.IsLockedOut("cashier1"));
        service.RecordFailedAttempt("CASHIER1");
        Assert.True(service.IsLockedOut("cashier1"));
    }

    [Fact]
    public void ResetAttempts_clears_lockout()
    {
        var service = CreateService(maxAttempts: 2);
        service.RecordFailedAttempt("user@test.com");
        service.RecordFailedAttempt("user@test.com");
        Assert.True(service.IsLockedOut("user@test.com"));

        service.ResetAttempts("user@test.com");
        Assert.False(service.IsLockedOut("user@test.com"));
    }

    [Fact]
    public void Disabled_options_are_noop()
    {
        var service = CreateService(maxAttempts: 1, enabled: false);
        service.RecordFailedAttempt("user");
        Assert.False(service.IsLockedOut("user"));
    }

    private static AccountLockoutService CreateService(int maxAttempts = 5, bool enabled = true)
    {
        var monitor = new OptionsMonitorStub(new AccountLockoutOptions
        {
            Enabled = enabled,
            MaxAttempts = maxAttempts,
            LockoutMinutes = 15,
        });
        return new AccountLockoutService(new MemoryCache(new MemoryCacheOptions()), monitor);
    }

    private sealed class OptionsMonitorStub : IOptionsMonitor<AccountLockoutOptions>
    {
        public OptionsMonitorStub(AccountLockoutOptions current) => CurrentValue = current;
        public AccountLockoutOptions CurrentValue { get; }
        public AccountLockoutOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<AccountLockoutOptions, string?> listener) => null;
    }
}
