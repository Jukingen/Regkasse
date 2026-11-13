'use client';

import type { PublicTenantMenu, WebsiteStatus } from '@/lib/publicApi';
import { OnlineOrderPanel } from '@/components/OnlineOrderPanel';

type Props = {
  tenantSlug: string;
  menu: PublicTenantMenu;
  /** Live website status — drives order CTAs (never POS/FA). */
  status?: WebsiteStatus | null;
  loadingStatus?: boolean;
  /** Convenience aliases when parent only has partial fields. */
  canOrder?: boolean;
  closeTime?: string | null;
};

/**
 * Menu + cart section. Order CTAs disabled when `canOrder` is false (website only).
 */
export function MenuSection({
  tenantSlug,
  menu,
  status = null,
  loadingStatus = false,
  canOrder,
  closeTime,
}: Props) {
  const resolved: WebsiteStatus | null =
    status ??
    (canOrder !== undefined
      ? {
          isOpen: canOrder === true || Boolean(closeTime),
          canOrder: canOrder === true,
          message:
            canOrder === true
              ? 'Online-Bestellung möglich'
              : 'Online-Bestellungen sind derzeit nicht möglich.',
          openTime: null,
          closeTime: closeTime ?? null,
          isSpecial: false,
        }
      : null);

  return (
    <OnlineOrderPanel
      tenantSlug={tenantSlug}
      menu={menu}
      status={resolved}
      loadingStatus={loadingStatus}
    />
  );
}
