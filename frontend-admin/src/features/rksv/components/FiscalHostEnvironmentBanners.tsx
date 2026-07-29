'use client';

import { Alert } from 'antd';
import type { CSSProperties } from 'react';

import { useRksvStatus } from '@/features/rksv/hooks/useRksvBackendEnvironment';
import {
  ENVIRONMENT_CONFIG,
  getReleaseStageBannerKind,
  getReleaseStageBannerLabel,
  type EnvironmentBannerKind,
} from '@/shared/config/environmentBadge';
import { useI18n } from '@/i18n/I18nProvider';

type Props = {
  style?: CSSProperties;
};

function alertTypeForKind(kind: EnvironmentBannerKind): 'success' | 'warning' {
  // DEVELOPMENT = green (success); STAGING = yellow (warning); CANARY = warning + orange accent
  return kind === 'development' ? 'success' : 'warning';
}

/**
 * Global FA banners: release stage (DEVELOPMENT / STAGING / CANARY) + fiscal Simulation.
 * Production shows no release-stage banner. Lock violations: {@link RksvDeploymentEnvironmentAlert}.
 */
export function FiscalHostEnvironmentBanners({ style }: Props) {
  const { t } = useI18n();
  const { data, isLoading } = useRksvStatus();
  const buildIsDev = ENVIRONMENT_CONFIG.isDevelopment;
  const buildStage = ENVIRONMENT_CONFIG.releaseStage;

  if (isLoading && !data) {
    return null;
  }

  const kind = getReleaseStageBannerKind(data?.releaseStage ?? buildStage, {
    isHostDevelopment: data?.isHostDevelopment === true || (data == null && buildIsDev),
    isHostStaging: data?.isHostStaging === true,
    isCanary: data?.isCanary === true,
  });

  const isSimulation = data?.isSimulationMode === true || data?.isSimulated === true;

  const stageTitleKey =
    kind === 'development'
      ? 'rksvHub.env.backend.host.developmentTitle'
      : kind === 'staging'
        ? 'rksvHub.env.backend.host.stagingTitle'
        : kind === 'canary'
          ? 'rksvHub.env.backend.host.canaryTitle'
          : null;

  const stageDescriptionKey =
    kind === 'development'
      ? 'rksvHub.env.backend.host.developmentDescription'
      : kind === 'staging'
        ? 'rksvHub.env.backend.host.stagingDescription'
        : kind === 'canary'
          ? 'rksvHub.env.backend.host.canaryDescription'
          : null;

  return (
    <>
      {kind && stageTitleKey && stageDescriptionKey ? (
        <Alert
          showIcon
          type={alertTypeForKind(kind)}
          title={t(stageTitleKey) || getReleaseStageBannerLabel(kind)}
          description={t(stageDescriptionKey)}
          style={{
            marginBottom: 12,
            ...(kind === 'canary'
              ? { borderColor: '#fa8c16', background: '#fff7e6' }
              : kind === 'staging'
                ? { borderColor: '#faad14' }
                : {}),
            ...style,
          }}
          data-regkasse-release-stage={kind}
          data-regkasse-host-environment={data?.hostEnvironment || kind}
        />
      ) : null}
      {isSimulation ? (
        <Alert
          showIcon
          type="warning"
          title={t('rksvHub.env.backend.host.simulationTitle')}
          description={t('rksvHub.env.backend.host.simulationDescription')}
          style={{ marginBottom: 12, ...style }}
          data-regkasse-fiscal-simulation="true"
        />
      ) : null}
    </>
  );
}
