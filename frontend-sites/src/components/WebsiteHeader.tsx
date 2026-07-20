import type { PublicTenantProfile } from '@/lib/publicApi';

export function WebsiteHeader({ tenant }: { tenant: PublicTenantProfile }) {
  return (
    <header
      style={{
        background: `linear-gradient(160deg, ${tenant.primaryColor} 0%, #0f172a 100%)`,
        color: '#fff',
        padding: '2.5rem 1.5rem 2rem',
      }}
    >
      <div style={{ maxWidth: 720, margin: '0 auto' }}>
        {tenant.logoUrl ? (
          // eslint-disable-next-line @next/next/no-img-element
          <img
            src={tenant.logoUrl}
            alt=""
            style={{ maxHeight: 72, borderRadius: 12, marginBottom: 16 }}
          />
        ) : null}
        <h1 style={{ margin: 0, fontSize: '2.25rem', letterSpacing: '-0.02em' }}>
          {tenant.displayName}
        </h1>
        {tenant.description ? (
          <p style={{ margin: '0.75rem 0 0', opacity: 0.9, maxWidth: '36rem', lineHeight: 1.5 }}>
            {tenant.description}
          </p>
        ) : null}
      </div>
    </header>
  );
}
