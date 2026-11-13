using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KasseAPI_Final.Data;

internal sealed class TenantFiskalyCredentialEntityConfiguration : IEntityTypeConfiguration<TenantFiskalyCredential>
{
    public void Configure(EntityTypeBuilder<TenantFiskalyCredential> builder)
    {
        builder.Property(e => e.IsActive).HasDefaultValue(true);
        builder.Property(e => e.Environment).HasDefaultValue("TEST");
        builder.Property(e => e.IssuerCertificatesDerBase64)
            .HasColumnType("jsonb");

        builder.HasIndex(e => e.TenantId)
            .IsUnique()
            .HasDatabaseName("ux_tenant_fiskaly_credentials_tenant_id");

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
