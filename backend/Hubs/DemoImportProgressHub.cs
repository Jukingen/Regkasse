using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace KasseAPI_Final.Hubs;

[Authorize]
public sealed class DemoImportProgressHub : Hub
{
    private readonly IDemoProductImportJobManager _jobManager;

    public DemoImportProgressHub(IDemoProductImportJobManager jobManager)
    {
        _jobManager = jobManager;
    }

    public async Task SubscribeToJob(string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            throw new HubException("Job id is required.");

        if (!_jobManager.TryAuthorizeSubscription(Context.User, jobId))
            throw new HubException("Not allowed to subscribe to this import job.");

        await Groups.AddToGroupAsync(Context.ConnectionId, DemoProductImportJobManager.GroupName(jobId))
            .ConfigureAwait(false);

        var snapshot = _jobManager.GetProgress(jobId);
        if (snapshot != null)
            await Clients.Caller.SendAsync("ImportProgress", snapshot).ConfigureAwait(false);
    }
}
