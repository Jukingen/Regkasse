import { licenseApi, type TenantLicenseStatusDto } from '../../api/license';
import { showToast } from '../../utils/toast';

const EXPIRY_WARNING_DAYS = 14;

/**
 * Post-login mandant license gate. Uses GET /api/license/status?tenantId=…
 * Returns false when access is blocked; shows German warnings for grace / pre-expiry.
 */
export async function checkLicenseStatus(tenantId: string): Promise<boolean> {
  try {
    const data: TenantLicenseStatusDto = await licenseApi.getTenantLicenseStatus(tenantId);
    const {
      canAccess,
      statusMessage,
      daysRemaining,
      isInGracePeriod,
      gracePeriodRemaining,
    } = data;

    if (canAccess === false) {
      showToast('Lizenz', statusMessage ?? 'Mandantenlizenz ungültig. Bitte verlängern.');
      return false;
    }

    if (isInGracePeriod) {
      showToast(
        'Lizenz',
        `Lizenz abgelaufen. Grace Period: noch ${gracePeriodRemaining} Tage. Bitte verlängern.`,
      );
    } else if (daysRemaining <= EXPIRY_WARNING_DAYS && daysRemaining > 0) {
      showToast(
        'Lizenz',
        `Lizenz läuft in ${daysRemaining} Tagen ab. Bitte rechtzeitig verlängern.`,
      );
    }

    return true;
  } catch (error) {
    if (__DEV__) {
      // eslint-disable-next-line no-console
      console.error('License check failed:', error);
    }
    return false;
  }
}
