'use client';

import { QuestionCircleOutlined } from '@ant-design/icons';
import { Tag, Tooltip } from 'antd';
import React from 'react';

import {
  TENANT_GRACE_PERIOD_DAYS,
  clampTenantGraceRemaining,
} from '@/features/license/constants/licenseGracePeriod';
import {
  type LicenseStatus,
  useTenantLicenseStatus,
} from '@/features/license/hooks/useLicenseStatus';
import {
  getLicenseStatusMessage,
  resolveTenantRowLicenseStatus,
} from '@/features/license/utils/licenseStatus';
import { LicenseStatusBadge } from '@/features/tenants/components/LicenseStatusBadge';
import { useI18n } from '@/i18n';

export type TenantLicenseBadgeProps = {
  tenantId?: string;
  licenseValidUntilUtc?: string | null;
  licenseKey?: string | null;
  licenseDaysRemaining?: number | null;
};

export function TenantLicenseBadge({
  tenantId,
  licenseValidUntilUtc,
  licenseKey,
  licenseDaysRemaining,
}: TenantLicenseBadgeProps) {
  const { t } = useI18n();
  const { data: remoteStatus } = useTenantLicenseStatus(tenantId);

  const fallbackStatus = resolveTenantRowLicenseStatus({
    licenseValidUntilUtc,
    licenseKey,
    licenseDaysRemaining,
  });
  const status: LicenseStatus = remoteStatus ?? {
    ...fallbackStatus,
    daysRemainingInGrace:
      fallbackStatus.kind === 'grace_write'
        ? clampTenantGraceRemaining(TENANT_GRACE_PERIOD_DAYS - fallbackStatus.daysExpired)
        : 0,
    isExpired:
      fallbackStatus.kind === 'grace_write' ||
      fallbackStatus.kind === 'grace_readonly' ||
      fallbackStatus.kind === 'lockdown' ||
      fallbackStatus.kind === 'expired',
    isLocked: fallbackStatus.kind === 'lockdown' || fallbackStatus.kind === 'expired',
    lockDate: null,
    message: getLicenseStatusMessage(fallbackStatus, 'tenant', t),
  };

  if (status.kind === 'no_license' && !licenseValidUntilUtc?.trim() && !licenseKey?.trim()) {
    return (
      <Tooltip title={status.message}>
        <Tag color="default" icon={<QuestionCircleOutlined />}>
          Keine Lizenz
        </Tag>
      </Tooltip>
    );
  }

  return (
    <LicenseStatusBadge
      validUntil={licenseValidUntilUtc ?? null}
      isInGracePeriod={status.kind === 'grace_write'}
      isLockdown={status.kind === 'lockdown'}
      daysRemaining={status.daysRemaining}
      gracePeriodRemaining={
        status.kind === 'grace_write'
          ? clampTenantGraceRemaining(TENANT_GRACE_PERIOD_DAYS - status.daysExpired)
          : undefined
      }
    />
  );
}
