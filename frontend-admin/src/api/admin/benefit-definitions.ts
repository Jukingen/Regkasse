/**
 * Admin benefit definitions API – /api/admin/benefit-definitions.
 */
import type {
  UseMutationOptions,
  UseMutationResult,
  UseQueryOptions,
  UseQueryResult,
} from '@tanstack/react-query';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { customInstance } from '@/lib/axios';

const BASE = '/api/admin/benefit-definitions';

export enum AppliedBenefitKind {
  PercentageDiscount = 0,
  FreeAllowance = 1,
  BuyXGetY = 2,
}

export interface BenefitDefinition {
  id: string;
  code: string;
  name: string;
  benefitKind: AppliedBenefitKind;
  percentageValue?: number | null;
  allowanceQuantity?: number | null;
  allowanceScope?: string | null;
  buyXQuantity?: number | null;
  getYQuantity?: number | null;
  allowanceCategoryId?: string | null;
  isActive: boolean;
  createdAt?: string;
  updatedAt?: string | null;
}

export interface CreateBenefitDefinitionRequest {
  code: string;
  name: string;
  benefitKind: AppliedBenefitKind;
  percentageValue?: number | null;
  allowanceQuantity?: number | null;
  allowanceScope?: string | null;
  allowanceCategoryId?: string | null;
  buyXQuantity?: number | null;
  getYQuantity?: number | null;
  isActive: boolean;
}

export interface UpdateBenefitDefinitionRequest extends CreateBenefitDefinitionRequest {}

type SecondParameter<T> = T extends (arg: any, arg2?: infer U) => any ? U : never;

function unwrapData<T>(res: any): T {
  if (res?.data !== undefined) return res.data as T;
  return res as T;
}

export function getAdminBenefitDefinitions(
  options?: SecondParameter<typeof customInstance>,
  signal?: AbortSignal
): Promise<BenefitDefinition[]> {
  return customInstance<BenefitDefinition[]>({ url: BASE, method: 'GET', signal }, options).then(
    (res) => unwrapData<BenefitDefinition[]>(res)
  );
}

export function getAdminBenefitDefinitionById(
  id: string,
  options?: SecondParameter<typeof customInstance>,
  signal?: AbortSignal
): Promise<BenefitDefinition> {
  return customInstance<BenefitDefinition>(
    { url: `${BASE}/${id}`, method: 'GET', signal },
    options
  ).then((res) => unwrapData<BenefitDefinition>(res));
}

export function createAdminBenefitDefinition(
  data: CreateBenefitDefinitionRequest,
  options?: SecondParameter<typeof customInstance>
) {
  return customInstance<BenefitDefinition>(
    { url: BASE, method: 'POST', headers: { 'Content-Type': 'application/json' }, data },
    options
  ).then((res) => unwrapData<BenefitDefinition>(res));
}

export function updateAdminBenefitDefinition(
  id: string,
  data: UpdateBenefitDefinitionRequest,
  options?: SecondParameter<typeof customInstance>
) {
  return customInstance<BenefitDefinition>(
    { url: `${BASE}/${id}`, method: 'PUT', headers: { 'Content-Type': 'application/json' }, data },
    options
  ).then((res) => unwrapData<BenefitDefinition>(res));
}

export function deleteAdminBenefitDefinition(
  id: string,
  options?: SecondParameter<typeof customInstance>
) {
  return customInstance<void>({ url: `${BASE}/${id}`, method: 'DELETE' }, options);
}

export const adminBenefitDefinitionsQueryKeys = {
  all: ['admin', 'benefit-definitions'] as const,
  lists: () => [...adminBenefitDefinitionsQueryKeys.all, 'list'] as const,
  details: () => [...adminBenefitDefinitionsQueryKeys.all, 'detail'] as const,
  detail: (id: string) => [...adminBenefitDefinitionsQueryKeys.details(), id] as const,
};

export function useAdminBenefitDefinitionsList(
  options?: Partial<UseQueryOptions<BenefitDefinition[], Error, BenefitDefinition[]>>
): UseQueryResult<BenefitDefinition[], Error> {
  return useQuery({
    queryKey: adminBenefitDefinitionsQueryKeys.lists(),
    queryFn: ({ signal }) => getAdminBenefitDefinitions(undefined, signal),
    ...options,
  });
}

export function useAdminBenefitDefinitionById(
  id: string,
  options?: Partial<UseQueryOptions<BenefitDefinition, Error, BenefitDefinition>>
): UseQueryResult<BenefitDefinition, Error> {
  return useQuery({
    queryKey: adminBenefitDefinitionsQueryKeys.detail(id),
    queryFn: ({ signal }) => getAdminBenefitDefinitionById(id, undefined, signal),
    enabled: !!id,
    ...options,
  });
}

export function useCreateAdminBenefitDefinition(
  opts?: UseMutationOptions<BenefitDefinition, Error, { data: CreateBenefitDefinitionRequest }>
): UseMutationResult<BenefitDefinition, Error, { data: CreateBenefitDefinitionRequest }> {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ data }) => createAdminBenefitDefinition(data),
    onSuccess: () => qc.invalidateQueries({ queryKey: adminBenefitDefinitionsQueryKeys.lists() }),
    ...opts,
  });
}

export function useUpdateAdminBenefitDefinition(
  opts?: UseMutationOptions<
    BenefitDefinition,
    Error,
    { id: string; data: UpdateBenefitDefinitionRequest }
  >
): UseMutationResult<
  BenefitDefinition,
  Error,
  { id: string; data: UpdateBenefitDefinitionRequest }
> {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }) => updateAdminBenefitDefinition(id, data),
    onSuccess: (_, { id }) => {
      qc.invalidateQueries({ queryKey: adminBenefitDefinitionsQueryKeys.detail(id) });
      qc.invalidateQueries({ queryKey: adminBenefitDefinitionsQueryKeys.lists() });
    },
    ...opts,
  });
}

export function useDeleteAdminBenefitDefinition(
  opts?: UseMutationOptions<void, Error, { id: string }>
): UseMutationResult<void, Error, { id: string }> {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id }) => deleteAdminBenefitDefinition(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: adminBenefitDefinitionsQueryKeys.lists() }),
    ...opts,
  });
}
