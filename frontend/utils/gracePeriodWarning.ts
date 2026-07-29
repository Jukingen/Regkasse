import {
  POS_GRACE_MODAL_AUTO_SHOW_DAYS,
  POS_GRACE_URGENT_DAYS,
} from '../constants/licenseGracePeriod';

/** Whether the POS grace modal should auto-open for the current remaining days. */
export function shouldAutoShowGracePeriodModal(
  isGrace: boolean,
  graceDaysRemaining: number
): boolean {
  if (!isGrace) return false;
  if (!Number.isFinite(graceDaysRemaining)) return false;
  return graceDaysRemaining <= POS_GRACE_MODAL_AUTO_SHOW_DAYS;
}

/** Stronger (error) styling for the final grace days. */
export function isGracePeriodWarningUrgent(graceDaysRemaining: number): boolean {
  if (!Number.isFinite(graceDaysRemaining)) return false;
  return graceDaysRemaining <= POS_GRACE_URGENT_DAYS;
}
