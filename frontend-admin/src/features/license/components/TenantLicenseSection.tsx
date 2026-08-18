'use client';

import { Alert, Button, Card, Descriptions, Empty, Space, Switch, Tooltip, Typography } from 'antd';
import { useMemo, useState } from 'react';

import { LicenseExtendModal } from '@/features/license/components/LicenseExtendModal';
import { LicenseHistory } from '@/features/license/components/LicenseHistory';
import { LicenseKeyRevealText } from '@/features/license/components/LicenseKeyRevealText';
import { UnifiedLicenseDetailDrawer } from '@/features/license/components/UnifiedLicenseDetailDrawer';
import { UnifiedLicenseStatusBadge } from '@/features/license/components/UnifiedLicenseStatusBadge';
import { LicenseExpiryCountdownText } from '@/features/license/components/LicenseExpiryCountdownText';
import { downloadLicenseCertificatePdf } from '@/api/manual/adminLicense';
import { createSupportTicket } from '@/features/support-tickets/api/supportTickets';
import { useNotify } from '@/hooks/useNotify';
import { useLicenseKeyReveal } from '@/features/license/hooks/useLicenseKeyReveal';
import { useTenantLicenseDetail } from '@/features/license/hooks/useTenantLicenseDetail';
import {
  getLicenseStatusMessage,
  getLicenseStatusRemainingText,
  mapPublicStatusToTenantLicenseStatus,
  resolveTenantLicenseFromPublicStatus,
  resolveTenantLicenseStatus,
} from '@/features/license/utils/licenseStatus';
import type { UnifiedLicenseRowStatus } from '@/features/license/utils/unifiedLicenseRows';
import { formatLicenseValidUntil } from '@/features/license/utils/licenseValidUntil';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useTenant } from '@/features/tenancy/providers/TenantProvider';
import { FirmenInfo } from '@/features/tenants/components/FirmenInfo';
import { usePermissions } from '@/hooks/usePermissions';
import { useTenantLicense } from '@/hooks/useTenantLicense';
import { useI18n } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';

const EXPIRING_SOON_THRESHOLD_DAYS = 7;

function toBadgeStatus(
  kind: string | undefined,
  daysRemaining: number
): UnifiedLicenseRowStatus {
  if (kind === 'grace_write' || kind === 'grace_readonly') return 'grace';
  if (kind === 'lockdown') return 'locked';
  if (kind === 'expired' || kind === 'no_license' || !kind) return 'expired';
  if (kind === 'active' && daysRemaining > 0 && daysRemaining <= 30) return 'expiringSoon';
  return 'active';
}

