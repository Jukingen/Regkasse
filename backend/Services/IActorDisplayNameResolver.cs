namespace KasseAPI_Final.Services
{
    /// <summary>Resolves actor user IDs to display names (e.g. for audit log UI). Implemented via UserManager.</summary>
    public interface IActorDisplayNameResolver
    {
        Task<IReadOnlyDictionary<string, string>> ResolveAsync(IList<string> userIds);

        /// <summary>
        /// Resolves login labels for readable logs: email, else username, else omitted (caller uses <c>unknown</c>).
        /// </summary>
        Task<IReadOnlyDictionary<string, string>> ResolveLoginLabelsAsync(IList<string> userIds);
    }
}
