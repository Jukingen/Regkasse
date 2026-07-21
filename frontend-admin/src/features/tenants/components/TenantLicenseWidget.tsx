'use client';

import { Alert, Descriptions, Skeleton, Typography } from 'antd';
import Link from 'next/link';
import React from 'react';

import type { WidgetShellProps } from '@/features/dashboard/components/WidgetShell';
import { WidgetShell } from '@/features/dashboard/components/WidgetShell';
import {
  TENANT_GRACE_PERIOD_DAYS,
  clampTenantGraceRemaining,
} from '@/features/license/constants/licenseGracePeriod';
import { resolveTenantLicenseStatus } from '@/features/license/utils/licenseStatus';
import { getAdminTenantLicense } from '@/features/super-admin/api/adminTenantLicense';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { LicenseStatusBadge } from '@/features/tenants/components/LicenseStatusBadge';
import { useAuthorizedQuery } from '@/hooks/useAuthorizedQuery';
import { useI18n } from '@/i18n/I18nProvider';
import { formatDate } from '@/i18n/formatting';

export type TenantLicenseWidgetProps = {
  tenantId?: string;
  title?: string;
  dragHandleProps?: WidgetShellProps['dragHandleProps'];
  onRefresh?: () => void;
  /** When false, render content without dashboard card chrome. */
  asWidget?: boolean;
};

export function TenantLicenseWidget({
  tenantId,
  title = 'Mandantenlizenz',
  dragHandleProps,
  onRefresh,
  asWidget = true,
}: TenantLicenseWidgetProps) {
  const { formatLocale } = useI18n();
  const currentTenant = useCurrentTenant();
  const resolvedTenantId = tenantId ?? currentTenant.tenantId ?? undefined;

  const query = useAuthorizedQuery({
    queryKey: ['tenant-license', resolvedTenantId],
    queryFn: () => getAdminTenantLicense(resolvedTenantId!),
    requiredRole: 'SuperAdmin',
    enabled: Boolean(resolvedTenantId),
    staleTime: 60_000,
  });

  if (!query.isAuthorized) {
    return null;
  }

  const handleRefresh = () => {
    void query.refetch();
    onRefresh?.();
  };

  const body = (() => {
    if (!resolvedTenantId) {
      return (
        <Alert
          type="info"
          showIcon
          title="Kein Mandant ausgewählt"
          description="Wählen Sie einen Mandanten, um die Lizenzdetails anzuzeigen."
        />
      );
    }

    if (query.isLoading) {
      return <Skeleton active paragraph={{ rows: 3 }} />;
    }

    if (query.isError || !query.data) {
      return (
        <Alert
          type="error"
          showIcon
          title="Lizenzdaten konnten nicht geladen werden"
          description="Bitte versuchen Sie es später erneut."
        />
      );
    }

    const status = resolveTenantLicenseStatus(query.data.status);
    const validUntil = query.data.status.validUntilUtc ?? null;

    return (
      <>
        <LicenseStatusBadge
          validUntil={validUntil}
          isInGracePeriod={status.kind === 'grace_write'}
          isLockdown={status.kind === 'lockdown'}
          daysRemaining={status.daysRemaining}
          gracePeriodRemaining={
            status.kind === 'grace_write'
              ? clampTenantGraceRemaining(TENANT_GRACE_PERIOD_DAYS - status.daysExpired)
              : undefined
          }
        />
        <Descriptions
          size="small"
          column={1}
          style={{ marginTop: 16 }}
          items={[
            {
              key: 'validUntil',
              label: 'Gültig bis',
              children: validUntil ? formatDate(validUntil, formatLocale) : '—',
            },
            {
              key: 'tier',
              label: 'Stufe',
              children: query.data.status.tier ?? '—',
            },
            {
              key: 'history',
              label: 'Historie',
              children: `${query.data.history.length} Einträge`,
            },
          ]}
        />
        <Typography.Paragraph type="secondary" style={{ marginTop: 12, marginBottom: 0 }}>
          <Link href={`/admin/tenants/${resolvedTenantId}?tab=license`}>Lizenz verwalten</Link>
        </Typography.Paragraph>
      </>
    );
  })();

  if (!asWidget) {
    return body;
  }

  return (
    <WidgetShell
      title={title}
      dragHandleProps={dragHandleProps}
      onRefresh={handleRefresh}
      refreshing={query.isFetching}
    >
      {body}
    </WidgetShell>
  );
}
