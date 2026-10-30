using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KasseAPI_Final.Data;

internal sealed class OnlineOrderIntegrationEntityConfiguration : IEntityTypeConfiguration<OnlineOrderIntegration>
{
    public void Configure(EntityTypeBuilder<OnlineOrderIntegration> builder)
    {
        builder.Property(e => e.AttemptCount).HasDefaultValue(0);
        builder.Property(e => e.Status).HasDefaultValue(OnlineOrderIntegrationStatuses.Received);
        builder.Property(e => e.IsActive).HasDefaultValue(true);

        builder.HasIndex(e => new { e.TenantId, e.Provider, e.ExternalOrderId })
            .IsUnique()
            .HasDatabaseName("ux_online_order_integrations_tenant_provider_external");

        builder.HasIndex(e => e.OnlineOrderId)
            .HasDatabaseName("idx_online_order_integrations_online_order_id");

        builder.HasIndex(e => new { e.TenantId, e.Status, e.ReceivedAt })
            .HasDatabaseName("idx_online_order_integrations_tenant_status_received");

        builder.HasIndex(e => new { e.Status, e.NextAttemptAt })
            .HasDatabaseName("idx_online_order_integrations_status_next_attempt");

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<OnlineOrder>()
            .WithMany()
            .HasForeignKey(e => e.OnlineOrderId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class OnlineOrderIntegrationLocationEntityConfiguration : IEntityTypeConfiguration<OnlineOrderIntegrationLocation>
{
    public void Configure(EntityTypeBuilder<OnlineOrderIntegrationLocation> builder)
    {
        builder.Property(e => e.Enabled).HasDefaultValue(false);
        builder.Property(e => e.IsActive).HasDefaultValue(true);

        builder.HasIndex(e => new { e.Provider, e.ExternalLocationId })
            .IsUnique()
            .HasDatabaseName("ux_online_order_integration_locations_provider_location");

        builder.HasIndex(e => e.ClaimingUserId)
            .HasDatabaseName("idx_online_order_integration_locations_claiming_user");

        builder.HasIndex(e => e.DeliveryFeeProductId)
            .HasDatabaseName("idx_online_order_integration_locations_delivery_product");

        builder.HasIndex(e => e.ServiceFeeProductId)
            .HasDatabaseName("idx_online_order_integration_locations_service_product");

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(e => e.ClaimingUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(e => e.DeliveryFeeProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(e => e.ServiceFeeProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class OnlineOrderProductMappingEntityConfiguration : IEntityTypeConfiguration<OnlineOrderProductMapping>
{
    public void Configure(EntityTypeBuilder<OnlineOrderProductMapping> builder)
    {
        builder.Property(e => e.IsActive).HasDefaultValue(true);

        builder.HasIndex(e => new { e.TenantId, e.Provider, e.ExternalProductId })
            .IsUnique()
            .HasDatabaseName("ux_online_order_product_mappings_tenant_provider_external");

        builder.HasIndex(e => e.ProductId)
            .HasDatabaseName("idx_online_order_product_mappings_product");

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class OnlineOrderIntegrationOAuthTokenEntityConfiguration : IEntityTypeConfiguration<OnlineOrderIntegrationOAuthToken>
{
    public void Configure(EntityTypeBuilder<OnlineOrderIntegrationOAuthToken> builder)
    {
        builder.Property(e => e.IsActive).HasDefaultValue(true);

        builder.HasIndex(e => new { e.TenantId, e.Provider, e.ExternalLocationId })
            .IsUnique()
            .HasDatabaseName("ux_online_order_integration_oauth_tokens_tenant_provider_location");

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class OnlineOrderIntegrationProviderEntityConfiguration : IEntityTypeConfiguration<OnlineOrderIntegrationProvider>
{
    public void Configure(EntityTypeBuilder<OnlineOrderIntegrationProvider> builder)
    {
        builder.Property(e => e.Enabled).HasDefaultValue(false);
        builder.Property(e => e.IsActive).HasDefaultValue(true);
        builder.Property(e => e.CredentialOwner)
            .HasMaxLength(16)
            .HasDefaultValue(OnlineOrderIntegrationCredentialOwners.Deployment);

        builder.HasIndex(e => e.Provider)
            .IsUnique()
            .HasDatabaseName("ux_online_order_integration_providers_provider");
    }
}

internal sealed class OnlineOrderIntegrationTenantCredentialEntityConfiguration
    : IEntityTypeConfiguration<OnlineOrderIntegrationTenantCredential>
{
    public void Configure(EntityTypeBuilder<OnlineOrderIntegrationTenantCredential> builder)
    {
        builder.Property(e => e.IsActive).HasDefaultValue(true);

        builder.HasIndex(e => new { e.TenantId, e.Provider })
            .IsUnique()
            .HasDatabaseName("ux_online_order_integration_tenant_credentials_tenant_provider");

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
