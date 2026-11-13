import NetInfo from '@react-native-community/netinfo';

import { OfflineSessionManager } from '@/services/auth/offlineSessionManager';
import { OfflineConfigService } from '@/services/config/offlineConfigService';
import { eventEmitter } from '@/utils/eventEmitter';
import { fetchIsNetworkOnline, isNetworkOnline } from '@/utils/isNetworkOnline';

const OFFLINE_TIME_CHECK_MS = 60_000;
const TOKEN_EXPIRY_CHECK_MS = 300_000;

const TOKEN_EXPIRY_WARNING_MESSAGE_DE =
  'Token läuft in Kürze ab. Bitte verbinden Sie sich mit dem Internet.';

export class OfflineNotificationService {
  private static instance: OfflineNotificationService | undefined;
  private readonly config: OfflineConfigService;
  private readonly sessionManager: OfflineSessionManager;
  private readonly warningsShown = new Set<string>();
  private offlineTimeInterval: ReturnType<typeof setInterval> | null = null;
  private tokenExpiryInterval: ReturnType<typeof setInterval> | null = null;
  private netInfoUnsubscribe: (() => void) | null = null;
  private readonly onSyncOnline = (): void => {
    this.warningsShown.clear();
  };

  private constructor() {
    this.config = OfflineConfigService.getInstance();
    this.sessionManager = OfflineSessionManager.getInstance();
    this.setupListeners();
  }

  static getInstance(): OfflineNotificationService {
    if (!OfflineNotificationService.instance) {
      OfflineNotificationService.instance = new OfflineNotificationService();
    }
    return OfflineNotificationService.instance;
  }

  private setupListeners(): void {
    this.offlineTimeInterval = setInterval(() => {
      void this.checkOfflineTime();
    }, OFFLINE_TIME_CHECK_MS);

    this.tokenExpiryInterval = setInterval(() => {
      this.checkTokenExpiry();
    }, TOKEN_EXPIRY_CHECK_MS);

    eventEmitter.on('sync:online', this.onSyncOnline);

    this.netInfoUnsubscribe = NetInfo.addEventListener((state) => {
      if (isNetworkOnline(state)) {
        this.warningsShown.clear();
      }
    });
  }

  private async checkOfflineTime(): Promise<void> {
    const hoursRemaining = this.sessionManager.getRemainingOfflineHours();
    const criticalThreshold = this.config.get('OFFLINE_CRITICAL_HOURS');
    const warningThreshold = this.config.get('OFFLINE_WARNING_HOURS');

    if (hoursRemaining <= criticalThreshold && hoursRemaining > 0) {
      if (!this.warningsShown.has('critical')) {
        this.warningsShown.add('critical');
        eventEmitter.emit('offline:critical', { hoursRemaining });
      }
      return;
    }

    if (hoursRemaining <= warningThreshold && hoursRemaining > criticalThreshold) {
      if (!this.warningsShown.has('warning')) {
        this.warningsShown.add('warning');
        eventEmitter.emit('offline:warning', { hoursRemaining });
      }
    }

    const online = await fetchIsNetworkOnline();
    if (online) {
      this.warningsShown.clear();
    }
  }

  private checkTokenExpiry(): void {
    if (!this.sessionManager.isTokenExpiringSoon()) return;
    if (this.warningsShown.has('token')) return;

    this.warningsShown.add('token');
    eventEmitter.emit('sync:warning', {
      message: TOKEN_EXPIRY_WARNING_MESSAGE_DE,
    });
  }

  destroy(): void {
    if (this.offlineTimeInterval) {
      clearInterval(this.offlineTimeInterval);
      this.offlineTimeInterval = null;
    }

    if (this.tokenExpiryInterval) {
      clearInterval(this.tokenExpiryInterval);
      this.tokenExpiryInterval = null;
    }

    this.netInfoUnsubscribe?.();
    this.netInfoUnsubscribe = null;
    eventEmitter.off('sync:online', this.onSyncOnline);
    this.warningsShown.clear();
  }

  static resetForTests(): void {
    OfflineNotificationService.instance?.destroy();
    OfflineNotificationService.instance = undefined;
  }
}
