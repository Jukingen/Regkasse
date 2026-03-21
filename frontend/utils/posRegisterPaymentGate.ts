import { isValidPosCashRegisterId } from './posCashRegister';

/**
 * Pure gate: when POS ensure-ready returns `ready` with a valid register id before
 * user settings finish loading, payment must not stay blocked on `cashRegisterResolved === false`.
 */
export type PosRegisterPaymentGateParams = {
  enabled: boolean;
  posEnsureReadyOnEntry: boolean;
  cashRegisterResolved: boolean;
  settingsLoadFailed: boolean;
  posReadinessLoading: boolean;
  posReadinessError: boolean;
  posReadinessNextAction: string | null | undefined;
  posReadinessEffectiveRegisterId: string | null | undefined;
  effectiveCashRegisterIdForPayment: string | null;
};

export function computeRegisterGateBlockingPayment(p: PosRegisterPaymentGateParams): boolean {
  const hasValidCashRegisterId = isValidPosCashRegisterId(p.effectiveCashRegisterIdForPayment);

  const awaitingPosReadiness =
    p.posEnsureReadyOnEntry &&
    p.enabled &&
    p.posReadinessLoading &&
    !p.posReadinessError &&
    !hasValidCashRegisterId;

  /** Server says POS is not payment-ready (conflict, closed, selection, etc.); block even if profile still has a GUID. */
  const posReadinessDeniesPayment =
    p.posEnsureReadyOnEntry &&
    p.enabled &&
    typeof p.posReadinessNextAction === 'string' &&
    p.posReadinessNextAction.length > 0 &&
    p.posReadinessNextAction !== 'ready';

  const readinessAllowsPaymentBeforeSettings =
    p.posEnsureReadyOnEntry &&
    p.posReadinessNextAction === 'ready' &&
    isValidPosCashRegisterId(p.posReadinessEffectiveRegisterId ?? null) &&
    !p.posReadinessError;

  /**
   * GET /user/settings carries profile + persisted cashRegisterId but is not part of the fiscal payment contract:
   * POST /api/pos/payment validates the body register server-side. When ensure-ready already returned `ready`
   * with a valid effectiveRegisterId, blocking on settings fetch failure only hurts POS without improving correctness.
   */
  const settingsFailureBlocksPayment =
    p.settingsLoadFailed && !readinessAllowsPaymentBeforeSettings;

  const settingsOrResolutionBlocking =
    settingsFailureBlocksPayment ||
    (!p.cashRegisterResolved && !readinessAllowsPaymentBeforeSettings);

  return (
    settingsOrResolutionBlocking ||
    !hasValidCashRegisterId ||
    awaitingPosReadiness ||
    posReadinessDeniesPayment
  );
}
