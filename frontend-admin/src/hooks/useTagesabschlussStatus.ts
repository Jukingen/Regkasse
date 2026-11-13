'use client';

import { useMemo } from 'react';

import { useGetApiTagesabschlussCanCloseCashRegisterId } from '@/api/generated/tagesabschluss/tagesabschluss';
import type { AdminCashRegisterListItem } from '@/features/cash-registers/api/cashRegisters';
import { DASHBOARD_AUTO_REFRESH_MS } from '@/features/dashboard/types';
import { useTodaySales } from '@/features/reports/hooks/useTodaySales';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import { useCashRegisterSelection } from '@/hooks/useCashRegisterSelection';
import { PERMISSIONS } from '@/shared/auth/permissions';

export type TagesabschlussReminderRegister = {
  id: string;
  /** Operator-facing label (register number + location). */
  name: string;
};

export type UseTagesabschlussStatusOptions = {
  enabled?: boolean;
  /** When set (e.g. from ManagerDashboard), reuse parent selection — avoid a second list query. */
  cashRegisterId?: string | null;
  register?: AdminCashRegisterListItem | null;
};

export type TagesabschlussStatus = {
  isClosingRequired: boolean;
  register: TagesabschlussReminderRegister | null;
  transactionCount: number;
  canClose: boolean;
  isLoading: boolean;
  isError: boolean;
};

function formatRegisterName(register: AdminCashRegisterListItem): string {
  const number = register.registerNumber?.trim();
  const location = register.location?.trim();
  if (number && location) return `${number} — ${location}`;
  return number || location || register.id;
}

/**
 * Pure rule: remind when today's Vienna business day is still open for closing
 * and there are fiscal transactions waiting (no automatic closing).
 */
export function computeIsClosingRequired(options: {
  canClose: boolean;
  transactionCount: number;
}): boolean {
  return options.canClose && options.transactionCount > 0;
}

/**
 * Selected-register Tagesabschluss reminder status for the FA dashboard.
 * Uses GET /api/Tagesabschluss/can-close/{id} + today's operational sales count.
 */
export function useTagesabschlussStatus(
  options: UseTagesabschlussStatusOptions = {}
): TagesabschlussStatus {
  const {
    enabled = true,
    cashRegisterId: externalRegisterId,
    register: externalRegister,
  } = options;

  const { isAuthorized: canViewClosing } = useAuthorizationGate({
    requiredPermission: PERMISSIONS.DAILY_CLOSING_VIEW,
  });

  const useExternalSelection = Boolean(externalRegisterId?.trim());

  const selection = useCashRegisterSelection({
    autoSelect: true,
    persistSelection: true,
    enabled: enabled && canViewClosing && !useExternalSelection,
  });

  const selectedRegister = externalRegister ?? selection.selectedRegister;
  const registerId = externalRegisterId?.trim() || selection.selectedRegisterId?.trim() || '';

  const queryEnabled = enabled && canViewClosing && registerId.length > 0;

  const canCloseQuery = useGetApiTagesabschlussCanCloseCashRegisterId(registerId, undefined, {
    query: {
      enabled: queryEnabled,
      staleTime: DASHBOARD_AUTO_REFRESH_MS / 2,
      refetchInterval: DASHBOARD_AUTO_REFRESH_MS,
      refetchIntervalInBackground: false,
      refetchOnWindowFocus: true,
    },
  });

  const sales = useTodaySales(queryEnabled ? registerId : undefined);

  const canClose = canCloseQuery.data?.canClose === true;
  const transactionCount = sales.data?.count ?? 0;

  const register = useMemo((): TagesabschlussReminderRegister | null => {
    if (!selectedRegister?.id) return null;
    return {
      id: selectedRegister.id,
      name: formatRegisterName(selectedRegister),
    };
  }, [selectedRegister]);

  const isClosingRequired = computeIsClosingRequired({ canClose, transactionCount });

  return {
    isClosingRequired: queryEnabled ? isClosingRequired : false,
    register,
    transactionCount,
    canClose,
    isLoading: queryEnabled && (canCloseQuery.isLoading || sales.isLoading),
    isError: queryEnabled && (canCloseQuery.isError || sales.isError),
  };
}