export function TenantLicenseSection() {
  const { t } = useI18n();
  const currentTenant = useCurrentTenant();
  const { tenant, isLoading: tenantLoading, error: tenantError } = useTenant();
  const { hasPermission } = usePermissions();
  const notify = useNotify();
  const [extendOpen, setExtendOpen] = useState(false);
  const [detailKey, setDetailKey] = useState<string | null>(null);
  const [requesting, setRequesting] = useState(false);
  const [downloading, setDownloading] = useState(false);

  const tenantId = currentTenant.tenantId ?? '';
  const isSuperAdmin = hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
  const { showKeys, onShowKeysChange } = useLicenseKeyReveal(isSuperAdmin);

  const publicLicenseQuery = useTenantLicense(tenantId, {
    enabled: !isSuperAdmin && Boolean(tenantId),
  });
  const adminLicenseQuery = useTenantLicenseDetail(tenantId, {
    enabled: isSuperAdmin && Boolean(tenantId),
  });

  const licenseQuery = isSuperAdmin ? adminLicenseQuery : publicLicenseQuery;

  const status = useMemo(() => {
    if (isSuperAdmin) {
      return adminLicenseQuery.data?.status ?? null;
    }
    if (!publicLicenseQuery.data) {
      return null;
    }
    return mapPublicStatusToTenantLicenseStatus(publicLicenseQuery.data);
  }, [isSuperAdmin, adminLicenseQuery.data, publicLicenseQuery.data]);

  const resolvedStatus = useMemo(() => {
    if (isSuperAdmin) {
      return status ? resolveTenantLicenseStatus(status) : null;
    }
    return publicLicenseQuery.data
      ? resolveTenantLicenseFromPublicStatus(publicLicenseQuery.data)
      : null;
  }, [isSuperAdmin, status, publicLicenseQuery.data]);

  const remainingText = useMemo(() => {
    if (!resolvedStatus) return null;
    return getLicenseStatusRemainingText(resolvedStatus, t, status?.validUntilUtc);
  }, [resolvedStatus, status?.validUntilUtc, t]);

  const expiryBanner = useMemo(() => {
    if (!resolvedStatus) return null;
    const days = resolvedStatus.daysRemaining;
    if (days > 0 && days <= EXPIRING_SOON_THRESHOLD_DAYS) {
      return (
        <Alert
          type="warning"
          showIcon
          title={t('license.mandant.expiresSoon')}
          description={remainingText ?? undefined}
        />
      );
    }
    if (
      resolvedStatus.kind === 'grace_write' ||
      resolvedStatus.kind === 'grace_readonly' ||
      resolvedStatus.kind === 'lockdown' ||
      resolvedStatus.kind === 'no_license' ||
      days <= 0
    ) {
      return (
        <Alert
          type="error"
          showIcon
          title={t('license.mandant.expired')}
          description={getLicenseStatusMessage(resolvedStatus, 'tenant', t)}
        />
      );
    }
    return null;
  }, [resolvedStatus, remainingText, t]);

  const firmenInfo = (
    <FirmenInfo
      tenant={tenant}
      loading={tenantLoading || (currentTenant.isTenantRecordLoading && !tenantId)}
      error={tenantError}
      licenseValidUntilUtc={status?.validUntilUtc}
    />
  );

  if ((currentTenant.isTenantRecordLoading && !tenantId) || !tenantId) {
    return (
      <Space orientation="vertical" size="large" style={{ width: '100%' }}>
        {firmenInfo}
      </Space>
    );
  }

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      {firmenInfo}

      <Typography.Title level={4} style={{ margin: 0 }}>
        {t('license.page.tenantLicense')}
      </Typography.Title>

      <Alert
        type="info"
        showIcon
        title={t('license.management.systemDoesNotUnlockTenant')}
      />

      {expiryBanner}

      <Card loading={licenseQuery.isLoading}>
        {!licenseQuery.isLoading && !status ? (
          <Empty description={t('license.mandant.noLicense')} />
        ) : null}
        {status ? (
          <Descriptions bordered column={{ xs: 1, sm: 2 }} size="small">
            <Descriptions.Item label={t('license.mandant.status')}>
              <UnifiedLicenseStatusBadge
                status={toBadgeStatus(resolvedStatus?.kind, resolvedStatus?.daysRemaining ?? 0)}
                validUntilUtc={status.validUntilUtc}
                showCountdown
              />
            </Descriptions.Item>
            {isSuperAdmin ? (
              <Descriptions.Item label={t('license.mandant.licenseKey')}>
                <Space wrap>
                  <LicenseKeyRevealText licenseKey={status.licenseKey} reveal={showKeys} />
                  <Tooltip title={t('license.management.showKeysTooltip')}>
                    <Switch
                      size="small"
                      checked={showKeys}
                      checkedChildren={t('license.management.hideKeys')}
                      unCheckedChildren={t('license.management.showKeys')}
                      onChange={onShowKeysChange}
                    />
                  </Tooltip>
                </Space>
              </Descriptions.Item>
            ) : null}
            <Descriptions.Item label={t('license.mandant.validUntil')}>
              {formatLicenseValidUntil(status.validUntilUtc)}
            </Descriptions.Item>
            {resolvedStatus ? (
              <Descriptions.Item label={t('tenants.detail.license.remaining')}>
                {remainingText ?? '—'}
                <div>
                  <LicenseExpiryCountdownText
                    expiresAt={status.validUntilUtc}
                    labelKey="license.statusBadge.countdown"
                    t={t}
                  />
                </div>
              </Descriptions.Item>
            ) : null}
          </Descriptions>
        ) : null}
        {resolvedStatus?.kind === 'active' && !expiryBanner ? (
          <Alert
            style={{ marginTop: 16 }}
            type="success"
            showIcon
            title={getLicenseStatusMessage(resolvedStatus, 'tenant', t)}
          />
        ) : null}
        <div style={{ marginTop: 16 }}>
          <Space wrap>
            <Button type="primary" onClick={() => setExtendOpen(true)}>
              {t('license.renewal.renewNow')}
            </Button>
            {!isSuperAdmin ? (
              <Button
                loading={requesting}
                onClick={() => {
                  setRequesting(true);
                  void createSupportTicket({
                    category: 'License',
                    priority: 'High',
                    title: t('license.renewal.requestTitle'),
                    message: t('license.renewal.requestMessage', {
                      validUntil: formatLicenseValidUntil(status?.validUntilUtc),
                    }),
                  })
                    .then(() => notify.success(t('license.renewal.requestSuccess')))
                    .catch((err) =>
                      notify.apiError(err, {
                        logContext: 'TenantLicenseSection.requestRenewal',
                        fallbackKey: 'license.renewal.requestError',
                      })
                    )
                    .finally(() => setRequesting(false));
                }}
              >
                {t('license.renewal.requestButton')}
              </Button>
            ) : null}
            <Button
              loading={downloading}
              onClick={() => {
                setDownloading(true);
                void downloadLicenseCertificatePdf()
                  .then(() => notify.success(t('license.certificate.downloadSuccess')))
                  .catch((err) =>
                    notify.apiError(err, {
                      logContext: 'TenantLicenseSection.certificate',
                      fallbackKey: 'license.certificate.downloadError',
                    })
                  )
                  .finally(() => setDownloading(false));
              }}
            >
              {t('license.certificate.download')}
            </Button>
            {status?.licenseKey ? (
              <Button onClick={() => setDetailKey(status.licenseKey ?? null)}>
                {t('license.management.viewDetails')}
              </Button>
            ) : null}
          </Space>
        </div>
      </Card>

      <LicenseExtendModal
        open={extendOpen}
        tenantId={tenantId}
        status={status}
        resolvedStatus={resolvedStatus}
        onClose={() => setExtendOpen(false)}
      />

      <UnifiedLicenseDetailDrawer licenseKey={detailKey} onClose={() => setDetailKey(null)} />

      <LicenseHistory tenantId={tenantId} />
    </Space>
  );
}
