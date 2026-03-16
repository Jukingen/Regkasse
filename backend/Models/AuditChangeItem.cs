namespace KasseAPI_Final.Models
{
    /// <summary>
    /// Single field change for enterprise audit diff. Used in AuditLog.Changes JSON.
    /// Only whitelisted fields (firstName, lastName, email, userName, role, isActive, isDemo) are allowed.
    /// </summary>
    public class AuditChangeItem
    {
        public string Field { get; set; } = string.Empty;
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
    }
}
