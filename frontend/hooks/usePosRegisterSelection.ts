import { useCallback, useEffect, useMemo, useState } from 'react';

import { usePosRegisterReadiness } from '../contexts/PosRegisterReadinessContext';
import { usePosStatusOverview } from '../contexts/PosStatusOverviewContext';
import {
  fetchPosSelectableRegisters,
  type CashRegisterSelectableRow,
  type PosSelectableEmptyReason,
} from '../services/api/cashRegisterService';
import { getUserSettings, updateCashRegisterConfig } from '../services/api/userSettingsService';
import { isValidPosCashRegisterId } from '../utils/posCashRegister';
import { classifyRegisterListError, type RegisterListFailureKind } from '../utils/registerListError';

export type UsePosRegisterSelectionResult = {
  /** Resolved register id (settings assignment or ensure-ready effective id). */
  effectiveRegisterId: string | null;
  registers: CashRegisterSelectableRow[];
  registersLoading: boolean;
  registersEmptyReason: PosSelectableEmptyReason;
  registersListFailure: RegisterListFailureKind | null;
  settingsLoading: boolean;
  readinessLoading: boolean;
  isLoading: boolean;
  savingRegisterId: string | null;
  settingsLoadFailed: boolean;
  selectRegister: (registerId: string) => Promise<boolean>;
  reloadSettings: () => Promise<void>;
  reloadRegisters: () => void;
  refreshReadiness: () => void;
};

function resolveEffectiveRegisterId(
  assignedId: string | null,
  readinessId?: string | null,
  overviewId?: string | null
): string | null {
  for (const id of [assignedId, readinessId, overviewId]) {
    if (isValidPosCashRegisterId(id)) return id!.trim();
  }
  return null;
}

/** Settings / POS: load selectable registers and persist user assignment. */
export function usePosRegisterSelection(): UsePosRegisterSelectionResult {
  const posReadiness = usePosRegisterReadiness();
  const { cashRegister: overviewRegister } = usePosStatusOverview();

  const [assignedId, setAssignedId] = useState<string | null>(null);
  const [settingsLoading, setSettingsLoading] = useState(true);
  const [settingsLoadFailed, setSettingsLoadFailed] = useState(false);
  const [registers, setRegisters] = useState<CashRegisterSelectableRow[]>([]);
  const [registersLoading, setRegistersLoading] = useState(false);
  const [registersEmptyReason, setRegistersEmptyReason] = useState<PosSelectableEmptyReason>(null);
  const [registersListFailure, setRegistersListFailure] = useState<RegisterListFailureKind | null>(null);
  const [registersRetryToken, setRegistersRetryToken] = useState(0);
  const [savingRegisterId, setSavingRegisterId] = useState<string | null>(null);

  const reloadSettings = useCallback(async () => {
    setSettingsLoading(true);
    setSettingsLoadFailed(false);
    try {
      const s = await getUserSettings();
      const id = s.cashRegisterId?.trim();
      const invalid = !id || id === '00000000-0000-0000-0000-000000000000';
      setAssignedId(invalid ? null : id);
    } catch {
      setSettingsLoadFailed(true);
      setAssignedId(null);
    } finally {
      setSettingsLoading(false);
    }
  }, []);

  useEffect(() => {
    void reloadSettings();
  }, [reloadSettings]);

  const effectiveRegisterId = useMemo(
    () =>
      resolveEffectiveRegisterId(
        assignedId,
        posReadiness.data?.effectiveRegisterId,
        overviewRegister?.effectiveRegisterId
      ),
    [assignedId, posReadiness.data?.effectiveRegisterId, overviewRegister?.effectiveRegisterId]
  );

  const shouldLoadRegisters = !settingsLoading;

  useEffect(() => {
    if (!shouldLoadRegisters) {
      setRegisters([]);
      setRegistersEmptyReason(null);
      setRegistersListFailure(null);
      setRegistersLoading(false);
      return;
    }

    let cancelled = false;
    setRegistersLoading(true);
    setRegistersListFailure(null);
    setRegistersEmptyReason(null);

    void fetchPosSelectableRegisters()
      .then(({ registers: rows, emptyReason }) => {
        if (cancelled) return;
        setRegisters(rows);
        setRegistersEmptyReason(emptyReason);
      })
      .catch((e) => {
        if (cancelled) return;
        setRegisters([]);
        setRegistersEmptyReason(null);
        setRegistersListFailure(classifyRegisterListError(e));
      })
      .finally(() => {
        if (!cancelled) setRegistersLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [shouldLoadRegisters, registersRetryToken]);

  const selectRegister = useCallback(
    async (registerId: string): Promise<boolean> => {
      const trimmedId = registerId.trim();
      if (!isValidPosCashRegisterId(trimmedId)) return false;

      setSavingRegisterId(trimmedId);
      try {
        const updated = await updateCashRegisterConfig({ cashRegisterId: trimmedId });
        const next = updated.cashRegisterId?.trim();
        setAssignedId(isValidPosCashRegisterId(next) ? next!.trim() : trimmedId);
        await posReadiness.refreshAsync();
        return true;
      } catch {
        return false;
      } finally {
        setSavingRegisterId(null);
      }
    },
    [posReadiness]
  );

  const reloadRegisters = useCallback(() => {
    setRegistersRetryToken((n) => n + 1);
  }, []);

  const refreshReadiness = useCallback(() => {
    posReadiness.refresh();
  }, [posReadiness]);

  return {
    effectiveRegisterId,
    registers,
    registersLoading,
    registersEmptyReason,
    registersListFailure,
    settingsLoading,
    readinessLoading: posReadiness.loading,
    isLoading: settingsLoading || posReadiness.loading,
    savingRegisterId,
    settingsLoadFailed,
    selectRegister,
    reloadSettings,
    reloadRegisters,
    refreshReadiness,
  };
}
