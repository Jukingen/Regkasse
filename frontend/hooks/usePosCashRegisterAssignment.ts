import { useState, useEffect, useCallback, useRef, useMemo } from 'react';
import { Alert } from 'react-native';

import { POS_ENSURE_READY_ON_ENTRY } from '../constants/posFeatureFlags';
import { usePosRegisterReadiness } from '../contexts/PosRegisterReadinessContext';
import { getUserSettings, updateCashRegisterConfig } from '../services/api/userSettingsService';
import {
  fetchPosSelectableRegisters,
  type CashRegisterSelectableRow,
  type PosSelectableEmptyReason,
} from '../services/api/cashRegisterService';
import { isValidPosCashRegisterId } from '../utils/posCashRegister';
import { computeRegisterGateBlockingPayment } from '../utils/posRegisterPaymentGate';
import { debugPosPaymentTrace } from '../utils/debugPosPaymentTrace';
import {
  classifyRegisterListError,
  type RegisterListFailureKind,
} from '../utils/registerListError';
import {
  cashRegisterPersistFailureAlertDe,
  isCashRegisterAssignmentRejectedByBackend,
  shouldRetainOptimisticCashRegisterAfterPersistFailure,
} from '../utils/cashRegisterAssignmentPersistPolicy';
import { shouldFetchPosSelectableRegisterList } from '../utils/posRegisterAssignmentFetchPolicy';

/**
 * Builds the `CashRegisterId` sent on POS payment: merges ensure-ready (when enabled), GET /user/settings (read-only;
 * login runs POST /user/settings/bootstrap for sole-assign), and `fetchPosSelectableRegisters` → `POS_SELECTABLE_REGISTERS_PATH`
 * (see `shouldFetchPosSelectableRegisterList`).
 * Picklist rows are server-filtered: open, not on another user's shift (closed rows are never returned);
 * auto-persist runs when exactly one selectable row is returned.
 *
 * The payment API does not re-run ensure-ready; it validates the body id via ValidatePaymentRegisterAsync.
 */
