'use client';

/**
 * License KPI cards and recent activity table sourced from dashboard-stats API.
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
import { Alert, Card, Col, Row, Skeleton, Statistic, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import utc from 'dayjs/plugin/utc';
import React, { useMemo } from 'react';

import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useAuth } from '@/features/auth/hooks/useAuth';
import {
  type LicenseActivity,
  useLicenseDashboardStats,
} from '@/features/license/api/licenseStats';
import {
  type LicenseStatus,
  useDeploymentLicenseStatus,
  useTenantLicenseStatus,
} from '@/features/license/hooks/useLicenseStatus';
import { dateColumnRender } from '@/components/DateColumn';
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

function actionTagColor(action: string): string {
  switch (action) {
    case 'activate':
      return 'green';
    case 'extend':
      return 'blue';
    case 'revoke':
      return 'red';
    case 'cancel':
      return 'magenta';
    case 'delete':
      return 'volcano';
    case 'unregister':
      return 'orange';
    default:
      return 'default';
  }
}

function actionLabelKey(action: string): string {
  const map: Record<string, string> = {
    activate: 'license.dashboard.actionActivate',
    extend: 'license.dashboard.actionExtend',
    revoke: 'license.dashboard.actionRevoke',
    cancel: 'license.dashboard.actionCancel',
    delete: 'license.dashboard.actionDelete',
    unregister: 'license.dashboard.actionUnregister',
    details: 'license.dashboard.actionDetails',
    other: 'license.dashboard.actionOther',
  };
  return map[action] ?? 'license.dashboard.actionOther';
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

  const activityColumns: ColumnsType<LicenseActivity> = useMemo(
    () => [
      {
        title: t('license.dashboard.colTime'),
        dataIndex: 'timestampUtc',
        key: 'timestampUtc',
        width: 170,
        render: dateColumnRender('datetime'),
      },
      {
        title: t('license.dashboard.colKey'),
        dataIndex: 'licenseKeyMasked',
        key: 'licenseKeyMasked',
        ellipsis: true,
        render: (key: string) => (
          <Typography.Text code style={{ fontSize: 12 }}>
            {key || '—'}
          </Typography.Text>
        ),
      },
      {
        title: t('license.dashboard.colMachine'),
        dataIndex: 'machineFingerprintShort',
        key: 'machineFingerprintShort',
        width: 160,
        ellipsis: true,
        render: (v: string | null | undefined) =>
          v ? (
            <Typography.Text code style={{ fontSize: 12 }}>
              {v}
            </Typography.Text>
          ) : (
            '—'
          ),
      },
      {
        title: t('license.dashboard.colAction'),
        dataIndex: 'action',
        key: 'action',
        width: 160,
        render: (action: string) => (
          <Tag color={actionTagColor(action)}>{t(actionLabelKey(action))}</Tag>
        ),
      },
    ],
    [t]
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
            {t('license.dashboard.activityTitle')}
          </Title>
          <Table<LicenseActivity>
            size="small"
            rowKey={(row) =>
              `lic-act-${row.timestampUtc}-${row.sourceCode}-${row.licenseKeyMasked}-${row.machineFingerprintShort ?? ''}-${row.action}`
            }
            pagination={false}
            dataSource={data?.recentActivities ?? []}
            columns={activityColumns}
            locale={{ emptyText: '—' }}
            scroll={{ x: 720 }}
          />
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
