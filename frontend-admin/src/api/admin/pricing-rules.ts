/**
 * Admin pricing rules — /api/admin/pricing-rules
 */
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { UseMutationOptions, UseQueryOptions, UseQueryResult, UseMutationResult } from '@tanstack/react-query';
import { customInstance } from '@/lib/axios';

const BASE = '/api/admin/pricing-rules';

export interface PricingRuleAdmin {
  id: string;
  name: string;
  priority: number;
  isActive: boolean;
  validFromDate: string;
  validToDate: string;
  daysOfWeekMask: number;
  timeWindowEnabled: boolean;
  timeStartMinutes: number;
  timeEndMinutes: number;
  targetScope: number;
  targetId: string;
  actionType: number;
  actionValue: number;
  cashRegisterId?: string | null;
  createdAtUtc: string;
  updatedAtUtc?: string | null;
}

export interface CreatePricingRuleRequest {
  name: string;
  priority: number;
  isActive: boolean;
  validFromDate: string;
  validToDate: string;
  daysOfWeekMask: number;
  timeWindowEnabled: boolean;
  timeStartMinutes: number;
  timeEndMinutes: number;
  targetScope: number;
  targetId: string;
  actionType: number;
  actionValue: number;
  cashRegisterId?: string | null;
}

export type UpdatePricingRuleRequest = CreatePricingRuleRequest;

type SecondParameter<T> = T extends (arg: any, arg2?: infer U) => any ? U : never;

function unwrapData<T>(res: any): T {
  if (res?.data !== undefined) return res.data as T;
  return res as T;
}

export function getAdminPricingRules(
  options?: SecondParameter<typeof customInstance>,
  signal?: AbortSignal
): Promise<PricingRuleAdmin[]> {
  return customInstance<PricingRuleAdmin[]>({ url: BASE, method: 'GET', signal }, options).then((res) =>
    unwrapData<PricingRuleAdmin[]>(res)
  );
}

export function createAdminPricingRule(
  data: CreatePricingRuleRequest,
  options?: SecondParameter<typeof customInstance>
) {
  return customInstance<PricingRuleAdmin>(
    { url: BASE, method: 'POST', headers: { 'Content-Type': 'application/json' }, data },
    options
  ).then((res) => unwrapData<PricingRuleAdmin>(res));
}

export function updateAdminPricingRule(
  id: string,
  data: UpdatePricingRuleRequest,
  options?: SecondParameter<typeof customInstance>
) {
  return customInstance<PricingRuleAdmin>(
    { url: `${BASE}/${id}`, method: 'PUT', headers: { 'Content-Type': 'application/json' }, data },
    options
  ).then((res) => unwrapData<PricingRuleAdmin>(res));
}

export function deleteAdminPricingRule(id: string, options?: SecondParameter<typeof customInstance>) {
  return customInstance<{ id: string; message?: string }>({ url: `${BASE}/${id}`, method: 'DELETE' }, options).then((res) =>
    unwrapData<{ id: string; message?: string }>(res)
  );
}

export const adminPricingRulesQueryKeys = {
  all: ['admin', 'pricing-rules'] as const,
  lists: () => [...adminPricingRulesQueryKeys.all, 'list'] as const,
};

export function useAdminPricingRulesList(
  options?: Partial<UseQueryOptions<PricingRuleAdmin[], Error, PricingRuleAdmin[]>>
): UseQueryResult<PricingRuleAdmin[], Error> {
  return useQuery({
    queryKey: adminPricingRulesQueryKeys.lists(),
    queryFn: ({ signal }) => getAdminPricingRules(undefined, signal),
    ...options,
  });
}

export function useCreateAdminPricingRule(
  options?: UseMutationOptions<PricingRuleAdmin, Error, CreatePricingRuleRequest>
): UseMutationResult<PricingRuleAdmin, Error, CreatePricingRuleRequest> {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data) => createAdminPricingRule(data),
    onSuccess: () => qc.invalidateQueries({ queryKey: adminPricingRulesQueryKeys.lists() }),
    ...options,
  });
}

export function useUpdateAdminPricingRule(
  options?: UseMutationOptions<PricingRuleAdmin, Error, { id: string; data: UpdatePricingRuleRequest }>
): UseMutationResult<PricingRuleAdmin, Error, { id: string; data: UpdatePricingRuleRequest }> {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }) => updateAdminPricingRule(id, data),
    onSuccess: () => qc.invalidateQueries({ queryKey: adminPricingRulesQueryKeys.lists() }),
    ...options,
  });
}

export function useDeleteAdminPricingRule(
  options?: UseMutationOptions<{ id: string; message?: string }, Error, string>
): UseMutationResult<{ id: string; message?: string }, Error, string> {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id) => deleteAdminPricingRule(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: adminPricingRulesQueryKeys.lists() }),
    ...options,
  });
}
