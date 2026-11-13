namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Birden fazla publisher (ör. log + webhook) sırayla çağrılır.
/// </summary>
public sealed class CompositeBackupAlertPublisher : IBackupAlertPublisher
{
    private readonly IReadOnlyList<IBackupAlertPublisher> _publishers;

    public CompositeBackupAlertPublisher(IEnumerable<IBackupAlertPublisher> publishers) =>
        _publishers = publishers.ToList();

    public void Publish(BackupAlertEvent evt)
    {
        foreach (var p in _publishers)
            p.Publish(evt);
    }
}
