'use client';

import { Alert, Button, Spin } from 'antd';
import Link from 'next/link';
import type { ReactNode } from 'react';

import { useTenant } from '@/hooks/useTenant';
import { useI18n } from '@/i18n';

/**
 * TSE admin section requires an active mandant context.
 * Super Admin without selection is blocked by {@link TenantGuard}; this layout is defense-in-depth.
 */
export default function TseAdminLayout({ children }: { children: ReactNode }) {
  const { t } = useI18n();
  const { tenant, isLoading } = useTenant();

  if (isLoading) {
    return (
      <div style={{ display: 'flex', justifyContent: 'center', padding: 48 }}>
        <Spin />
      </div>
    );
  }

  if (!tenant) {
    return (
      <div style={{ padding: 32, maxWidth: 640, margin: '0 auto' }}>
        <Alert
          type="warning"
          showIcon
          title={t('adminShell.tenant.tseLayout.title')}
          description={t('adminShell.tenant.tseLayout.body')}
          action={
            <Link href="/admin/tenants">
              <Button type="primary" size="small">
                {t('adminShell.tenant.superAdminPromptAction')}
              </Button>
            </Link>
          }
        />
      </div>
    );
  }

  return <>{children}</>;
}