export function usePosCashRegisterAssignment(enabled: boolean) {
  const posReadiness = usePosRegisterReadiness();
  const [cashRegisterId, setCashRegisterId] = useState<string | null>(null);
  const [cashRegisterResolved, setCashRegisterResolved] = useState(false);
  const [registerPicklist, setRegisterPicklist] = useState<CashRegisterSelectableRow[]>([]);
  const [registerListLoading, setRegisterListLoading] = useState(false);
  const [savingRegisterId, setSavingRegisterId] = useState<string | null>(null);
  /** Set when GET /pos/cash-register/selectable fails; null means last fetch succeeded or not yet failed. */
  const [registerListFailureKind, setRegisterListFailureKind] =
    useState<RegisterListFailureKind | null>(null);
  /** Server hint when selectable list is empty (successful GET). */
  const [registerListEmptyReason, setRegisterListEmptyReason] =
    useState<PosSelectableEmptyReason>(null);
  const [registerListRetryToken, setRegisterListRetryToken] = useState(0);
  const [settingsRetryToken, setSettingsRetryToken] = useState(0);
  const [settingsLoadFailed, setSettingsLoadFailed] = useState(false);
  const prevResolvedRef = useRef(false);
  const readinessSnapshotRef = useRef(posReadiness.data);
  useEffect(() => {
    readinessSnapshotRef.current = posReadiness.data;
  }, [posReadiness.data]);

  const refetchRegisterList = useCallback(() => {
    setRegisterListRetryToken((n) => n + 1);
  }, []);

  const retryUserSettingsLoad = useCallback(() => {
    setSettingsLoadFailed(false);
    setSettingsRetryToken((n) => n + 1);
    void posReadiness.refreshAsync();
  }, [posReadiness]);

  useEffect(() => {
    if (!enabled) {
      setCashRegisterId(null);
      setCashRegisterResolved(false);
      setRegisterPicklist([]);
      setRegisterListEmptyReason(null);
      setRegisterListLoading(false);
      setRegisterListFailureKind(null);
      setRegisterListRetryToken(0);
      setSettingsLoadFailed(false);
      setSettingsRetryToken(0);
      prevResolvedRef.current = false;
      return;
    }

    setCashRegisterResolved(false);
    setSettingsLoadFailed(false);
    debugPosPaymentTrace('settings_load_start', { enabled });
    getUserSettings()
      .then((s) => {
        const id = s.cashRegisterId?.trim();
        const invalid = !id || id === '00000000-0000-0000-0000-000000000000';
        setCashRegisterId(invalid ? null : id);
        setSettingsLoadFailed(false);
        debugPosPaymentTrace('settings_loaded', {
          cashRegisterId: invalid ? null : id,
          userId: s.userId,
        });
      })
      .catch((e) => {
        debugPosPaymentTrace('settings_load_error', { message: e instanceof Error ? e.message : String(e) });
        setCashRegisterId(null);
        setSettingsLoadFailed(true);
      })
      .finally(() => setCashRegisterResolved(true));
  }, [enabled, settingsRetryToken]);

  useEffect(() => {
    if (!enabled) return;
    const d = posReadiness.data;
    if (d?.nextAction === 'ready' && d.effectiveRegisterId && isValidPosCashRegisterId(d.effectiveRegisterId)) {
      setCashRegisterId((prev) => (isValidPosCashRegisterId(prev) ? prev : d.effectiveRegisterId!.trim()));
    }
  }, [enabled, posReadiness.data]);

  useEffect(() => {
    if (cashRegisterResolved && !prevResolvedRef.current) {
      debugPosPaymentTrace('cash_register_resolved_transition', {
        from: false,
        to: true,
        cashRegisterId,
        hasValidCashRegisterId: isValidPosCashRegisterId(cashRegisterId),
      });
    }
    prevResolvedRef.current = cashRegisterResolved;
  }, [cashRegisterResolved, cashRegisterId]);

  useEffect(() => {
    const fetchList = shouldFetchPosSelectableRegisterList({
      enabled,
      cashRegisterResolved,
      cashRegisterId,
      readinessNextAction: posReadiness.data?.nextAction ?? null,
      readinessEffectiveRegisterId: posReadiness.data?.effectiveRegisterId ?? null,
      settingsLoadFailed,
      posEnsureReadyOnEntry: POS_ENSURE_READY_ON_ENTRY,
      posReadinessLoading: posReadiness.loading,
      posReadinessError: !!posReadiness.error,
    });

    if (!fetchList) {
      if (!enabled) {
        setRegisterPicklist([]);
        setRegisterListEmptyReason(null);
      }
      return;
    }

    let cancelled = false;
    setRegisterListLoading(true);
    setRegisterListFailureKind(null);
    setRegisterListEmptyReason(null);
    debugPosPaymentTrace('register_list_load_start', {});

    fetchPosSelectableRegisters()
      .then(async ({ registers: rows, emptyReason }) => {
        if (cancelled) return;
        setRegisterPicklist(rows);
        setRegisterListEmptyReason(emptyReason);
        setRegisterListFailureKind(null);
        debugPosPaymentTrace('register_list_loaded', { count: rows.length, emptyReason });

        // Single selectable row from /pos/cash-register/selectable → safe implicit assignment after persist.
        if (rows.length !== 1) return;

        const onlyId = rows[0]?.id?.trim();
        if (!onlyId) return;

        try {
          const updated = await updateCashRegisterConfig({ cashRegisterId: onlyId });
          if (cancelled) return;
          const next = updated.cashRegisterId?.trim();
          if (next && next !== '00000000-0000-0000-0000-000000000000') {
            setCashRegisterId(next);
          }
          await posReadiness.refreshAsync();
        } catch (e) {
          if (!cancelled) {
            console.warn('[usePosCashRegisterAssignment] Auto-assign single cash register failed:', e);
            void posReadiness.refreshAsync();
          }
        }
      })
      .catch((e) => {
        console.warn('[usePosCashRegisterAssignment] Cash register list failed:', e);
        const kind = classifyRegisterListError(e);
        debugPosPaymentTrace('register_list_load_error', {
          message: e instanceof Error ? e.message : String(e),
          kind,
        });
        if (!cancelled) {
          setRegisterPicklist([]);
          setRegisterListEmptyReason(null);
          setRegisterListFailureKind(kind);
        }
      })
      .finally(() => {
        if (!cancelled) setRegisterListLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [
    enabled,
    cashRegisterResolved,
    settingsLoadFailed,
    cashRegisterId,
    registerListRetryToken,
    posReadiness.data,
    posReadiness.loading,
    posReadiness.error,
    posReadiness.refresh,
    posReadiness.refreshAsync,
  ]);

  const handlePersistCashRegister = useCallback(async (id: string) => {
    const trimmed = id.trim();
    if (!trimmed) return;
    const revertTo = cashRegisterId;
    setSavingRegisterId(trimmed);
    setCashRegisterId(trimmed);
    try {
      const updated = await updateCashRegisterConfig({ cashRegisterId: trimmed });
      const next = updated.cashRegisterId?.trim();
      if (next && next !== '00000000-0000-0000-0000-000000000000') {
        setCashRegisterId(next);
      } else {
        setCashRegisterId(trimmed);
      }
      await posReadiness.refreshAsync();
      Alert.alert('Gespeichert', 'Kasse wurde zugewiesen.');
    } catch (e) {
      console.warn('[usePosCashRegisterAssignment] Failed to persist cash register:', e);
      const snap = readinessSnapshotRef.current;
      const retain = shouldRetainOptimisticCashRegisterAfterPersistFailure(e, {
        nextAction: snap?.nextAction,
        effectiveRegisterId: snap?.effectiveRegisterId,
        attemptedRegisterId: trimmed,
      });
      if (retain) {
        setCashRegisterId(trimmed);
      } else {
        setCashRegisterId(revertTo);
      }
      if (!retain) {
        void posReadiness.refreshAsync();
      }
      const { title, message } = cashRegisterPersistFailureAlertDe(e, retain);
      Alert.alert(title, message);
    } finally {
      setSavingRegisterId(null);
    }
  }, [cashRegisterId, posReadiness.refreshAsync]);

  const effectiveCashRegisterIdForPayment = useMemo(() => {
    if (isValidPosCashRegisterId(cashRegisterId)) return cashRegisterId!.trim();
    const rid = posReadiness.data?.effectiveRegisterId?.trim() ?? null;
    if (posReadiness.data?.nextAction === 'ready' && isValidPosCashRegisterId(rid)) return rid;
    return null;
  }, [cashRegisterId, posReadiness.data]);

  const hasValidCashRegisterId = isValidPosCashRegisterId(effectiveCashRegisterIdForPayment);

  const isRegisterGateBlockingPayment = computeRegisterGateBlockingPayment({
    enabled,
    posEnsureReadyOnEntry: POS_ENSURE_READY_ON_ENTRY,
    cashRegisterResolved,
    settingsLoadFailed,
    posReadinessLoading: posReadiness.loading,
    posReadinessError: !!posReadiness.error,
    posReadinessNextAction: posReadiness.data?.nextAction,
    posReadinessEffectiveRegisterId: posReadiness.data?.effectiveRegisterId,
    posReadinessRegisterStatus: posReadiness.data?.registerStatus ?? null,
    effectiveCashRegisterIdForPayment,
  });

  return {
    cashRegisterId: effectiveCashRegisterIdForPayment,
    cashRegisterResolved,
    settingsLoadFailed,
    retryUserSettingsLoad,
    registerPicklist,
    registerListLoading,
    registerListFailureKind,
    registerListEmptyReason,
    refetchRegisterList,
    savingRegisterId,
    hasValidCashRegisterId,
    isRegisterGateBlockingPayment,
    handlePersistCashRegister,
    refreshPosReadiness: posReadiness.refresh,
    posReadinessLoading: posReadiness.loading,
    posReadinessError: !!posReadiness.error,
    posReadinessNextAction: posReadiness.data?.nextAction ?? null,
    posReadinessMessageCode: posReadiness.data?.messageCode ?? null,
    posReadinessRegisterStatus: posReadiness.data?.registerStatus ?? null,
  };
}

/** Alias for callers that prefer the “resolver” naming. */
export const useResolvedCashRegisterForPayment = usePosCashRegisterAssignment;
