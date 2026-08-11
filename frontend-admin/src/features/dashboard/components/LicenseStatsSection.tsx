'use client';

/**
 * License KPI cards and recent activity feed sourced from dashboard-stats API.
 * Super Admin: platform-wide tenant + deployment metrics.
 * Manager: own tenant mandant license only.
 */
import {
  CloseCircleOutlined,
  MobileOutlined,
  SafetyOutlined,
  StopOutlined,
  WarningOutlined,
} from '@ant-design/icons';
import { Alert, Card, Col, Row, Skeleton, Statistic, Tag, Typography } from 'antd';
import dayjs from 'dayjs';
import utc from 'dayjs/plugin/utc';
import React, { useMemo } from 'react';

import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useLicenseDashboardStats } from '@/features/license/api/licenseStats';
import { LicenseActivityLogCard } from '@/features/license/components/LicenseActivityLogCard';
import {
  type LicenseStatus,
  useDeploymentLicenseStatus,
  useTenantLicenseStatus,
} from '@/features/license/hooks/useLicenseStatus';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useI18n } from '@/i18n/I18nProvider';

dayjs.extend(utc);

const { Title } = Typography;

type TenantLicenseCounts = {
  active: number;
  expiring: number;
  expired: number;
};

function computeOwnTenantLicenseCounts(
  licenseValidUntilUtc: string | null | undefined,
  isActive: boolean,
  tenantStatus: string | null | undefined
): TenantLicenseCounts {
  if (tenantStatus === 'deleted' || !isActive) {
    return { active: 0, expiring: 0, expired: 1 };
  }

  if (!licenseValidUntilUtc?.trim()) {
    return { active: 0, expiring: 0, expired: 1 };
  }

  const until = dayjs.utc(licenseValidUntilUtc);
  if (!until.isValid()) {
    return { active: 0, expiring: 0, expired: 1 };
  }

  const now = dayjs.utc();
  if (!until.isAfter(now)) {
    return { active: 0, expiring: 0, expired: 1 };
  }

  const daysRemaining = until.diff(now, 'day', true);
  if (daysRemaining <= 30) {
    return { active: 0, expiring: 1, expired: 0 };
  }

  return { active: 1, expiring: 0, expired: 0 };
}

function getLicensePhaseColor(kind: LicenseStatus['kind']): string {
  switch (kind) {
    case 'active':
      return '#52c41a';
    case 'grace_write':
      return '#faad14';
    case 'grace_readonly':
      return '#ff7a45';
    case 'lockdown':
    case 'expired':
      return '#ff4d4f';
    default:
      return '#8c8c8c';
  }
}

function getLicensePhaseIcon(kind: LicenseStatus['kind']) {
  switch (kind) {
    case 'active':
      return <SafetyOutlined />;
    case 'grace_write':
    case 'grace_readonly':
      return <WarningOutlined />;
    case 'lockdown':
    case 'expired':
      return <StopOutlined />;
    default:
      return <CloseCircleOutlined />;
  }
}

function licensePhaseLabelKey(kind: LicenseStatus['kind']): string {
  switch (kind) {
    case 'active':
      return 'license.phase.labels.active';
    case 'grace_write':
      return 'license.phase.labels.graceWrite';
    case 'grace_readonly':
      return 'license.phase.labels.graceReadonly';
    case 'lockdown':
      return 'license.phase.labels.lockdown';
    case 'expired':
      return 'license.phase.labels.expired';
    default:
      return 'license.phase.labels.noLicense';
  }
}

function LicenseStatsCard({ title, status }: { title: string; status: LicenseStatus }) {
  const { t } = useI18n();
  const phaseColor = getLicensePhaseColor(status.kind);
  const value = status.daysRemaining > 0 ? status.daysRemaining : status.daysExpired;
  const suffix =
    status.daysRemaining > 0
      ? t('license.dashboard.daysSuffix')
      : t('license.dashboard.expiredSuffix');

  return (
    <Card variant="borderless">
      <Statistic
        title={title}
        value={value}
        suffix={suffix}
        prefix={getLicensePhaseIcon(status.kind)}
        styles={{ content: { color: phaseColor } }}
      />
      <div style={{ marginTop: 8 }}>
        <Tag color={phaseColor}>{t(licensePhaseLabelKey(status.kind))}</Tag>
      </div>
    </Card>
  );
}

