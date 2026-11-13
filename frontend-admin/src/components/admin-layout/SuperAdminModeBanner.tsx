'use client';

import { Alert, Button } from 'antd';
import Link from 'next/link';

import { useSuperAdminTenantMode } from '@/features/tenancy/hooks/useSuperAdminTenantMode';
import { useI18n } from '@/i18n';

export function SuperAdminModeBanner() {
  const { t } = useI18n();
  const { requiresTenantSelection, hasAuthToken } = useSuperAdminTenantMode();

  if (!hasAuthToken || !requiresTenantSelection) {
    return null;
  }

  return (
    <Alert
      type="warning"
      showIcon
      style={{ marginBottom: 12 }}
      title={t('superadmin.noTenant.banner')}
      description={t('superadmin.noTenant.message')}
      action={
        <Link href="/admin">
          <Button size="small" type="primary">
            {t('superadmin.selectTenant')}
          </Button>
        </Link>
      }
    />
  );
}
