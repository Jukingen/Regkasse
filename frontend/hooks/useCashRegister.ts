import { useEffect, useMemo, useState } from 'react';

import { usePosStatusOverview } from '../contexts/PosStatusOverviewContext';
import {
  fetchPosSelectableRegisters,
  type CashRegisterSelectableRow,
} from '../services/api/cashRegisterService';
import { getPosCashRegisterCurrent } from '../services/api/posCashRegisterReadinessService';
import { isValidPosCashRegisterId } from '../utils/posCashRegister';

/** Display model for the cashier's active/assigned cash register (UserMenu, etc.). */
export type CurrentCashRegister = {
  id: string;
  /** Human-readable label: registerNumber, optionally with location. */
  name: string;
  registerNumber: string;
  location?: string;
};

export type UseCashRegisterOptions = {
  /** When false, skip network refresh (e.g. menu closed). Default true. */
  enabled?: boolean;
};

export type UseCashRegisterResult = {
  /** Assigned/effective register, or null when none. */
  register: CurrentCashRegister | null;
  isLoading: boolean;
};

function formatRegisterName(registerNumber: string, location?: string): string {
  const num = registerNumber.trim() || '—';
  const loc = location?.trim();
  return loc ? `${num} – ${loc}` : num;
}

function resolveRegisterId(
  preferred?: string | null,
  effective?: string | null,
  settingsId?: string | null
): string | null {
  for (const id of [settingsId, preferred, effective]) {
    if (isValidPosCashRegisterId(id)) return id!.trim();
  }
  return null;
}

function toCurrentRegister(
  id: string,
  row: CashRegisterSelectableRow | undefined
): CurrentCashRegister {
  if (row) {
    return {
      id,
      registerNumber: row.registerNumber,
      location: row.location,
      name: formatRegisterName(row.registerNumber, row.location),
    };
  }
  const short = id.length > 8 ? `${id.slice(0, 8)}…` : id;
  return {
    id,
    registerNumber: short,
    name: short,
  };
}

/**
 * Current POS cash register for UI (name + id).
 * Uses GET /api/pos/cash-register/current and enriches name via selectable list.
 * (POS has no TanStack Query; stale cache is in-memory for the hook lifetime.)
 */
export function useCashRegister(options: UseCashRegisterOptions = {}): UseCashRegisterResult {
  const { enabled = true } = options;
  const { settingsCashRegisterId, cashRegister: overviewRegister } = usePosStatusOverview();

  const overviewId = useMemo(
    () =>
      resolveRegisterId(
        overviewRegister?.preferredRegisterId,
        overviewRegister?.effectiveRegisterId,
        settingsCashRegisterId
      ),
    [
      overviewRegister?.preferredRegisterId,
      overviewRegister?.effectiveRegisterId,
      settingsCashRegisterId,
    ]
  );

  const [register, setRegister] = useState<CurrentCashRegister | null>(() =>
    overviewId ? toCurrentRegister(overviewId, undefined) : null
  );
  const [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    if (!overviewId) {
      setRegister(null);
      return;
    }
    setRegister((prev) =>
      prev?.id === overviewId ? prev : toCurrentRegister(overviewId, undefined)
    );
  }, [overviewId]);

  useEffect(() => {
    if (!enabled) {
      setIsLoading(false);
      return;
    }

    let cancelled = false;
    setIsLoading(true);

    void (async () => {
      try {
        const [current, selectable] = await Promise.all([
          getPosCashRegisterCurrent().catch(() => null),
          fetchPosSelectableRegisters().catch(() => null),
        ]);

        if (cancelled) return;

        const id = resolveRegisterId(
          current?.preferredRegisterId,
          current?.effectiveRegisterId,
          settingsCashRegisterId ?? overviewId
        );

        if (!id) {
          setRegister(null);
          return;
        }

        const row = selectable?.registers.find((r) => r.id === id);
        setRegister(toCurrentRegister(id, row));
      } finally {
        if (!cancelled) setIsLoading(false);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [enabled, overviewId, settingsCashRegisterId]);

  return {
    register,
    isLoading,
  };
}
