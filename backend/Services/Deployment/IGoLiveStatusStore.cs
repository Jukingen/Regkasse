using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Deployment;

/// <summary>Process-local last go-live check result (not durable across restarts).</summary>
public interface IGoLiveStatusStore
{
    void Save(GoLiveStatusDto status);

    GoLiveStatusDto? GetLatest();
}
