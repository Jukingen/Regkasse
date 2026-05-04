import { isValidPosCashRegisterId } from './posCashRegister';

/**
 * Pure client-side gate for disabling the POS pay action. It does not run on the server.
 *
 * Payment creation (`POST /api/pos/payment`) authorizes the body `CashRegisterId` only via
 * `ICashRegisterResolutionService.ValidatePaymentRegisterAsync` (open register, shift conflict,
 * assignment / sole-register rules). It does not call ensure-ready and does not consume `nextAction`.
 *
 * When `POS_ENSURE_READY_ON_ENTRY` is on, this gate additionally mirrors the last ensure-ready
 * `nextAction` / `effectiveRegisterId` from the client cache so the UI stays aligned with session
 * state and typical server rejection reasons — not because the payment endpoint enforces that DTO.
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
  /** From ensure-ready DTO `registerStatus` (e.g. Decommissioned). */
  posReadinessRegisterStatus?: string | null;
  effectiveCashRegisterIdForPayment: string | null;
};

export function computeRegisterGateBlockingPayment(p: PosRegisterPaymentGateParams): boolean {
  const regSt = (p.posReadinessRegisterStatus ?? '').trim().toLowerCase();
  if (regSt === 'decommissioned') {
    return true;
  }

  const hasValidCashRegisterId = isValidPosCashRegisterId(p.effectiveCashRegisterIdForPayment);

  const awaitingPosReadiness =
    p.posEnsureReadyOnEntry &&
    p.enabled &&
    p.posReadinessLoading &&
    !p.posReadinessError &&
    !hasValidCashRegisterId;

  /**
   * Cached ensure-ready says not `ready` (conflict, closed, selection, etc.). Client blocks submit when that
   * cache is present. After a successful cash-register assignment, the readiness provider clears this DTO and
   * re-fetches ensure-ready so stale `select_register` / `open_register` does not block forever.
   * Payment POST remains authoritative (server `ValidatePaymentRegisterAsync` only).
   */
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
   * GET /user/settings is only for persisted assignment in the client; payment uses the register id on the POST body.
   * Register authorization on POST is ValidatePaymentRegisterAsync (not ensure-ready / not nextAction).
   *
   * When ensure-ready already returned `ready` with a valid effectiveRegisterId, do not keep blocking on a failed
   * settings GET (client UX only). When ensure-ready is enabled and settings failed but the client holds a valid id
   * from a successful assignment PUT or readiness merge, likewise do not block solely on the failed settings GET.
   * When ensure-ready is disabled, a failed settings fetch stays a hard client gate (no ensure-ready waive).
   * `posReadinessDeniesPayment` applies cached ensure-ready `nextAction` on the client when the flag is on.
   */
  const settingsFailureBlocksPayment =
    p.settingsLoadFailed &&
    !readinessAllowsPaymentBeforeSettings &&
    (!hasValidCashRegisterId || !p.posEnsureReadyOnEntry);

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
