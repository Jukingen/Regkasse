/**
 * FinanzOnline reconciliation row triage — uses only API-provided status strings.
 * Aligns with the same status checks as the "Erneut senden" button; does not infer server-side retry eligibility beyond that.
 */
import type { FinanzOnlineReconciliationItemDto } from '@/api/generated/model/finanzOnlineReconciliationItemDto';
import { OPERATOR_FO_RETRY_UI_COPY } from '@/shared/operatorTruthCopy';

export type FinanzOnlineRetryUiState =
  'retry_available' | 'submitted_no_retry' | 'other_status' | 'empty';

export function getFinanzOnlineRetryUiState(
  status: string | null | undefined
): FinanzOnlineRetryUiState {
  const s = status?.trim();
  if (!s) return 'empty';
  if (s === 'Pending' || s === 'Failed' || s === 'NeedsReconciliation') return 'retry_available';
  if (s === 'Submitted') return 'submitted_no_retry';
  return 'other_status';
}

/**
 * Exact mirror of the reconciliation table "Erneut senden" affordance: non-empty payment id and
 * retry UI state. No separate backend `retryable` flag on the list DTO.
 */
export function isFinanzOnlineRetryButtonContract(row: FinanzOnlineReconciliationItemDto): boolean {
  const pid = row.paymentId?.trim();
  if (!pid) return false;
  return getFinanzOnlineRetryUiState(row.finanzOnlineStatus) === 'retry_available';
}

/** German operator copy for table tags / tooltips (UI mirrors retry button, not a separate backend flag). */
export function finanzOnlineRetryUiPresentation(state: FinanzOnlineRetryUiState): {
  tagLabel: string;
  tagColor: string;
  tooltip: string;
} {
  switch (state) {
    case 'retry_available':
      return OPERATOR_FO_RETRY_UI_COPY.retryAvailable;
    case 'submitted_no_retry':
      return OPERATOR_FO_RETRY_UI_COPY.submittedNoRetry;
    case 'other_status':
      return OPERATOR_FO_RETRY_UI_COPY.otherStatus;
    default:
      return OPERATOR_FO_RETRY_UI_COPY.empty;
  }
}
