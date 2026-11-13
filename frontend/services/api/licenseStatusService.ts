import { licenseApi, type TenantLicenseStatusDto } from '../../api/license';
import { formatLicenseRemainingDe } from '../../utils/licenseExpiryRemaining';
import { saveLicenseLockoutSnapshot } from '../../utils/licenseLockoutSnapshot';
import { showToast } from '../../utils/toast';

const EXPIRY_WARNING_DAYS = 14;

/**
 * Post-login mandant license gate. Uses GET /api/license/status?tenantId=…
 * Returns false when access is blocked; shows German warnings for grace / pre-expiry.
 * Lockout details are persisted for the license-expired screen (no toast — full-screen UX).
 */
export async function checkLicenseStatus(tenantId: string): Promise<boolean> {
  try {
    const data: TenantLicenseStatusDto = await licenseApi.getTenantLicenseStatus(tenantId);
    const {
      canAccess,
      statusMessage,
      daysRemaining,
      daysOverdue,
      isInGracePeriod,
      gracePeriodRemaining,
      validUntil,
    } = data;

    if (canAccess === false) {
      await saveLicenseLockoutSnapshot(
        typeof daysOverdue === 'number' && Number.isFinite(daysOverdue) ? daysOverdue : 0
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