function TenantLicenseStatCards({ active, expiring, expired }: TenantLicenseCounts) {
  const { t } = useI18n();

  return (
    <Row gutter={[16, 16]}>
      <Col xs={24} md={8}>
        <Card variant="borderless">
          <Statistic
            title={t('license.dashboard.statTenantActive')}
            value={active}
            prefix={<SafetyOutlined style={{ color: '#52c41a' }} />}
            styles={{ content: { color: '#52c41a' } }}
          />
        </Card>
      </Col>
      <Col xs={24} md={8}>
        <Card variant="borderless">
          <Statistic
            title={t('license.dashboard.statTenantExpiring30')}
            value={expiring}
            prefix={<WarningOutlined style={{ color: '#faad14' }} />}
            styles={{ content: { color: '#faad14' } }}
          />
        </Card>
      </Col>
      <Col xs={24} md={8}>
        <Card variant="borderless">
          <Statistic
            title={t('license.dashboard.statTenantExpired')}
            value={expired}
            prefix={<CloseCircleOutlined style={{ color: '#ff4d4f' }} />}
            styles={{ content: { color: '#ff4d4f' } }}
          />
        </Card>
      </Col>
    </Row>
  );
}

export function LicenseStatsSection() {
  const { t } = useI18n();
  const { user } = useAuth();
  const isSuperAdminUser = isSuperAdmin(user?.role);
  const { tenantId, licenseValidUntilUtc, isActive, tenantStatus, isTenantRecordLoading } =
    useCurrentTenant();

  const { data, isLoading, isError } = useLicenseDashboardStats();
  const { data: tenantLicenseStatus } = useTenantLicenseStatus(tenantId ?? undefined);
  const { data: deploymentLicenseStatus } = useDeploymentLicenseStatus();

  const ownTenantCounts = useMemo(
    () => computeOwnTenantLicenseCounts(licenseValidUntilUtc, isActive, tenantStatus),
    [licenseValidUntilUtc, isActive, tenantStatus]
  );

  if (isSuperAdminUser ? isLoading : isTenantRecordLoading) {
    return <Skeleton active paragraph={{ rows: 8 }} />;
  }

  if (isSuperAdminUser && isError) {
    return <Alert type="error" showIcon title={t('license.dashboard.loadFailed')} />;
  }

  return (
    <div className="license-stats-section">
      <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
        {tenantLicenseStatus ? (
          <Col xs={24} md={12}>
            <LicenseStatsCard
              title={t('license.dashboard.cardCurrentTenant')}
              status={tenantLicenseStatus}
            />
          </Col>
        ) : null}
        {deploymentLicenseStatus ? (
          <Col xs={24} md={12}>
            <LicenseStatsCard
              title={t('license.dashboard.cardCurrentDeployment')}
              status={deploymentLicenseStatus}
            />
          </Col>
        ) : null}
      </Row>
      {isSuperAdminUser ? (
        <>
          <Title level={4} style={{ marginTop: 0 }}>
            {t('license.dashboard.statGroupTenantSaas')}
          </Title>
          <TenantLicenseStatCards
            active={data?.activeTenantLicenses ?? 0}
            expiring={data?.expiringTenantLicenses ?? 0}
            expired={data?.expiredTenantLicenses ?? 0}
          />

          <Title level={4} style={{ marginTop: 24 }}>
            {t('license.dashboard.statGroupDeploymentOnPrem')}
          </Title>
          <Row gutter={[16, 16]}>
            <Col xs={24} md={8}>
              <Card variant="borderless">
                <Statistic
                  title={t('license.dashboard.statDeploymentActive')}
                  value={data?.activeDeploymentLicenses ?? 0}
                  prefix={<SafetyOutlined />}
                />
              </Card>
            </Col>
            <Col xs={24} md={8}>
              <Card variant="borderless">
                <Statistic
                  title={t('license.dashboard.statDeploymentExpiring30')}
                  value={data?.expiringDeploymentLicenses ?? 0}
                  prefix={<WarningOutlined style={{ color: '#faad14' }} />}
                  styles={{ content: { color: '#faad14' } }}
                />
              </Card>
            </Col>
            <Col xs={24} md={8}>
              <Card variant="borderless">
                <Statistic
                  title={t('license.dashboard.statDeploymentExpired')}
                  value={data?.expiredDeploymentLicenses ?? 0}
                  prefix={<CloseCircleOutlined style={{ color: '#ff4d4f' }} />}
                  styles={{ content: { color: '#ff4d4f' } }}
                />
              </Card>
            </Col>
          </Row>

          <Row gutter={[16, 16]} style={{ marginTop: 16 }}>
            <Col xs={24} md={12}>
              <Card variant="borderless">
                <Statistic
                  title={t('license.dashboard.statDevices')}
                  value={data?.activatedDevices ?? 0}
                  prefix={<MobileOutlined />}
                />
              </Card>
            </Col>
          </Row>

          <Title level={4} style={{ marginTop: 24 }}>
            {t('license.activityLog.title')}
          </Title>
          <LicenseActivityLogCard embedded />
        </>
      ) : (
        <>
          <Title level={4} style={{ marginTop: 0 }}>
            {t('license.dashboard.statGroupOwnTenant')}
          </Title>
          <TenantLicenseStatCards {...ownTenantCounts} />
        </>
      )}
    </div>
  );
}
