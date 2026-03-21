import { isValidPosCashRegisterId } from './posCashRegister';

export type PosSelectableRegisterListFetchParams = {
  enabled: boolean;
  cashRegisterResolved: boolean;
  /** Current assignment candidate from settings merge / readiness / optimistic persist. */
  cashRegisterId: string | null;
  readinessNextAction: string | null | undefined;
  readinessEffectiveRegisterId: string | null | undefined;
  settingsLoadFailed: boolean;
  posEnsureReadyOnEntry: boolean;
  posReadinessLoading: boolean;
  posReadinessError: boolean;
};

/**
 * Whether to run GET /api/pos/cash-register/selectable for the POS assignment hook.
 *
 * Data sources for the client merge (highest priority first for UX; payment POST still validates id separately):
 * 1. POST /api/pos/cash-register/ensure-ready — nextAction + effectiveRegisterId (when POS_ENSURE_READY_ON_ENTRY).
 * 2. GET /user/settings — persisted cashRegisterId (merged when load succeeds).
 * 3. GET /api/pos/cash-register/selectable (`fetchPosSelectableRegisters` / ListSelectableForPosPickerAsync) — recovery when (2) is missing or failed; optional auto-persist when exactly one row.
 *
 * When settings load fails, defer (3) until ensure-ready is not in-flight (unless it already errored), so readiness outcome is applied before spending a list request and to avoid racing auto-persist against a pending readiness response.
 */
export function shouldFetchPosSelectableRegisterList(p: PosSelectableRegisterListFetchParams): boolean {
  if (!p.enabled || !p.cashRegisterResolved) return false;

  const readinessReady =
    p.readinessNextAction === 'ready' &&
    isValidPosCashRegisterId(p.readinessEffectiveRegisterId ?? null);

  if (isValidPosCashRegisterId(p.cashRegisterId)) return false;
  if (readinessReady) return false;

  const deferUntilReadinessKnown =
    p.settingsLoadFailed &&
    p.posEnsureReadyOnEntry &&
    p.posReadinessLoading &&
    !p.posReadinessError;

  if (deferUntilReadinessKnown) return false;

  return true;
}
