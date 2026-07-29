/**
 * Mandant license lockdown — FA surface map.
 *
 * | Layer | Location |
 * | --- | --- |
 * | API write block | `backend/Middleware/LicenseLockdownMiddleware.cs` (pipeline in ApplicationHost) |
 * | Status banner | `LicenseLockdownBanner` via `LicenseStatusBanner` → `LicenseExpiryBanner` |
 * | Renewal modal | `LicenseRenewalModal` + `LicenseRenewalModalHost` + store |
 * | Renewal recovery | `LicenseRenewalRecoveryBanner` (localStorage, 1h) |
 * | Sidebar hide/disable | `shared/sidebarLicenseLockdown.ts` (AdminSidebar) |
 * | Menu visibility API | `hooks/useLicenseMenuVisibility` (aliases + action gate + footer) |
 * | Allowed menus config | `shared/licenseMenuConfig.ts` |
 * | 403 toasts | axios interceptor → `licenseLockdownClient` |
 * | Pre-request UI guard | `hooks/useLicenseGuard` (`guard` / `guardWriteOperation` / `guardAction`) |
 */
export { LicenseRenewalFlow } from '@/features/license/components/LicenseRenewalFlow';
export { LicenseRenewalModal } from '@/features/license/components/LicenseRenewalModal';
export { LicenseRenewalModalHost } from '@/features/license/components/LicenseRenewalModalHost';
export { LicenseLockdownBanner } from '@/components/LicenseLockdownBanner';
export { LicenseStatusBanner } from '@/components/LicenseStatusBanner';
export {
  closeLicenseRenewalModal,
  openLicenseRenewalModal,
  useLicenseRenewalModalStore,
} from '@/features/license/stores/licenseRenewalModalStore';
export {
  handleLicenseExpiredForbidden,
  isLicenseExpiredForbiddenPayload,
  notifyLicenseGuardBlocked,
} from '@/features/license/utils/licenseLockdownClient';
export {
  getConfiguredLicensePaymentUrl,
  redirectToLicensePayment,
  resolveLicensePaymentRedirectTarget,
} from '@/features/license/utils/licensePaymentRedirect';
