'use client';

/**
 * Mandant license grace / lock banner for protected FA pages.
 * Shows remaining grace days, POS lock date, and renewal CTA.
 */
import { LockOutlined, WarningOutlined } from '@ant-design/icons';
import { Alert, Button, Flex, Typography } from 'antd';
import { useRouter } from 'next/navigation';
import { useMemo } from 'react';

import {
  TENANT_GRACE_PERIOD_DAYS,
  clampTenantGraceRemaining,
} from '@/features/license/constants/licenseGracePeriod';
import {
  type LicenseStatus,
  useTenantLicenseStatus,
} from '@/features/license/hooks/useLicenseStatus';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useI18n } from '@/i18n';
import { formatDate } from '@/i18n/formatting';

const { Text } = Typography;

function isAdminOnlyLicenseIssue(kind: LicenseStatus['kind']): boolean {
  // Missing license config is SuperAdmin-facing; grace/lock warnings are operational for all roles.
  return kind === 'no_license';
}

function resolveLockDateLabel(
  license: LicenseStatus,
  formatLocale: string,
  daysRemainingInGrace: number
): string {
  if (license.lockDate) {
    return formatDate(license.lockDate, formatLocale);
  }
  const lock = new Date();
  lock.setUTCDate(lock.getUTCDate() + Math.max(0, daysRemainingInGrace));
  return formatDate(lock.toISOString(), formatLocale);
}

export function LicenseBanner() {
  const router = useRouter();
  const { t, formatLocale } = useI18n();
  const tenant = useCurrentTenant();
  const { data: license } = useTenantLicenseStatus();

  const view = useMemo(() => {
    if (!license || tenant.suppressLicenseWarnings) return null;
    if (!tenant.isRealTenantSlug) return null;
    if (license.kind === 'active') return null;
    if (!tenant.isSuperAdminUser && isAdminOnlyLicenseIssue(license.kind)) {
      return null;
    }
    if (
      license.kind !== 'grace_write' &&
      license.kind !== 'lockdown' &&
      license.kind !== 'no_license'
    ) {
      return null;
    }

    const isLocked = license.isLocked || license.kind === 'lockdown';
    const daysRemainingInGrace = clampTenantGraceRemaining(
      license.daysRemainingInGrace > 0
        ? license.daysRemainingInGrace
        : TENANT_GRACE_PERIOD_DAYS - license.daysExpired
    );

    return { license, isLocked, daysRemainingInGrace };
  }, [license, tenant.isRealTenantSlug, tenant.isSuperAdminUser, tenant.suppressLicenseWarnings]);

  if (!view) return null;

  const { license: status, isLocked, daysRemainingInGrace } = view;
  const lockDateLabel = resolveLockDateLabel(status, formatLocale, daysRemainingInGrace);

  const title = isLocked
    ? t('license.banner.tenant.lockdown.title')
    : status.kind === 'no_license'
      ? t('license.banner.tenant.noLicense.title')
      : t('license.banner.tenant.graceWrite.title');

  const description =
    status.kind === 'no_license'
      ? t('license.banner.tenant.noLicense.adminDescription')
      : isLocked
        ? tenant.isSuperAdminUser
          ? t('license.banner.tenant.lockdown.adminDescription', {
              daysExpired: status.daysExpired,
            })
          : t('license.banner.tenant.lockdown.contactAdminDescription')
        : tenant.isSuperAdminUser
          ? t('license.banner.tenant.graceWrite.adminDescription', {
              daysExpired: status.daysExpired,
              daysRemaining: daysRemainingInGrace,
              lockDate: lockDateLabel,
            })
          : t('license.banner.tenant.graceWrite.contactAdminDescription', {
              daysExpired: status.daysExpired,
              daysRemaining: daysRemainingInGrace,
              lockDate: lockDateLabel,
            });

  const openLicensePage = () => {
    router.push('/admin/license');
  };

  return (
    <Alert
      type={isLocked || status.kind === 'no_license' ? 'error' : 'warning'}
      banner
      showIcon
      icon={isLocked ? <LockOutlined /> : <WarningOutlined />}
      style={{ marginBottom: 12 }}
      title={title}
      description={
        <Flex vertical gap={8}>
          <Text>{description}</Text>
          {status.kind !== 'no_license' ? (
            <Flex gap={16} wrap="wrap">
              {!isLocked ? (
                <>
                  <Text type="secondary">
                    {t('license.banner.tenant.details.graceRemaining', {
                      days: daysRemainingInGrace,
                    })}
                  </Text>
                  <Text type="secondary">
                    {t('license.banner.tenant.details.lockDate', {
                      date: lockDateLabel,
                    })}
                  </Text>
                </>
              ) : (
                <Text type="secondary">{t('license.banner.tenant.details.contactAdmin')}</Text>
              )}
            </Flex>
          ) : null}
          <Text type="secondary">
            {isLocked
              ? t('license.banner.tenant.lockdown.restrictions')
              : status.kind === 'grace_write'
                ? t('license.banner.tenant.graceWrite.restrictions')
                : null}
          </Text>
          {tenant.tenantId ? (
            <div>
              <Button size="small" type="primary" onClick={openLicensePage}>
                {isLocked
                  ? t('license.banner.actions.contact')
                  : t('license.banner.actions.renewTenant')}
              </Button>
            </div>
          ) : null}
        </Flex>
      }
    />
  );
}
