import { useState, useEffect, useCallback, useRef, useMemo } from 'react';
import { Alert } from 'react-native';

import { POS_ENSURE_READY_ON_ENTRY } from '../constants/posFeatureFlags';
import { usePosRegisterReadiness } from '../contexts/PosRegisterReadinessContext';
import { getUserSettings, updateCashRegisterConfig } from '../services/api/userSettingsService';
import { listPosCashRegisters, type CashRegisterRow } from '../services/api/cashRegisterService';
import { isValidPosCashRegisterId } from '../utils/posCashRegister';
import { computeRegisterGateBlockingPayment } from '../utils/posRegisterPaymentGate';
import { debugPosPaymentTrace } from '../utils/debugPosPaymentTrace';
import {
  classifyRegisterListError,
  type RegisterListFailureKind,
} from '../utils/registerListError';

/**
 * Resolves cash register for POST /api/pos/payment: POS ensure-ready (layout) + user settings + optional list/auto-assign.
 */
export function usePosCashRegisterAssignment(enabled: boolean) {
  const posReadiness = usePosRegisterReadiness();
  const [cashRegisterId, setCashRegisterId] = useState<string | null>(null);
  const [cashRegisterResolved, setCashRegisterResolved] = useState(false);
  const [registerPicklist, setRegisterPicklist] = useState<CashRegisterRow[]>([]);
  const [registerListLoading, setRegisterListLoading] = useState(false);
  const [savingRegisterId, setSavingRegisterId] = useState<string | null>(null);
  /** Set when GET /CashRegister fails; null means last fetch succeeded or not yet failed. */
  const [registerListFailureKind, setRegisterListFailureKind] =
    useState<RegisterListFailureKind | null>(null);
  const [registerListRetryToken, setRegisterListRetryToken] = useState(0);
  const [settingsRetryToken, setSettingsRetryToken] = useState(0);
  const [settingsLoadFailed, setSettingsLoadFailed] = useState(false);
  const prevResolvedRef = useRef(false);

  const refetchRegisterList = useCallback(() => {
    setRegisterListRetryToken((n) => n + 1);
  }, []);

  const retryUserSettingsLoad = useCallback(() => {
    setSettingsLoadFailed(false);
    setSettingsRetryToken((n) => n + 1);
    posReadiness.refresh();
  }, [posReadiness]);

  useEffect(() => {
    if (!enabled) {
      setCashRegisterId(null);
      setCashRegisterResolved(false);
      setRegisterPicklist([]);
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
    const readinessReady =
      posReadiness.data?.nextAction === 'ready' &&
      isValidPosCashRegisterId(posReadiness.data.effectiveRegisterId ?? null);

    if (!enabled || !cashRegisterResolved || settingsLoadFailed || cashRegisterId || readinessReady) {
      if (!enabled) setRegisterPicklist([]);
      return;
    }

    let cancelled = false;
    setRegisterListLoading(true);
    setRegisterListFailureKind(null);
    debugPosPaymentTrace('register_list_load_start', {});

    listPosCashRegisters()
      .then(async (rows) => {
        if (cancelled) return;
        setRegisterPicklist(rows);
        setRegisterListFailureKind(null);
        debugPosPaymentTrace('register_list_loaded', { count: rows.length });

        if (rows.length !== 1) return;

        const onlyId = rows[0]?.id?.trim();
        if (!onlyId) return;

        try {
          if (!cancelled) setCashRegisterId(onlyId);
          const updated = await updateCashRegisterConfig({ cashRegisterId: onlyId });
          if (cancelled) return;
          const next = updated.cashRegisterId?.trim();
          if (next && next !== '00000000-0000-0000-0000-000000000000') {
            setCashRegisterId(next);
          } else {
            setCashRegisterId(onlyId);
          }
        } catch (e) {
          if (!cancelled) {
            console.warn('[usePosCashRegisterAssignment] Auto-assign single cash register failed:', e);
            setCashRegisterId(onlyId);
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
          setRegisterListFailureKind(kind);
        }
      })
      .finally(() => {
        if (!cancelled) setRegisterListLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [enabled, cashRegisterResolved, settingsLoadFailed, cashRegisterId, registerListRetryToken, posReadiness.data]);

  const handlePersistCashRegister = useCallback(async (id: string) => {
    const trimmed = id.trim();
    if (!trimmed) return;
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
      Alert.alert('Gespeichert', 'Kasse wurde zugewiesen.');
    } catch (e) {
      console.warn('[usePosCashRegisterAssignment] Failed to persist cash register:', e);
      setCashRegisterId(trimmed);
      Alert.alert(
        'Hinweis',
        'Kasse konnte nicht im Profil gespeichert werden. Die gewählte Kasse wird für diese Sitzung trotzdem für die Zahlung verwendet.'
      );
    } finally {
      setSavingRegisterId(null);
    }
  }, []);

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
  };
}

/** Alias for callers that prefer the “resolver” naming. */
export const useResolvedCashRegisterForPayment = usePosCashRegisterAssignment;
