import { licenseApi, type TenantLicenseStatusDto } from '../../api/license';
import { formatLicenseRemainingDe } from '../../utils/licenseExpiryRemaining';
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
      validUntil,
    } = data;

    if (canAccess === false) {
      showToast(
        'Lizenz',
        statusMessage ??
          'Lizenz abgelaufen! POS ist gesperrt. Nur Super-Administrator kann entsperren.'
      );
      return false;
    }

    if (isInGracePeriod) {
      showToast(
        'Lizenz',
        statusMessage ??
          `Lizenz abgelaufen. Grace Period: noch ${gracePeriodRemaining} Tage. Bitte verlängern.`
      );
    } else if (daysRemaining <= EXPIRY_WARNING_DAYS && daysRemaining > 0) {
      const remainingLabel =
        formatLicenseRemainingDe(daysRemaining, validUntil) ?? `${daysRemaining} Tagen`;
      showToast('Lizenz', `Lizenz läuft in ${remainingLabel} ab. Bitte rechtzeitig verlängern.`);
    }

    return true;
  } catch (error) {
    if (__DEV__) {
      console.error('License check failed:', error);
    }
    return false;
  }
}
