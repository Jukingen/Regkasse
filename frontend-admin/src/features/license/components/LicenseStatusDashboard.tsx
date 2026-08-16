'use client';

import { ReloadOutlined } from '@ant-design/icons';
import { Alert, Button, Card, Col, Flex, Progress, Row, Spin, Statistic, Tag, Timeline } from 'antd';
import dayjs from 'dayjs';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { LockedLicenseDataRightsCard } from '@/features/data-management/components/LockedLicenseDataRightsCard';
import { TENANT_GRACE_PERIOD_DAYS } from '@/features/license/constants/licenseGracePeriod';
import { useLicensePageRefresh } from '@/features/license/hooks/useLicensePageRefresh';
import { useLicenseRenewalFunnelPageView } from '@/features/license/hooks/useLicenseRenewalFunnelPageView';
import { openLicenseRenewalModal } from '@/features/license/stores/licenseRenewalModalStore';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import {
  getLicenseHistoryEventLabel,
  getLicenseHistoryEventTagColor,
} from '@/features/license/utils/licenseHistoryLabels';
import { formatLicenseValidUntil } from '@/features/license/utils/licenseValidUntil';
import { useLicenseStatus, shouldShowSystemLockedAlertFromView } from '@/hooks/useLicenseStatus';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';

function stateColor(state: string | undefined): string {
  switch (state) {
    case 'Active':
      return '#52c41a';
    case 'Grace':
      return '#faad14';
    default:
      return '#cf1322';
  }
}

function daysValueColor(isActive: boolean, daysLeft: number): string {
  if (!isActive) return '#cf1322';
  if (daysLeft > 30) return '#52c41a';
  if (daysLeft > 7) return '#faad14';
  return '#cf1322';
}

function progressPercent(args: {
  isActive: boolean;
  isGrace: boolean;
  daysLeft: number;
  graceDaysRemaining: number;
}): number {
  if (args.isActive) {
    return Math.max(0, Math.min(100, (args.daysLeft / 365) * 100));
  }
  if (args.isGrace) {
    return Math.max(
      0,
      Math.min(100, (args.graceDaysRemaining / TENANT_GRACE_PERIOD_DAYS) * 100)
    );
  }
  return 0;
}

