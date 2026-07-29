/**
 * POS environment badge — prefers EXPO_PUBLIC_RELEASE_STAGE; falls back to __DEV__.
 */

import {
  getReleaseStageBannerColor,
  getReleaseStageBannerKind,
  getReleaseStageBannerLabel,
  normalizeReleaseStage,
  type EnvironmentBadgeColor,
  type ReleaseStage,
} from '../../../shared/constants/environment';

function resolveReleaseStage(): ReleaseStage {
  const fromEnv = normalizeReleaseStage(process.env.EXPO_PUBLIC_RELEASE_STAGE);
  if (fromEnv) return fromEnv;
  return __DEV__ ? 'dev' : 'production';
}

const releaseStage = resolveReleaseStage();
const bannerKind = getReleaseStageBannerKind(releaseStage, {
  isHostDevelopment: __DEV__ && releaseStage === 'dev',
});

export const ENVIRONMENT_CONFIG = {
  isDevelopment: __DEV__,
  isTest: false,
  isProduction: !__DEV__,
  releaseStage,

  getEnvironmentBadgeText: () => {
    return bannerKind ? getReleaseStageBannerLabel(bannerKind) : '';
  },

  getEnvironmentBadgeColor: (): EnvironmentBadgeColor => {
    return bannerKind ? getReleaseStageBannerColor(bannerKind) : 'green';
  },

  getEnvironmentBadgeType: () => {
    if (bannerKind === 'development') return 'development' as const;
    if (bannerKind === 'staging') return 'staging' as const;
    if (bannerKind === 'canary') return 'canary' as const;
    return 'production' as const;
  },
};

export const getEnvironmentBadge = () => ({
  text: ENVIRONMENT_CONFIG.getEnvironmentBadgeText(),
  color: ENVIRONMENT_CONFIG.getEnvironmentBadgeColor(),
  type: ENVIRONMENT_CONFIG.getEnvironmentBadgeType(),
});

export default ENVIRONMENT_CONFIG;
