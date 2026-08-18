'use client';

import {
  CheckCircleOutlined,
  ClockCircleOutlined,
  CloseCircleOutlined,
  StopOutlined,
  WarningOutlined,
} from '@ant-design/icons';
import { Tag, Tooltip } from 'antd';
import type { ReactNode } from 'react';

import { LicenseExpiryCountdownText } from '@/features/license/components/LicenseExpiryCountdownText';
import type { UnifiedLicenseRowStatus } from '@/features/license/utils/unifiedLicenseRows';
import { useI18n } from '@/i18n';

const STATUS_COLOR: Record<UnifiedLicenseRowStatus, string> = {
  active: 'green',
  grace: 'gold',
  expiringSoon: 'orange',
  expired: 'red',
  locked: 'default',
};

function statusIcon(status: UnifiedLicenseRowStatus): ReactNode {
  switch (status) {
    case 'active':
      return <CheckCircleOutlined />;
    case 'grace':
      return <WarningOutlined />;
    case 'expiringSoon':
      return <ClockCircleOutlined />;
    case 'expired':
      return <CloseCircleOutlined />;
    case 'locked':
      return <StopOutlined />;
    default:
      return null;
  }
}

export type UnifiedLicenseStatusBadgeProps = {
  status: UnifiedLicenseRowStatus;
  validUntilUtc?: string | null;
  showCountdown?: boolean;
};

export function UnifiedLicenseStatusBadge({
  status,
  validUntilUtc,
  showCountdown = false,
}: UnifiedLicenseStatusBadgeProps) {
  const { t } = useI18n();
  const label = t(`license.statusBadge.labels.${status}`);
  const tooltip = t(`license.statusBadge.tooltips.${status}`);
  const showTimer = showCountdown && (status === 'active' || status === 'expiringSoon');

  return (
    <Tooltip title={tooltip}>
      <span style={{ display: 'inline-flex', flexDirection: 'column', gap: 2 }}>
        <Tag
          color={STATUS_COLOR[status]}
          icon={statusIcon(status)}
          style={status === 'locked' ? { color: '#595959', borderColor: '#d9d9d9' } : undefined}
        >
          {label}
        </Tag>
        {showTimer ? (
          <LicenseExpiryCountdownText
            expiresAt={validUntilUtc}
            labelKey="license.statusBadge.countdown"
            t={t}
          />
        ) : null}
      </span>
    </Tooltip>
  );
}
