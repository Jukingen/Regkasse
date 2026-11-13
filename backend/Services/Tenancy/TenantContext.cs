namespace KasseAPI_Final.Services.Tenancy;

/// <summary>Resolved mandant snapshot for the current request.</summary>
public sealed record TenantContext(Guid Id, string Slug, string Name);
