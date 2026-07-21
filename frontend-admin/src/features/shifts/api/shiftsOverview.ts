import type { QueryClient } from '@tanstack/react-query';

import { customInstance } from '@/lib/axios';

export type AdminShiftRow = {
  id: string;
  cashRegisterId: string;
  registerNumber?: string | null;
  cashierId: string;
  cashierName: string;
  startedAt: string;
  endedAt?: string | null;
  startBalance: number;
  endBalance: number;
  totalSales: number;
  totalCash: number;
  totalCard: number;
  difference: number;
  status: string;
  dailyClosingId?: string | null;
  cashCount?: number | null;
  notes?: string | null;
  isOrphanedRegisterSession?: boolean;
  openDurationHours?: number | null;
};

export type AdminDailyClosingOverviewRow = {
  dailyClosingId: string;
  shiftId?: string | null;
  cashRegisterId: string;
  registerNumber?: string | null;
  cashierName: string;
  closingDate: string;
  shiftEndedAt?: string | null;
  totalSales: number;
  totalCash: number;
  totalCard: number;
  cashCount?: number | null;
  difference: number;
  fiscalTotalAmount: number;
  fiscalTotalTaxAmount: number;
  fiscalTransactionCount: number;
  hasTseSignature: boolean;
  shiftStatus: string;
  fiscalStatus: string;
};

export type AdminShiftOverview = {
  activeShifts: AdminShiftRow[];
  shiftHistory: AdminShiftRow[];
  dailyClosings: AdminDailyClosingOverviewRow[];
};

export type AdminShiftOverviewParams = {
  cashRegisterId?: string;
  fromUtc?: string;
  toUtc?: string;
  limit?: number;
};

export const adminShiftOverviewQueryKeyPrefix = ['admin', 'shifts', 'overview'] as const;

export const adminShiftOverviewQueryKey = (params: AdminShiftOverviewParams = {}) =>
  [...adminShiftOverviewQueryKeyPrefix, params] as const;

export async function invalidateAdminShiftOverviewQueries(queryClient: QueryClient): Promise<void> {
  await queryClient.invalidateQueries({ queryKey: adminShiftOverviewQueryKeyPrefix });
}

export async function fetchAdminShiftOverview(
  params: AdminShiftOverviewParams = {}
): Promise<AdminShiftOverview> {
  return customInstance<AdminShiftOverview>({
    url: '/api/admin/shifts/overview',
    method: 'GET',
    params,
  });
}

export type AdminForceCloseShiftRequest = {
  closingBalance?: number;
  reason?: string;
};

export async function forceCloseAdminShiftRegister(
  cashRegisterId: string,
  body: AdminForceCloseShiftRequest = {}
): Promise<{ cashRegisterId: string; closedShiftCount: number }> {
  return customInstance({
    url: `/api/admin/shifts/registers/${cashRegisterId.trim()}/force-close`,
    method: 'POST',
    data: body,
  });
}