export default function LicenseStatusDashboard() {
  const { t } = useI18n();
  const tenant = useCurrentTenant();
  const { status, history, isLoading, isFetching } = useLicenseStatus();
  const { refresh, isFetching: isRefreshing } = useLicensePageRefresh({
    tenantId: tenant.tenantId,
  });
  useLicenseRenewalFunnelPageView(Boolean(status));

  const isActive = status?.state === 'Active';
  const isGrace = status?.state === 'Grace';
  const isLocked = status?.state === 'Locked' || status?.state === 'Archived';
  const showSystemLockedAlert = Boolean(status && shouldShowSystemLockedAlertFromView(status));
  const daysLeft = status?.daysUntilExpiry ?? 0;
  const daysOverdue = status?.daysOverdue ?? 0;
  const graceDaysRemaining = status?.graceDaysRemaining ?? 0;

  const statusLabel = isActive
    ? t('license.statusDashboard.stateActive')
    : isGrace
      ? t('license.statusDashboard.stateGrace')
      : t('license.statusDashboard.stateLocked');

  const daysTitle = isActive
    ? t('license.statusDashboard.daysUntilExpiry')
    : isGrace
      ? t('license.statusDashboard.graceDaysRemaining')
      : t('license.statusDashboard.daysOverdue');

  const daysValue = isActive ? daysLeft : isGrace ? graceDaysRemaining : daysOverdue;

  const validUntilLabel = formatLicenseValidUntil(status?.expiredAt);

  const timelineItems = (history ?? []).slice(0, 12).map((item, index) => {
    const colorToken = getLicenseHistoryEventTagColor(item.eventType);
    const color =
      colorToken === 'green'
        ? 'green'
        : colorToken === 'red'
          ? 'red'
          : colorToken === 'blue'
            ? 'blue'
            : 'gray';
    return {
      key: `${item.eventType}-${item.atUtc}-${index}`,
      color,
      children: (
        <div>
          <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12 }}>
            <span>{getLicenseHistoryEventLabel(item.eventType, t)}</span>
            <span style={{ fontSize: 12, opacity: 0.65 }}>
              {dayjs(item.atUtc).format('DD.MM.YYYY HH:mm')}
            </span>
          </div>
          {item.summary ? (
            <div style={{ fontSize: 12, opacity: 0.75, marginTop: 4 }}>{item.summary}</div>
          ) : null}
        </div>
      ),
    };
  });

  if (isLoading && !status) {
    return (
      <div style={{ display: 'flex', justifyContent: 'center', padding: 48 }}>
        <Spin size="large" />
      </div>
    );
  }

  return (
    <div>
      <AdminPageHeader
        title={t('license.statusDashboard.title')}
        subtitle={t('license.statusDashboard.subtitle')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          {
            title: t('nav.licenseManagement'),
            href: '/admin/license-management',
          },
          { title: t('license.statusDashboard.title') },
        ]}
        extra={
          <Flex gap={8} wrap="wrap">
            <Button
              icon={<ReloadOutlined />}
              loading={isRefreshing || isFetching}
              onClick={() => void refresh()}
            >
              {t('common.buttons.refresh')}
            </Button>
            <Button
              type={isLocked || isGrace ? 'primary' : 'default'}
              danger={isLocked}
              onClick={() => openLicenseRenewalModal()}
            >
              {isLocked
                ? t('license.statusDashboard.renewLocked')
                : t('license.statusDashboard.renew')}
            </Button>
          </Flex>
        }
      />

      <Row gutter={[16, 16]}>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title={t('license.statusDashboard.status')}
              value={statusLabel}
              styles={{ content: { color: stateColor(status?.state) } }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title={daysTitle}
              value={daysValue}
              styles={{ content: { color: daysValueColor(Boolean(isActive), daysLeft) } }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic title={t('license.statusDashboard.validUntil')} value={validUntilLabel} />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title={t('license.statusDashboard.licensePlan')}
              value={status?.licensePlan?.trim() || t('license.statusDashboard.planFallback')}
            />
          </Card>
        </Col>
      </Row>

      <Card style={{ marginTop: 16 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
          <div style={{ flex: 1, minWidth: 0 }}>
            <Progress
              percent={progressPercent({
                isActive: Boolean(isActive),
                isGrace: Boolean(isGrace),
                daysLeft,
                graceDaysRemaining,
              })}
              strokeColor={isActive ? '#52c41a' : isGrace ? '#faad14' : '#cf1322'}
              format={() =>
                isActive
                  ? t('license.statusDashboard.progressActive', { days: daysLeft })
                  : isGrace
                    ? t('license.statusDashboard.progressGrace', { days: graceDaysRemaining })
                    : t('license.statusDashboard.progressExpired')
              }
            />
          </div>
          <Tag color={isActive ? 'green' : isGrace ? 'orange' : 'red'}>
            {status?.state ?? '—'}
          </Tag>
        </div>
      </Card>

      {showSystemLockedAlert ? (
        <Alert
          type="error"
          showIcon
          style={{ marginTop: 16 }}
          title={t('license.statusDashboard.lockedAlertTitle')}
          description={t('license.statusDashboard.lockedAlertDescription')}
        />
      ) : null}

      {isGrace ? (
        <Alert
          type="warning"
          showIcon
          style={{ marginTop: 16 }}
          title={t('license.statusDashboard.graceAlertTitle')}
          description={t('license.statusDashboard.graceAlertDescription', {
            days: graceDaysRemaining,
          })}
        />
      ) : null}

      {isLocked ? <LockedLicenseDataRightsCard /> : null}

      <Card title={t('license.statusDashboard.timelineTitle')} style={{ marginTop: 16 }}>
        {timelineItems.length === 0 ? (
          <div style={{ opacity: 0.65 }}>{t('license.history.empty')}</div>
        ) : (
          <Timeline items={timelineItems} />
        )}
      </Card>
    </div>
  );
}
