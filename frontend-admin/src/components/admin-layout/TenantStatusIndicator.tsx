'use client';

import { CheckCircleOutlined, WarningOutlined } from '@ant-design/icons';
import { Tag } from 'antd';

import { useTenant } from '@/hooks/useTenant';
import { useI18n } from '@/i18n';

/**
 * Compact header chip: active mandant or “none selected”.
 * Complements {@link TenantBadge} with an explicit success/warning status.
 */
export function TenantStatusIndicator() {
  const { t } = useI18n();
  const { tenant, isLoading } = useTenant();

  if (isLoading) {
    return null;
  }

  if (!tenant) {
    return (
      <Tag
        color="warning"
        icon={<WarningOutlined />}
        className="tenant-status-indicator"
        aria-label={t('adminShell.tenant.status.noneSelected')}
      >
        {t('adminShell.tenant.status.noneSelected')}
      </Tag>
    );
  }

  return (
    <Tag
      color="success"
      icon={<CheckCircleOutlined />}
      className="tenant-status-indicator"
      aria-label={t('adminShell.tenant.status.active', {
        name: tenant.name,
        slug: tenant.slug,
      })}
    >
      {t('adminShell.tenant.status.active', {
        name: tenant.name,
        slug: tenant.slug,
      })}
    </Tag>
  );
}
