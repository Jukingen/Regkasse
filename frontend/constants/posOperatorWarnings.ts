/**
 * Non-blocking POS operator warnings (thresholds and session-wide TSE check streak).
 * UI copy stays German in components / checkout i18n per POS rules.
 */

/** Cash sale total (EUR) from which we ask for an extra confirmation before fiscal pay. */
export const POS_LARGE_CASH_WARN_THRESHOLD_EUR = 500;

/** Show persistent TSE warning in payment UI after this many consecutive failed status checks (session-wide). */
export const POS_TSE_STATUS_FAILURE_WARN_STREAK = 2;

let posTseStatusCheckFailureStreak = 0;

/** Call after each checkTseStatus outcome while payment modal cares about TSE health. */
export function registerPosTseStatusCheckOutcome(ok: boolean): number {
  if (ok) {
    posTseStatusCheckFailureStreak = 0;
  } else {
    posTseStatusCheckFailureStreak = Math.min(posTseStatusCheckFailureStreak + 1, 99);
  }
  return posTseStatusCheckFailureStreak;
}

export function getPosTseStatusCheckFailureStreak(): number {
  return posTseStatusCheckFailureStreak;
}
