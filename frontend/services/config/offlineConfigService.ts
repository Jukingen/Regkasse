import AsyncStorage from '@react-native-async-storage/async-storage';

import { OFFLINE_CONFIG } from '@/constants/offlineConfig';

type OfflineConfigState = {
  [K in keyof typeof OFFLINE_CONFIG]: (typeof OFFLINE_CONFIG)[K];
};

const USER_CONFIG_STORAGE_KEY = 'offline_user_config';

export class OfflineConfigService {
  private static instance: OfflineConfigService;
  private config: OfflineConfigState;

  private constructor() {
    this.config = { ...OFFLINE_CONFIG };
    void this.loadUserConfig();
  }

  static getInstance(): OfflineConfigService {
    if (!OfflineConfigService.instance) {
      OfflineConfigService.instance = new OfflineConfigService();
    }
    return OfflineConfigService.instance;
  }

  /** Get configuration value */
  get<K extends keyof typeof OFFLINE_CONFIG>(key: K): (typeof OFFLINE_CONFIG)[K] {
    return this.config[key];
  }

  /** Load user-specific config from storage */
  private async loadUserConfig(): Promise<void> {
    try {
      const userConfig = await AsyncStorage.getItem(USER_CONFIG_STORAGE_KEY);
      if (userConfig) {
        const parsed = JSON.parse(userConfig) as Partial<OfflineConfigState>;
        this.config = { ...this.config, ...parsed };
      }
    } catch (error) {
      console.warn('Failed to load user config:', error);
    }
  }

  /** Save user-specific config */
  async saveUserConfig(config: Partial<typeof OFFLINE_CONFIG>): Promise<void> {
    this.config = { ...this.config, ...config };
    await AsyncStorage.setItem(USER_CONFIG_STORAGE_KEY, JSON.stringify(this.config));
  }

  /** Expiry time in milliseconds */
  getExpiryMs(): number {
    return this.config.OFFLINE_EXPIRY_HOURS * 60 * 60 * 1000;
  }

  /** Token expiry in milliseconds */
  getTokenExpiryMs(): number {
    return this.config.TOKEN_EXPIRY_HOURS * 60 * 60 * 1000;
  }

  /** Whether offline order snapshots are enabled */
  isOfflineOrdersEnabled(): boolean {
    return this.config.ENABLE_OFFLINE_ORDERS;
  }

  /** Whether non-fiscal offline payments are enabled */
  isOfflinePaymentsEnabled(): boolean {
    return this.config.ENABLE_OFFLINE_PAYMENTS;
  }
}
