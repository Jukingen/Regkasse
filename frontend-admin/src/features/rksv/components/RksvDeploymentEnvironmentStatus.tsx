'use client';

import { Alert, Skeleton, Tag, Tooltip } from 'antd';
import type { CSSProperties } from 'react';

import { useRksvStatus } from '@/features/rksv/hooks/useRksvBackendEnvironment';
import {
  isTseFiscalConfigLockUnsafe,
  type RksvBackendEnvironmentStatus,
} from '@/features/rksv/types/rksvBackendEnvironment';
import { useI18n } from '@/i18n/I18nProvider';

function badgeColor(isDemo: boolean): 'warning' | 'success' {
  return isDemo ? 'warning' : 'success';
}

type BadgeProps = {
  status?: RksvBackendEnvironmentStatus | null;
  isDemo?: boolean;
  loading?: boolean;
};

export function RksvDeploymentEnvironmentBadge({ status, isDemo, loading }: BadgeProps) {
  const { t } = useI18n();
  const query = useRksvStatus({
    enabled: status === undefined && isDemo === undefined && loading === undefined,
  });
  const resolvedStatus = status ?? query.data ?? null;
  const resolvedLoading = loading ?? query.isLoading;
  const isSimulated = isDemo ?? resolvedStatus?.isSimulated ?? false;

  if (resolvedLoading && !resolvedStatus) {
    return <Skeleton.Button active size="small" style={{ width: 96, height: 22 }} />;
  }

  if (!resolvedStatus) {
    return null;
  }

  const labelKey = isSimulated
    ? 'rksvHub.env.backend.displayLabel.demo'
    : 'rksvHub.env.backend.displayLabel.production';

  return (
    <Tooltip title={t('rksvHub.env.backend.badgeTooltip')}>
      <Tag
        color={badgeColor(isSimulated)}
        data-rksv-deployment-environment={resolvedStatus.environment}
        data-rksv-deployment-simulated={String(resolvedStatus.isSimulated)}
      >
        {t(labelKey)}
      </Tag>
    </Tooltip>
  );
}

type AlertProps = {
  style?: CSSProperties;
};

export function RksvDeploymentEnvironmentAlert({ style }: AlertProps) {
  const { t } = useI18n();
  const { data, isLoading, isError } = useRksvStatus();
  const isSimulated = data?.isSimulated === true;
  const lockUnsafe = isTseFiscalConfigLockUnsafe(data);

  if (isLoading && !data) {
    return <Skeleton active paragraph={{ rows: 1 }} style={style} />;
  }

  if (isError || !data) {
    return null;
  }

  if (lockUnsafe) {
    const reasons =
      data.fiscalConfigLockReasons.length > 0
        ? data.fiscalConfigLockReasons.join(' · ')
        : t('rksvHub.env.backend.productionLock.reasonsUnknown');
    return (
      <Alert
        showIcon
        type="error"
        title={
          data.fiscalConfigLockEscapeHatchActive
            ? t('rksvHub.env.backend.productionLock.escapeTitle')
            : t('rksvHub.env.backend.productionLock.title')
        }
        description={t('rksvHub.env.backend.productionLock.description', { reasons })}
        style={style}
        data-rksv-tse-production-lock="unsafe"
        data-rksv-tse-production-lock-escape={String(data.fiscalConfigLockEscapeHatchActive)}
      />
    );
  }

  const messageKey = isSimulated
    ? 'rksvHub.env.backend.banner.demo'
    : 'rksvHub.env.backend.banner.production';

  return (
    <Alert
      showIcon
      type={isSimulated ? 'warning' : 'success'}
      title={t(messageKey)}
      description={
        data.tseStatusDisplay
          ? t('rksvHub.env.backend.tseStatus', { status: data.tseStatusDisplay })
          : undefined
      }
      style={style}
      data-rksv-deployment-environment={data.environment}
    />
  );
}
