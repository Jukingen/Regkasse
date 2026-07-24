'use client';

import { Alert, Tag } from 'antd';

import { useTsePageTenant } from '@/features/tse-shared/hooks/useTsePageTenant';
import { useI18n } from '@/i18n';

/** Compact active-mandant chip for AdminPageHeader `extra`. */
export function TseActiveTenantTag() {
  const { t } = useI18n();
  const { tenant, isReady } = useTsePageTenant();

  if (!isReady || !tenant) {
    return null;
  }

  return (
    <Tag color="blue" style={{ marginInlineEnd: 0 }}>
      {t('adminShell.tenant.tseLayout.activeTag', {
        name: tenant.name,
        slug: tenant.slug,
      })}
    </Tag>
  );
}

type TseTenantRequiredAlertProps = {
  /** Page-specific empty-select title key, e.g. `tseAutoHealing.emptySelect`. */
  emptySelectKey: string;
};

/** Fallback when layout/TenantGuard did not block yet. */
export function TseTenantRequiredAlert({ emptySelectKey }: TseTenantRequiredAlertProps) {
  const { t } = useI18n();
  return (
    <Alert
      type="warning"
      showIcon
      title={t(emptySelectKey)}
      description={t('adminShell.tenant.tseLayout.selectFromHeader')}
    />
  );
}
