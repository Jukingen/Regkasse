'use client';

import {
  CheckCircleOutlined,
  LockOutlined,
  WarningOutlined,
} from '@ant-design/icons';
import { Alert, Card, Col, Flex, Row, Typography } from 'antd';
import type { ReactNode } from 'react';

import { TENANT_GRACE_PERIOD_DAYS } from '@/features/license/constants/licenseGracePeriod';
import {
  getLicenseExpiryImpactModel,
  getLicenseImpactAccentStyles,
  type LicenseImpactAccent,
} from '@/features/license/utils/licenseExpiryImpact';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import { useLicenseStatus } from '@/hooks/useLicenseStatus';
import { useI18n } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';

const { Text, Paragraph } = Typography;

function PhaseTile({
  accent,
  eyebrow,
  title,
  description,
  icon,
}: {
  accent: LicenseImpactAccent;
  eyebrow: string;
  title: string;
  description: string;
  icon: ReactNode;
}) {
  const styles = getLicenseImpactAccentStyles(accent);
  return (
    <div
      style={{
        padding: 16,
        borderRadius: 8,
        border: `1px solid ${styles.borderColor}`,
        background: styles.background,
        height: '100%',
      }}
    >
      <Text type="secondary" style={{ fontSize: 12 }}>
        {eyebrow}
      </Text>
      <Flex align="center" gap={8} style={{ marginTop: 8 }}>
        {icon}
        <Text strong>{title}</Text>
      </Flex>
      <Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0, fontSize: 12 }}>
        {description}
      </Paragraph>
    </div>
  );
}

/**
 * Mandant license expiry impact timeline: Active → Grace → Locked.
 * Uses real product semantics (Grace = full access + warnings; Locked = read-only / POS blocked).
 */
export function LicenseExpiryImpactCard() {
  const { t } = useI18n();
  const tenant = useCurrentTenant();
  const { status, isLoading } = useLicenseStatus();
  const { isAuthorized: canView } = useAuthorizationGate({
    requiredPermission: PERMISSIONS.LICENSE_VIEW,
  });

  if (!canView || isLoading || !status) return null;
  if (!tenant.isRealTenantSlug || tenant.isSuperAdminPlatformMode) return null;

  const model = getLicenseExpiryImpactModel({
    state: status.state,
    daysUntilExpiry: status.daysUntilExpiry,
    graceDaysRemaining: status.graceDaysRemaining,
    daysOverdue: status.daysOverdue,
  });

  const stateLabel = t(`dashboard.widgets.licenseImpact.states.${status.state}`);
  const currentDaysDescription =
    model.currentDaysKind === 'untilExpiry'
      ? t('dashboard.widgets.licenseImpact.current.daysValid', {
          days: model.currentDaysLabelValue,
        })
      : model.currentDaysKind === 'graceRemaining'
        ? t('dashboard.widgets.licenseImpact.current.daysGrace', {
            days: model.currentDaysLabelValue,
          })
        : t('dashboard.widgets.licenseImpact.current.daysOverdue', {
            days: model.currentDaysLabelValue,
          });

  const alertOk = model.alertType === 'info';

  return (
    <Card
      size="small"
      title={t('dashboard.widgets.licenseImpact.title')}
      style={{ marginBottom: 16 }}
      styles={{ body: { paddingBlock: 16 } }}
    >
      <Row gutter={[16, 16]}>
        <Col xs={24} md={8}>
          <PhaseTile
            accent={model.currentAccent}
            eyebrow={t('dashboard.widgets.licenseImpact.current.eyebrow')}
            title={stateLabel}
            description={currentDaysDescription}
            icon={
              status.state === 'Active' ? (
                <CheckCircleOutlined style={{ color: '#52c41a' }} aria-hidden />
              ) : status.state === 'Grace' ? (
                <WarningOutlined style={{ color: '#faad14' }} aria-hidden />
              ) : (
                <LockOutlined style={{ color: '#cf1322' }} aria-hidden />
              )
            }
          />
        </Col>
        <Col xs={24} md={8}>
          <PhaseTile
            accent={model.graceAccent}
            eyebrow={t('dashboard.widgets.licenseImpact.grace.eyebrow', {
              days: TENANT_GRACE_PERIOD_DAYS,
            })}
            title={t('dashboard.widgets.licenseImpact.grace.title')}
            description={t('dashboard.widgets.licenseImpact.grace.description')}
            icon={<WarningOutlined style={{ color: '#faad14' }} aria-hidden />}
          />
        </Col>
        <Col xs={24} md={8}>
          <PhaseTile
            accent={model.lockedAccent}
            eyebrow={t('dashboard.widgets.licenseImpact.locked.eyebrow')}
            title={t('dashboard.widgets.licenseImpact.locked.title')}
            description={t('dashboard.widgets.licenseImpact.locked.description')}
            icon={<LockOutlined style={{ color: '#cf1322' }} aria-hidden />}
          />
        </Col>
      </Row>

      <Alert
        type={model.alertType}
        showIcon
        style={{ marginTop: 16 }}
        title={
          alertOk
            ? t('dashboard.widgets.licenseImpact.alert.okTitle')
            : t('dashboard.widgets.licenseImpact.alert.actionTitle')
        }
        description={
          alertOk
            ? t('dashboard.widgets.licenseImpact.alert.okDescription')
            : t('dashboard.widgets.licenseImpact.alert.actionDescription')
        }
      />
    </Card>
  );
}
