/**
 * Temporary POS environment badge — __DEV__ only (no shared/constants import).
 */

export const ENVIRONMENT_CONFIG = {
  isDevelopment: __DEV__,
  isTest: false,
  isProduction: !__DEV__,

  getEnvironmentBadgeText: () => {
    return __DEV__ ? '🧪 Entwicklung' : '';
  },

  getEnvironmentBadgeColor: () => {
    return __DEV__ ? 'orange' : 'green';
  },

  getEnvironmentBadgeType: () => {
    return __DEV__ ? ('development' as const) : ('production' as const);
  },
};

export const getEnvironmentBadge = () => ({
  text: ENVIRONMENT_CONFIG.getEnvironmentBadgeText(),
  color: ENVIRONMENT_CONFIG.getEnvironmentBadgeColor(),
  type: ENVIRONMENT_CONFIG.getEnvironmentBadgeType(),
});

export default ENVIRONMENT_CONFIG;
