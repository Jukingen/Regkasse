'use client';

import {
  CalendarOutlined,
  CheckCircleOutlined,
  ClockCircleOutlined,
  CloseCircleOutlined,
  HomeOutlined,
  IdcardOutlined,
} from '@ant-design/icons';
import { Flex, Space, Tag, Tooltip, Typography } from 'antd';
import type { CSSProperties, ReactNode } from 'react';

import { useLicense } from '@/features/license/hooks/useLicense';
import {
  type LicenseStatusKind,
  type ResolvedLicenseStatus,
  getLicenseStatusDayText,
  getLicenseStatusLabel,
  getLicenseStatusMessage,
  getLicenseStatusTagColor,
} from '@/features/license/utils/licenseStatus';
import { useSuperAdminTenantMode } from '@/features/tenancy/hooks/useSuperAdminTenantMode';
import { useTenantContext } from '@/features/tenancy/hooks/useTenantContext';
import { useTenantInfo } from '@/features/tenant/hooks/useTenantInfo';
import { useTenant } from '@/hooks/useTenant';
import { formatDate, useI18n } from '@/i18n';

import styles from './TenantHeader.module.css';

const { Text } = Typography;

export type TenantHeaderProps = {
  className?: string;
  style?: CSSProperties;
};

function licenseIcon(kind: LicenseStatusKind): ReactNode {
  if (kind === 'active') {
    return <CheckCircleOutlined />;
  }
  if (kind === 'grace_write' || kind === 'grace_readonly') {
    return <ClockCircleOutlined />;
  }
  return <CloseCircleOutlined />;
}

function LicenseStatusTag({ licenseStatus }: { licenseStatus: ResolvedLicenseStatus }) {
  const { t } = useI18n();
  const tooltip = getLicenseStatusDayText(licenseStatus, t);
  const message = getLicenseStatusMessage(licenseStatus, 'tenant', t);

  return (
    <Tooltip title={tooltip ? `${message} ${tooltip}` : message}>
      <Tag
        color={getLicenseStatusTagColor(licenseStatus.kind)}
        icon={licenseIcon(licenseStatus.kind)}
      >
        {getLicenseStatusLabel(licenseStatus.kind, t)}
      </Tag>
    </Tooltip>
  );
}

function TenantModeTags({
  isDevTenantOverride,
  isImpersonating,
  isPlatformAdminHost,
}: {
  isDevTenantOverride: boolean;
  isImpersonating: boolean;
  isPlatformAdminHost: boolean;
}) {
  const { t } = useI18n();

  if (!isDevTenantOverride && !isImpersonating && !isPlatformAdminHost) {
    return null;
  }

  return (
    <Flex gap={4} wrap="wrap">
      {isDevTenantOverride ? (
        <Tag color="gold">{t('adminShell.tenant.infoCardDevOverrideTag')}</Tag>
      ) : null}
      {isImpersonating ? (
        <Tag color="purple">{t('adminShell.tenant.infoCardImpersonationTag')}</Tag>
      ) : null}
      {isPlatformAdminHost ? (
        <Tag color="orange">{t('adminShell.tenant.infoCardPlatformAdminTag')}</Tag>
      ) : null}
    </Flex>
  );
}

function formatIdPreview(tenantId: string): string {
  return tenantId.length > 8 ? `${tenantId.slice(0, 8)}…` : tenantId;
}

export function TenantHeader({ className, style }: TenantHeaderProps) {
  const { t, formatLocale } = useI18n();
  const { tenant } = useTenant();
  const { licenseStatus } = useLicense();
  const { requiresTenantSelection } = useSuperAdminTenantMode();
  const { jwtTenantSlug, isDevTenantOverride, isImpersonating, isPlatformAdminHost } =
    useTenantContext();
  const {
    tenantSlug,
    tenantId,
    tenantName,
    registeredAt,
    licenseStatus: tenantRowLicense,
    hasAuthToken,
    isLoading,
  } = useTenantInfo();

  if (!hasAuthToken || requiresTenantSelection) {
    return null;
  }

  const displayName = tenant?.name?.trim() || tenantName?.trim() || null;
  const displaySlug = tenant?.slug?.trim() || tenantSlug?.trim() || null;
  const displayId = tenant?.id || tenantId || null;

  if (!displayName && !displaySlug && !displayId && !isLoading) {
    return null;
  }

  const idPreview = displayId ? formatIdPreview(displayId) : '—';
  const registeredLabel = registeredAt
    ? formatDate(registeredAt, formatLocale, {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
      })
    : null;

  const resolvedLicense = licenseStatus ?? tenantRowLicense;
  const normalizedSlug = displaySlug?.toLowerCase() ?? '';
  const normalizedJwtSlug = jwtTenantSlug?.trim().toLowerCase() ?? '';
  const showJwtSlug = normalizedJwtSlug.length > 0 && normalizedJwtSlug !== normalizedSlug;
  const mandantTooltip = `${t('common.tenant.tenant')} (${t('common.tenant.tenantAlt')})`;

  return (
    <div
      className={[styles.tenantHeader, 'tenant-header', className].filter(Boolean).join(' ')}
      style={style}
    >
      <Space size="middle" wrap>
        <TenantModeTags
          isDevTenantOverride={isDevTenantOverride}
          isImpersonating={isImpersonating}
          isPlatformAdminHost={isPlatformAdminHost}
        />

        <Tooltip title={mandantTooltip}>
          <Space size={4}>
            <HomeOutlined />
            <Text strong>{displayName || t('adminShell.tenant.infoCardTitle')}</Text>
            {displaySlug ? <Text type="secondary">({displaySlug})</Text> : null}
          </Space>
        </Tooltip>

        {displayId ? (
          <Tooltip title={`${t('adminShell.tenant.infoCardId')}: ${displayId}`}>
            <Space size={4}>
              <IdcardOutlined />
              <Text code copyable={{ text: displayId }} className={styles.idText}>
                {idPreview}
              </Text>
            </Space>
          </Tooltip>
        ) : null}

        {showJwtSlug ? (
          <Tooltip title={t('adminShell.tenant.infoCardJwtSlug')}>
            <Text code className={styles.idText}>
              {jwtTenantSlug}
            </Text>
          </Tooltip>
        ) : null}

        <LicenseStatusTag licenseStatus={resolvedLicense} />

        {registeredLabel ? (
          <Tooltip title={t('adminShell.tenant.info.registeredAt')}>
            <Space size={4}>
              <CalendarOutlined />
              <Text type="secondary" className={styles.dateText}>
                {registeredLabel}
              </Text>
            </Space>
          </Tooltip>
        ) : null}
      </Space>
    </div>
  );
}
