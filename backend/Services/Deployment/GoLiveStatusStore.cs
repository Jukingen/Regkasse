using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Deployment;

public sealed class GoLiveStatusStore : IGoLiveStatusStore
{
    private readonly object _gate = new();
    private GoLiveStatusDto? _latest;

    public void Save(GoLiveStatusDto status)
    {
        ArgumentNullException.ThrowIfNull(status);
        lock (_gate)
            _latest = Clone(status);
    }

    public GoLiveStatusDto? GetLatest()
    {
        lock (_gate)
            return _latest is null ? null : Clone(_latest);
    }

    private static GoLiveStatusDto Clone(GoLiveStatusDto source) => new()
    {
        Status = source.Status,
        CheckedAtUtc = source.CheckedAtUtc,
        Summary = source.Summary,
        Checks = source.Checks
            .Select(c => new GoLiveCheckDto
            {
                Name = c.Name,
                Category = c.Category,
                Passed = c.Passed,
                Details = c.Details,
                Remediation = c.Remediation,
            })
            .ToList(),
    };
}
