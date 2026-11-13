import type { PublicTenantProfile } from '@/lib/publicApi';

export function WebsiteFooter({ tenant }: { tenant: PublicTenantProfile }) {
  return (
    <footer
      style={{
        borderTop: '1px solid #e2e8f0',
        padding: '1.5rem',
        color: '#64748b',
        fontSize: '0.9rem',
      }}
    >
      <div style={{ maxWidth: 720, margin: '0 auto', lineHeight: 1.6 }}>
        {tenant.address ? <div>{tenant.address}</div> : null}
        {tenant.phone ? <div>Tel: {tenant.phone}</div> : null}
        {tenant.email ? <div>{tenant.email}</div> : null}
        <div style={{ marginTop: 8, opacity: 0.75 }}>Powered by Regkasse</div>
      </div>
    </footer>
  );
}
