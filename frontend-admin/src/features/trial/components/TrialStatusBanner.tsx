'use client';

import { Alert, Button, Space } from 'antd';
import { useRouter } from 'next/navigation';
import React from 'react';

import { getTenantLicense } from '@/features/license/api/tenantLicense';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useAuthorizedQuery } from '@/hooks/useAuthorizedQuery';
import { useI18n } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';

export type TrialBannerTenant = {
  trialStatus?: string | null;
  trialEndsAtUtc?: string | null;
  trialDaysRemaining?: number | null;
  trialGracePeriodEndsAtUtc?: string | null;
};

function resolveDaysLeft(tenant: TrialBannerTenant): number | null {
  if (typeof tenant.trialDaysRemaining === 'number' && Number.isFinite(tenant.trialDaysRemaining)) {
    return tenant.trialDaysRemaining;
  }
  if (!tenant.trialEndsAtUtc) return null;
  const ends = new Date(tenant.trialEndsAtUtc).getTime();
  if (Number.isNaN(ends)) return null;
  return Math.ceil((ends - Date.now()) / (24 * 60 * 60 * 1000));
}

export function resolveTrialBannerVariant(
  tenant: TrialBannerTenant
): 'info' | 'warning' | 'expiredActive' | 'expired' | null {
  const status = (tenant.trialStatus ?? '').toLowerCase();
  if (!status || status === 'converted' || status === 'deleted') return null;

  const daysLeft = resolveDaysLeft(tenant);
  if (status === 'expired') return 'expired';
  if (daysLeft == null) return null;
  if (daysLeft > 7) return 'info';
  if (daysLeft > 0) return 'warning';
  if (status === 'active') return 'expiredActive';
  return 'expired';
}

type TrialStatusBannerProps = {
  /** When provided (tenant detail), skip ambient license fetch. */
  tenant?: TrialBannerTenant | null;
  upgradeHref?: string;
};

export function TrialStatusBanner({ tenant: tenantProp, upgradeHref }: TrialStatusBannerProps) {
  const { t } = useI18n();
  const router = useRouter();
  const current = useCurrentTenant();

  const licenseQuery = useAuthorizedQuery({
    queryKey: ['admin', 'tenant-license', 'trial-banner', current.tenantId],
    queryFn: () => getTenantLicense(current.tenantId!),
    requiredPermission: [PERMISSIONS.LICENSE_VIEW, PERMISSIONS.LICENSE_MANAGE, PERMISSIONS.SYSTEM_CRITICAL],
    enabled: !tenantProp && Boolean(current.tenantId && current.isRealTenantSlug && current.hasAuthToken),
    staleTime: 60_000,
  });

  const tenant: TrialBannerTenant | null = tenantProp
    ? tenantProp
    : licenseQuery.data?.status
      ? {
          trialStatus: licenseQuery.data.status.trialStatus,
          trialEndsAtUtc: licenseQuery.data.status.trialEndsAtUtc,
          trialDaysRemaining: licenseQuery.data.status.trialDaysRemaining,
          trialGracePeriodEndsAtUtc: licenseQuery.data.status.trialGracePeriodEndsAtUtc,
        }
      : null;

  if (!tenant) return null;
  if (current.suppressLicenseWarnings && !tenantProp) return null;

  const variant = resolveTrialBannerVariant(tenant);
  if (!variant) return null;

  const daysLeft = resolveDaysLeft(tenant) ?? 0;
  const href = upgradeHref ?? '/admin/license-management';

  const upgradeAction = (
    <Button size="small" type="primary" onClick={() => router.push(href)}>
      {t('trials.banner.upgradeNow')}
    </Button>
  );

  let type: 'info' | 'warning' | 'error' = 'info';
  let message: string = t('trials.banner.info', { days: daysLeft });

  if (variant === 'warning') {
    type = 'warning';
    message = t('trials.banner.warning', { days: daysLeft });
  } else if (variant === 'expiredActive' || variant === 'expired') {
    type = 'error';
    message =
      variant === 'expired'
        ? t('trials.banner.expiredGrace')
        : t('trials.banner.expired');
  }

  return (
    <Alert
      banner
      showIcon
      type={type}
      style={{ marginBottom: 12 }}
      title={message}
      description={
        <Space size="small" wrap>
          {upgradeAction}
        </Space>
      }
    />
  );
}
