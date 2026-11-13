'use client';

import { TenantBadge } from '@/components/admin-layout/TenantBadge';
import { TenantStatusIndicator } from '@/components/admin-layout/TenantStatusIndicator';
import { HeaderDevTenantSwitch } from '@/features/auth/components/HeaderDevTenantSwitch';
import { HeaderTenantSwitcherProvider } from '@/features/auth/components/HeaderTenantSwitcherContext';

export type HeaderTenantSectionProps = {
  isMobile: boolean;
};

/** Groups tenant badge + status + dev switcher with shared dropdown open state. */
export function HeaderTenantSection({ isMobile }: HeaderTenantSectionProps) {
  return (
    <HeaderTenantSwitcherProvider>
      <div className="tenant-section">
        <TenantBadge compact={isMobile} />
        {!isMobile ? <TenantStatusIndicator /> : null}
        <HeaderDevTenantSwitch compact={isMobile} />
      </div>
    </HeaderTenantSwitcherProvider>
  );
}
