export const DEV_LIMIT_KEYS = [
  'maxActiveRegistersPerUser',
  'maxProductsPerTenant',
  'maxUsersPerTenant',
  'dailyMaxTransactions',
  'maxTransactionAmount',
  'dailyMaxRevenue',
  'maxBackupsPerTenant',
  'maxBackupSizeMB',
  'maxOfflineTransactions',
] as const;

export type DevLimitKey = (typeof DEV_LIMIT_KEYS)[number];

export type DevLimitScenario = 'near' | 'at' | 'tiny' | 'reset';

export const DEV_LIMIT_SCENARIOS: readonly DevLimitScenario[] = ['near', 'at', 'tiny', 'reset'];

export const DEV_LIMIT_SCENARIO_LABEL_KEYS = {
  near: 'tenants.limits.devPanel.scenario.near',
  at: 'tenants.limits.devPanel.scenario.at',
  tiny: 'tenants.limits.devPanel.scenario.tiny',
  reset: 'tenants.limits.devPanel.scenario.reset',
} as const;

export const DEV_LIMIT_LOG_ACTION_KEYS = {
  set: 'tenants.limits.devPanel.logActions.set',
  reset: 'tenants.limits.devPanel.logActions.reset',
  scenario: 'tenants.limits.devPanel.logActions.scenario',
  cache: 'tenants.limits.devPanel.logActions.cache',
} as const;

export const DEV_LIMIT_FIELD_META: Record<
  DevLimitKey,
  { labelKey: `tenants.limits.${DevLimitKey}`; money?: boolean }
> = {
  maxActiveRegistersPerUser: { labelKey: 'tenants.limits.maxActiveRegistersPerUser' },
  maxProductsPerTenant: { labelKey: 'tenants.limits.maxProductsPerTenant' },
  maxUsersPerTenant: { labelKey: 'tenants.limits.maxUsersPerTenant' },
  dailyMaxTransactions: { labelKey: 'tenants.limits.dailyMaxTransactions' },
  maxTransactionAmount: { labelKey: 'tenants.limits.maxTransactionAmount', money: true },
  dailyMaxRevenue: { labelKey: 'tenants.limits.dailyMaxRevenue', money: true },
  maxBackupsPerTenant: { labelKey: 'tenants.limits.maxBackupsPerTenant' },
  maxBackupSizeMB: { labelKey: 'tenants.limits.maxBackupSizeMB' },
  maxOfflineTransactions: { labelKey: 'tenants.limits.maxOfflineTransactions' },
};

export type DevLimitUsagePair = { current: number; limit: number };

export function readDevLimitUsage(
  key: DevLimitKey,
  usage: {
    currentProducts: number;
    currentUsers: number;
    currentDailyTransactions: number;
    currentDailyRevenue: number;
    currentBackups: number;
    currentBackupSizeMb: number;
    currentOfflineTransactions: number;
    currentMaxAssignedRegistersPerUser: number;
    limits: {
      maxActiveRegistersPerUser: number;
      maxProductsPerTenant: number;
      maxUsersPerTenant: number;
      dailyMaxTransactions: number;
      maxTransactionAmount: number;
      dailyMaxRevenue: number;
      maxBackupsPerTenant: number;
      maxBackupSizeMB: number;
      maxOfflineTransactions: number;
    };
  }
): DevLimitUsagePair {
  const { limits } = usage;
  switch (key) {
    case 'maxActiveRegistersPerUser':
      return { current: usage.currentMaxAssignedRegistersPerUser, limit: limits.maxActiveRegistersPerUser };
    case 'maxProductsPerTenant':
      return { current: usage.currentProducts, limit: limits.maxProductsPerTenant };
    case 'maxUsersPerTenant':
      return { current: usage.currentUsers, limit: limits.maxUsersPerTenant };
    case 'dailyMaxTransactions':
      return { current: usage.currentDailyTransactions, limit: limits.dailyMaxTransactions };
    case 'maxTransactionAmount':
      return { current: 0, limit: limits.maxTransactionAmount };
    case 'dailyMaxRevenue':
      return { current: usage.currentDailyRevenue, limit: limits.dailyMaxRevenue };
    case 'maxBackupsPerTenant':
      return { current: usage.currentBackups, limit: limits.maxBackupsPerTenant };
    case 'maxBackupSizeMB':
      return { current: usage.currentBackupSizeMb, limit: limits.maxBackupSizeMB };
    case 'maxOfflineTransactions':
      return { current: usage.currentOfflineTransactions, limit: limits.maxOfflineTransactions };
    default:
      return { current: 0, limit: 0 };
  }
}
