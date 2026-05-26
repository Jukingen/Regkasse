import {
  buildAdminUrl,
  getAdminBaseUrl,
  isAdminTargetAvailable,
  type AdminTarget,
  type AdminTargetContext,
} from './adminRoutes';
import {
  openAdmin as openAdminUrl,
  type OpenAdminOptions,
} from '@/utils/openAdmin';

export interface IAdminRedirector {
  openAdmin(target: AdminTarget, context?: AdminTargetContext, options?: OpenAdminOptions): Promise<boolean>;
  isAvailable(target: AdminTarget, context?: AdminTargetContext): boolean;
  getAdminBaseUrl(): string;
  buildUrl(target: AdminTarget, context?: AdminTargetContext): string;
}

export const adminRedirector: IAdminRedirector = {
  async openAdmin(target, context, options) {
    return openAdminUrl(target, context, options);
  },

  isAvailable(target, context) {
    return isAdminTargetAvailable(target, context);
  },

  getAdminBaseUrl() {
    return getAdminBaseUrl();
  },

  buildUrl(target, context) {
    return buildAdminUrl(target, context);
  },
};

export { openAdmin, openLicenseExtension } from '@/utils/openAdmin';
export { buildAdminUrl, getAdminBaseUrl, isAdminTargetAvailable };
export type { OpenAdminOptions } from '@/utils/openAdmin';
export type { AdminTarget, AdminTargetContext };
