import { isExpoPublicOfflineGutscheinEnabled } from '@/constants/expoPublicEnv';
import { OFFLINE_CONFIG } from '@/constants/offlineConfig';
import { storage } from '@/utils/storage';

type OfflineConfigState = {
  [K in keyof typeof OFFLINE_CONFIG]: K extends 'ENABLE_OFFLINE_GUTSCHEIN'
    ? boolean
    : (typeof OFFLINE_CONFIG)[K];
};

const USER_CONFIG_STORAGE_KEY = 'offline_user_config';

export class OfflineConfigService {
  private static instance: OfflineConfigService;
  private config: OfflineConfigState;

  private constructor() {
    this.config = {
      ...OFFLINE_CONFIG,
      ENABLE_OFFLINE_GUTSCHEIN: isExpoPublicOfflineGutscheinEnabled(),
    };
    void this.loadUserConfig();
  }

  static getInstance(): OfflineConfigService {
    if (!OfflineConfigService.instance) {
      OfflineConfigService.instance = new OfflineConfigService();
    }
    return OfflineConfigService.instance;
  }

  /** Get configuration value */
  get<K extends keyof OfflineConfigState>(key: K): OfflineConfigState[K] {
    return this.config[key];
  }

  /** Load user-specific config from storage */
  private async loadUserConfig(): Promise<void> {
    try {
      const parsed = await storage.getJson<Partial<OfflineConfigState>>(USER_CONFIG_STORAGE_KEY);
      if (parsed) {
        const { ENABLE_OFFLINE_GUTSCHEIN: _ignoredGutschein, ...rest } = parsed;
        this.config = {
          ...this.config,
          ...rest,
          ENABLE_OFFLINE_GUTSCHEIN: isExpoPublicOfflineGutscheinEnabled(),
        };
      }
    } catch (error) {
      console.warn('Failed to load user config:', error);
    }
  }

  /** Save user-specific config. Gutschein offline cannot be enabled via stored user config. */
  async saveUserConfig(config: Partial<typeof OFFLINE_CONFIG>): Promise<void> {
    const { ENABLE_OFFLINE_GUTSCHEIN: _ignoredGutschein, ...rest } = config;
    this.config = {
      ...this.config,
      ...rest,
      ENABLE_OFFLINE_GUTSCHEIN: isExpoPublicOfflineGutscheinEnabled(),
    };
    await storage.setJson(USER_CONFIG_STORAGE_KEY, this.config);
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

  /** Whether Gutschein may be queued offline (env only; default false). */
  isOfflineGutscheinEnabled(): boolean {
    return isExpoPublicOfflineGutscheinEnabled();
  }
}
