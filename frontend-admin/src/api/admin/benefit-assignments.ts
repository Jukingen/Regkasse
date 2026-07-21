/**
 * Admin benefit assignments API – /api/admin/benefit-assignments.
 */
import type {
  UseMutationOptions,
  UseMutationResult,
  UseQueryOptions,
  UseQueryResult,
} from '@tanstack/react-query';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { customInstance } from '@/lib/axios';

import type { BenefitDefinition } from './benefit-definitions';

const BASE = '/api/admin/benefit-assignments';

export interface CustomerRef {
  id: string;
  name?: string;
  customerNumber?: string;
}

export interface BenefitAssignment {
  id: string;
  benefitDefinitionId: string;
  customerId: string;
  validFrom: string;
  validTo?: string | null;
  priority: number;
  isActive: boolean;
  createdAt?: string;
  updatedAt?: string | null;
  benefitDefinition?: BenefitDefinition | null;
  customer?: CustomerRef | null;
}

export interface CreateBenefitAssignmentRequest {
  benefitDefinitionId: string;
  customerId: string;
  validFrom: string;
  validTo?: string | null;
  priority: number;
  isActive: boolean;
}

export interface UpdateBenefitAssignmentRequest extends CreateBenefitAssignmentRequest {}

type SecondParameter<T> = T extends (arg: any, arg2?: infer U) => any ? U : never;

function unwrapData<T>(res: any): T {
  if (res?.data !== undefined) return res.data as T;
  return res as T;
}

export function getAdminBenefitAssignments(
  options?: SecondParameter<typeof customInstance>,
  signal?: AbortSignal
): Promise<BenefitAssignment[]> {
  return customInstance<BenefitAssignment[]>({ url: BASE, method: 'GET', signal }, options).then(
    (res) => unwrapData<BenefitAssignment[]>(res)
  );
}

export function getAdminBenefitAssignmentById(
  id: string,
  options?: SecondParameter<typeof customInstance>,
  signal?: AbortSignal
): Promise<BenefitAssignment> {
  return customInstance<BenefitAssignment>(
    { url: `${BASE}/${id}`, method: 'GET', signal },
    options
  ).then((res) => unwrapData<BenefitAssignment>(res));
}

export function createAdminBenefitAssignment(
  data: CreateBenefitAssignmentRequest,
  options?: SecondParameter<typeof customInstance>
) {
  return customInstance<BenefitAssignment>(
    { url: BASE, method: 'POST', headers: { 'Content-Type': 'application/json' }, data },
    options
  ).then((res) => unwrapData<BenefitAssignment>(res));
}

export function updateAdminBenefitAssignment(
  id: string,
  data: UpdateBenefitAssignmentRequest,
  options?: SecondParameter<typeof customInstance>
) {
  return customInstance<BenefitAssignment>(
    { url: `${BASE}/${id}`, method: 'PUT', headers: { 'Content-Type': 'application/json' }, data },
    options
  ).then((res) => unwrapData<BenefitAssignment>(res));
}

export function deleteAdminBenefitAssignment(
  id: string,
  options?: SecondParameter<typeof customInstance>
) {
  return customInstance<void>({ url: `${BASE}/${id}`, method: 'DELETE' }, options);
}

export const adminBenefitAssignmentsQueryKeys = {
  all: ['admin', 'benefit-assignments'] as const,
  lists: () => [...adminBenefitAssignmentsQueryKeys.all, 'list'] as const,
  details: () => [...adminBenefitAssignmentsQueryKeys.all, 'detail'] as const,
  detail: (id: string) => [...adminBenefitAssignmentsQueryKeys.details(), id] as const,
};

export function useAdminBenefitAssignmentsList(
  options?: Partial<UseQueryOptions<BenefitAssignment[], Error, BenefitAssignment[]>>
): UseQueryResult<BenefitAssignment[], Error> {
  return useQuery({
    queryKey: adminBenefitAssignmentsQueryKeys.lists(),
    queryFn: ({ signal }) => getAdminBenefitAssignments(undefined, signal),
    ...options,
  });
}

export function useAdminBenefitAssignmentById(
  id: string,
  options?: Partial<UseQueryOptions<BenefitAssignment, Error, BenefitAssignment>>
): UseQueryResult<BenefitAssignment, Error> {
  return useQuery({
    queryKey: adminBenefitAssignmentsQueryKeys.detail(id),
    queryFn: ({ signal }) => getAdminBenefitAssignmentById(id, undefined, signal),
    enabled: !!id,
    ...options,
  });
}

export function useCreateAdminBenefitAssignment(
  opts?: UseMutationOptions<BenefitAssignment, Error, { data: CreateBenefitAssignmentRequest }>
): UseMutationResult<BenefitAssignment, Error, { data: CreateBenefitAssignmentRequest }> {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ data }) => createAdminBenefitAssignment(data),
    onSuccess: () => qc.invalidateQueries({ queryKey: adminBenefitAssignmentsQueryKeys.lists() }),
    ...opts,
  });
}

export function useUpdateAdminBenefitAssignment(
  opts?: UseMutationOptions<
    BenefitAssignment,
    Error,
    { id: string; data: UpdateBenefitAssignmentRequest }
  >
): UseMutationResult<
  BenefitAssignment,
  Error,
  { id: string; data: UpdateBenefitAssignmentRequest }
> {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }) => updateAdminBenefitAssignment(id, data),
    onSuccess: (_, { id }) => {
      qc.invalidateQueries({ queryKey: adminBenefitAssignmentsQueryKeys.detail(id) });
      qc.invalidateQueries({ queryKey: adminBenefitAssignmentsQueryKeys.lists() });
    },
    ...opts,
  });
}

export function useDeleteAdminBenefitAssignment(
  opts?: UseMutationOptions<void, Error, { id: string }>
): UseMutationResult<void, Error, { id: string }> {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id }) => deleteAdminBenefitAssignment(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: adminBenefitAssignmentsQueryKeys.lists() }),
    ...opts,
  });
}
