import { useLicenseExpiryNotifications } from '../hooks/useLicenseExpiryNotifications';

/**
 * Mounts license local-notification scheduling + Locked/Archived alert inside providers.
 */
export function LicenseExpiryNotificationBridge() {
  useLicenseExpiryNotifications();
  return null;
}
