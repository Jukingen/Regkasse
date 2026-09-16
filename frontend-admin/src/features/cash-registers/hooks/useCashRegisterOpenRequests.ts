'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { cashRegisterListQueryKey } from '@/features/cash-registers/api/cashRegisters';
import {
  approveCashRegisterOpenRequest,
  denyCashRegisterOpenRequest,
  fetchCashRegisterOpenRequests,
} from '@/features/cash-registers/api/openRequests';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import { AppPermissions, PERMISSIONS } from '@/shared/auth/permissions';

export const CASH_REGISTER_OPEN_REQUESTS_QUERY_KEY = ['admin', 'cash-registers', 'open-requests'] as const;

export type CashRegisterOpenRequestListFilter = 'Pending' | 'Approved' | 'Denied' | 'all';

export function useCashRegisterOpenRequests(
  status: CashRegisterOpenRequestListFilter = 'Pending',
  tenantId?: string
) {
  const { isAuthorized } = useAuthorizationGate({
    requiredPermission: [AppPermissions.CashRegisterManage, PERMISSIONS.SYSTEM_CRITICAL],
  });
  const statusParam = status === 'all' ? null : status;
  return useQuery({
    queryKey: [...CASH_REGISTER_OPEN_REQUESTS_QUERY_KEY, status, tenantId ?? '__jwt__'],
    queryFn: () => fetchCashRegisterOpenRequests(statusParam, tenantId),
    enabled: isAuthorized,
    refetchInterval: status === 'Pending' ? 15_000 : false,
  });
}

function invalidateAfterResolve(queryClient: ReturnType<typeof useQueryClient>) {
  void queryClient.invalidateQueries({ queryKey: CASH_REGISTER_OPEN_REQUESTS_QUERY_KEY });
  void queryClient.invalidateQueries({ queryKey: cashRegisterListQueryKey });
  void queryClient.invalidateQueries({ queryKey: ['admin', 'cash-registers', 'list'] });
}

export function useApproveCashRegisterOpenRequest() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (args: { id: string; note?: string }) =>
      approveCashRegisterOpenRequest(args.id, args.note),
    onSuccess: () => invalidateAfterResolve(queryClient),
  });
}

export function useDenyCashRegisterOpenRequest() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (args: { id: string; note?: string }) =>
      denyCashRegisterOpenRequest(args.id, args.note),
    onSuccess: () => invalidateAfterResolve(queryClient),
  });
}
