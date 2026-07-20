'use client';

import { WebsiteFooter } from '@/components/WebsiteFooter';
import { WebsiteHeader } from '@/components/WebsiteHeader';
import { OrderStatusWidget } from '@/components/OrderStatus';
import { MenuSection } from '@/components/MenuSection';
import { useWebsiteStatus } from '@/hooks/useWebsiteStatus';
import type { PublicTenantMenu, PublicTenantProfile } from '@/lib/publicApi';

type Props = {
  slug: string;
  tenant: PublicTenantProfile;
  menu: PublicTenantMenu;
};

/**
 * Client tenant website shell.
 * Working hours gate online orders only — never used by POS/FA.
 */
export function TenantWebsite({ slug, tenant, menu }: Props) {
  const { data: status, loading } = useWebsiteStatus(slug);

  return (
    <div
      style={{
        ['--primary' as string]: tenant.primaryColor,
        ['--accent' as string]: tenant.accentColor,
        minHeight: '100vh',
        display: 'flex',
        flexDirection: 'column',
      }}
    >
      <WebsiteHeader tenant={tenant} />

      <main style={{ flex: 1, width: '100%', maxWidth: 720, margin: '0 auto', padding: '1.5rem' }}>
        {/* Website: show open / closed / cutoff status */}
        <OrderStatusWidget status={status} loading={loading} />

        {/* Website: disable order buttons when closed */}
        <MenuSection
          tenantSlug={slug}
          menu={menu}
          status={status}
          canOrder={status?.canOrder}
          closeTime={status?.closeTime}
          loadingStatus={loading}
        />
      </main>

      <WebsiteFooter tenant={tenant} />
    </div>
  );
}

export default TenantWebsite;
