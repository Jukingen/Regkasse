import type { QueryClient } from '@tanstack/react-query';

import { cashRegisterListQueryKey } from '@/features/cash-registers/api/cashRegisters';
import { shiftStatusQueryKey } from '@/features/shifts/api/shiftManagement';
import { invalidateAdminShiftOverviewQueries } from '@/features/shifts/api/shiftsOverview';

/** Invalidate shift status, overview, and cash-register queries after open/close mutations. */
export async function invalidateShiftRelatedQueries(
  queryClient: QueryClient,
  registerId?: string,
): Promise<void> {
  await Promise.all([
    queryClient.invalidateQueries({ queryKey: ['shift', 'status'] }),
    queryClient.invalidateQueries({ queryKey: shiftStatusQueryKey(registerId) }),
    queryClient.invalidateQueries({ queryKey: ['admin', 'cash-registers'] }),
    queryClient.invalidateQueries({ queryKey: cashRegisterListQueryKey }),
    queryClient.invalidateQueries({ queryKey: ['cash-registers'] }),
    queryClient.invalidateQueries({ queryKey: ['admin', 'cash-registers', 'list'] }),
    invalidateAdminShiftOverviewQueries(queryClient),
  ]);
}
