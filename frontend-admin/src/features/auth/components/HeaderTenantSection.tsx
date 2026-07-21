'use client';

import { TenantBadge } from '@/components/admin-layout/TenantBadge';
import { HeaderDevTenantSwitch } from '@/features/auth/components/HeaderDevTenantSwitch';
import { HeaderTenantSwitcherProvider } from '@/features/auth/components/HeaderTenantSwitcherContext';

export type HeaderTenantSectionProps = {
  isMobile: boolean;
};

/** Groups tenant badge + dev switcher with shared dropdown open state. */
export function HeaderTenantSection({ isMobile }: HeaderTenantSectionProps) {
  return (
    <HeaderTenantSwitcherProvider>
      <div className="tenant-section">
        <TenantBadge compact={isMobile} />
        <HeaderDevTenantSwitch compact={isMobile} />
      </div>
    </HeaderTenantSwitcherProvider>
  );
}
