/**
 * Admin payment method definitions — /api/admin/payment-method-definitions
 */
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { UseMutationOptions, UseQueryOptions, UseQueryResult, UseMutationResult } from '@tanstack/react-query';
import { customInstance } from '@/lib/axios';

const BASE = '/api/admin/payment-method-definitions';

export interface PaymentMethodDefinitionAdmin {
  id: string;
  code: string;
  name: string;
  isActive: boolean;
  isDefault: boolean;
  displayOrder: number;
  legacyPaymentMethodValue: number;
  fiscalCategory?: string | null;
  requiresTerminal: boolean;
  terminalType?: string | null;
  allowRefund: boolean;
  icon?: string | null;
  metadataJson?: string | null;
  createdAtUtc: string;
  updatedAtUtc?: string | null;
}

export interface CreatePaymentMethodDefinitionRequest {
  code: string;
  name: string;
  legacyPaymentMethodValue: number;
  fiscalCategory?: string | null;
  isActive: boolean;
  isDefault: boolean;
  displayOrder: number;
  requiresTerminal: boolean;
  terminalType?: string | null;
  allowRefund: boolean;
  icon?: string | null;
  metadataJson?: string | null;
}

export type UpdatePaymentMethodDefinitionRequest = CreatePaymentMethodDefinitionRequest;

type SecondParameter<T> = T extends (arg: any, arg2?: infer U) => any ? U : never;

function unwrapData<T>(res: any): T {
  if (res?.data !== undefined) return res.data as T;
  return res as T;
}

export function getAdminPaymentMethodDefinitions(
  options?: SecondParameter<typeof customInstance>,
  signal?: AbortSignal
): Promise<PaymentMethodDefinitionAdmin[]> {
  return customInstance<PaymentMethodDefinitionAdmin[]>({ url: BASE, method: 'GET', signal }, options).then((res) =>
    unwrapData<PaymentMethodDefinitionAdmin[]>(res)
  );
}

export function createAdminPaymentMethodDefinition(
  data: CreatePaymentMethodDefinitionRequest,
  options?: SecondParameter<typeof customInstance>
) {
  return customInstance<PaymentMethodDefinitionAdmin>(
    { url: BASE, method: 'POST', headers: { 'Content-Type': 'application/json' }, data },
    options
  ).then((res) => unwrapData<PaymentMethodDefinitionAdmin>(res));
}

export function updateAdminPaymentMethodDefinition(
  id: string,
  data: UpdatePaymentMethodDefinitionRequest,
  options?: SecondParameter<typeof customInstance>
) {
  return customInstance<PaymentMethodDefinitionAdmin>(
    { url: `${BASE}/${id}`, method: 'PUT', headers: { 'Content-Type': 'application/json' }, data },
    options
  ).then((res) => unwrapData<PaymentMethodDefinitionAdmin>(res));
}

export function deleteAdminPaymentMethodDefinition(id: string, options?: SecondParameter<typeof customInstance>) {
  return customInstance<{ id: string; message?: string }>({ url: `${BASE}/${id}`, method: 'DELETE' }, options).then((res) =>
    unwrapData(res)
  );
}

export const adminPaymentMethodDefinitionsQueryKeys = {
  all: ['admin', 'payment-method-definitions'] as const,
  lists: () => [...adminPaymentMethodDefinitionsQueryKeys.all, 'list'] as const,
};

export function useAdminPaymentMethodDefinitionsList(
  options?: Partial<UseQueryOptions<PaymentMethodDefinitionAdmin[], Error, PaymentMethodDefinitionAdmin[]>>
): UseQueryResult<PaymentMethodDefinitionAdmin[], Error> {
  return useQuery({
    queryKey: adminPaymentMethodDefinitionsQueryKeys.lists(),
    queryFn: ({ signal }) => getAdminPaymentMethodDefinitions(undefined, signal),
    ...options,
  });
}

export function useCreateAdminPaymentMethodDefinition(
  options?: UseMutationOptions<PaymentMethodDefinitionAdmin, Error, CreatePaymentMethodDefinitionRequest>
): UseMutationResult<PaymentMethodDefinitionAdmin, Error, CreatePaymentMethodDefinitionRequest> {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data) => createAdminPaymentMethodDefinition(data),
    onSuccess: () => qc.invalidateQueries({ queryKey: adminPaymentMethodDefinitionsQueryKeys.lists() }),
    ...options,
  });
}

export function useUpdateAdminPaymentMethodDefinition(
  options?: UseMutationOptions<
    PaymentMethodDefinitionAdmin,
    Error,
    { id: string; data: UpdatePaymentMethodDefinitionRequest }
  >
): UseMutationResult<
  PaymentMethodDefinitionAdmin,
  Error,
  { id: string; data: UpdatePaymentMethodDefinitionRequest }
> {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }) => updateAdminPaymentMethodDefinition(id, data),
    onSuccess: () => qc.invalidateQueries({ queryKey: adminPaymentMethodDefinitionsQueryKeys.lists() }),
    ...options,
  });
}

export function useDeleteAdminPaymentMethodDefinition(
  options?: UseMutationOptions<void, Error, string>
): UseMutationResult<void, Error, string> {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id) => deleteAdminPaymentMethodDefinition(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: adminPaymentMethodDefinitionsQueryKeys.lists() }),
    ...options,
  });
}
