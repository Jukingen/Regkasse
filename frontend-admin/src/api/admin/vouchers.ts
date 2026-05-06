/**
 * Admin vouchers API — /api/admin/vouchers (manual client; not generated).
 */
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { UseMutationOptions, UseQueryOptions, UseQueryResult } from '@tanstack/react-query';
import { customInstance } from '@/lib/axios';

const BASE = '/api/admin/vouchers';

export interface AdminVoucherListItemDto {
  id: string;
  maskedCode: string;
  initialAmount: number;
  remainingAmount: number;
  currency: string;
  status: string;
  validFromUtc: string;
  expiresAtUtc: string;
  createdByUserId: string;
  createdByDisplayName?: string | null;
  createdByEmail?: string | null;
  createdByRoles?: string[] | null;
  createdAtUtc: string;
}

export interface AdminVoucherListResponse {
  items: AdminVoucherListItemDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface AdminVoucherDetailDto {
  id: string;
  maskedCode: string;
  initialAmount: number;
  remainingAmount: number;
  currency: string;
  status: string;
  validFromUtc: string;
  expiresAtUtc: string;
  createdByUserId: string;
  createdByDisplayName?: string | null;
  createdByEmail?: string | null;
  createdByRoles?: string[] | null;
  createdAtUtc: string;
  cancelledAtUtc?: string | null;
  cancellationReason?: string | null;
  internalNote?: string | null;
}

export interface AdminVoucherLedgerLineDto {
  id: string;
  type: string;
  amount: number;
  balanceAfter: number;
  paymentId?: string | null;
  receiptId?: string | null;
  receiptNumber?: string | null;
  createdByUserId: string;
  createdByDisplayName?: string | null;
  createdByEmail?: string | null;
  createdByRoles?: string[] | null;
  createdAtUtc: string;
  correlationId?: string | null;
}

export interface CreateAdminVoucherRequest {
  initialAmount: number;
  currency: string;
  expiryMode: 'DefaultOneYear' | 'Custom';
  expiresAtUtc?: string | null;
  note?: string | null;
}

export interface CreateAdminVoucherResponse {
  id: string;
  plaintextCode: string;
  maskedCode: string;
  initialAmount: number;
  currency: string;
  validFromUtc: string;
  expiresAtUtc: string;
}

export interface VerifyAdminVoucherCodeResponse {
  matches: boolean;
}

type SecondParameter<T> = T extends (arg: any, arg2?: infer U) => any ? U : never;

function unwrapData<T>(res: unknown): T {
  const r = res as { data?: T };
  if (r?.data !== undefined) return r.data;
  return res as T;
}

export function getAdminVouchers(
  params: { page?: number; pageSize?: number; q?: string },
  options?: SecondParameter<typeof customInstance>,
  signal?: AbortSignal
): Promise<AdminVoucherListResponse> {
  return customInstance<AdminVoucherListResponse>(
    {
      url: BASE,
      method: 'GET',
      params: {
        page: params.page ?? 1,
        pageSize: params.pageSize ?? 20,
        q: params.q?.trim() || undefined,
      },
      signal,
    },
    options
  ).then((res) => unwrapData<AdminVoucherListResponse>(res));
}

export function getAdminVoucherById(
  id: string,
  options?: SecondParameter<typeof customInstance>,
  signal?: AbortSignal
): Promise<AdminVoucherDetailDto> {
  return customInstance<AdminVoucherDetailDto>({ url: `${BASE}/${id}`, method: 'GET', signal }, options).then((res) =>
    unwrapData<AdminVoucherDetailDto>(res)
  );
}

export function getAdminVoucherLedger(
  id: string,
  options?: SecondParameter<typeof customInstance>,
  signal?: AbortSignal
): Promise<AdminVoucherLedgerLineDto[]> {
  return customInstance<AdminVoucherLedgerLineDto[]>(
    { url: `${BASE}/${id}/ledger`, method: 'GET', signal },
    options
  ).then((res) => unwrapData<AdminVoucherLedgerLineDto[]>(res));
}

export function createAdminVoucher(
  data: CreateAdminVoucherRequest,
  options?: SecondParameter<typeof customInstance>
): Promise<CreateAdminVoucherResponse> {
  return customInstance<CreateAdminVoucherResponse>(
    { url: BASE, method: 'POST', headers: { 'Content-Type': 'application/json' }, data },
    options
  ).then((res) => unwrapData<CreateAdminVoucherResponse>(res));
}

export function cancelAdminVoucher(
  id: string,
  reason: string,
  options?: SecondParameter<typeof customInstance>
): Promise<void> {
  return customInstance<void>(
    {
      url: `${BASE}/${id}/cancel`,
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      data: { reason },
    },
    options
  ).then(() => undefined);
}

export function verifyAdminVoucherCode(
  id: string,
  code: string,
  options?: SecondParameter<typeof customInstance>
): Promise<VerifyAdminVoucherCodeResponse> {
  return customInstance<VerifyAdminVoucherCodeResponse>(
    {
      url: `${BASE}/${id}/verify-code`,
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      data: { code },
    },
    options
  ).then((res) => unwrapData<VerifyAdminVoucherCodeResponse>(res));
}

export const adminVouchersQueryKeys = {
  all: ['admin', 'vouchers'] as const,
  lists: () => [...adminVouchersQueryKeys.all, 'list'] as const,
  list: (page: number, pageSize: number, q: string) => [...adminVouchersQueryKeys.lists(), page, pageSize, q] as const,
  details: () => [...adminVouchersQueryKeys.all, 'detail'] as const,
  detail: (id: string) => [...adminVouchersQueryKeys.details(), id] as const,
  ledger: (id: string) => [...adminVouchersQueryKeys.all, 'ledger', id] as const,
};

export function useAdminVouchersList(
  params: { page: number; pageSize: number; q: string },
  options?: Partial<UseQueryOptions<AdminVoucherListResponse, Error>>
): UseQueryResult<AdminVoucherListResponse, Error> {
  return useQuery({
    queryKey: adminVouchersQueryKeys.list(params.page, params.pageSize, params.q),
    queryFn: ({ signal }) => getAdminVouchers(params, undefined, signal),
    ...options,
  });
}

export function useAdminVoucherDetail(
  id: string | undefined,
  options?: Partial<UseQueryOptions<AdminVoucherDetailDto, Error>>
): UseQueryResult<AdminVoucherDetailDto, Error> {
  return useQuery({
    queryKey: adminVouchersQueryKeys.detail(id ?? ''),
    queryFn: ({ signal }) => getAdminVoucherById(id!, undefined, signal),
    enabled: !!id,
    ...options,
  });
}

export function useAdminVoucherLedger(
  id: string | undefined,
  enabled: boolean,
  options?: Partial<UseQueryOptions<AdminVoucherLedgerLineDto[], Error>>
): UseQueryResult<AdminVoucherLedgerLineDto[], Error> {
  return useQuery({
    queryKey: adminVouchersQueryKeys.ledger(id ?? ''),
    queryFn: ({ signal }) => getAdminVoucherLedger(id!, undefined, signal),
    enabled: !!id && enabled,
    ...options,
  });
}

export function useCreateAdminVoucher(
  options?: UseMutationOptions<CreateAdminVoucherResponse, Error, CreateAdminVoucherRequest>
) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateAdminVoucherRequest) => createAdminVoucher(data),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: adminVouchersQueryKeys.lists() });
    },
    ...options,
  });
}

export function useCancelAdminVoucher(
  options?: UseMutationOptions<void, Error, { id: string; reason: string }>
) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, reason }: { id: string; reason: string }) => cancelAdminVoucher(id, reason),
    onSuccess: (_data, vars) => {
      void qc.invalidateQueries({ queryKey: adminVouchersQueryKeys.lists() });
      void qc.invalidateQueries({ queryKey: adminVouchersQueryKeys.detail(vars.id) });
      void qc.invalidateQueries({ queryKey: adminVouchersQueryKeys.ledger(vars.id) });
    },
    ...options,
  });
}

export function useVerifyAdminVoucherCode(
  options?: UseMutationOptions<VerifyAdminVoucherCodeResponse, Error, { id: string; code: string }>
) {
  return useMutation({
    mutationFn: ({ id, code }: { id: string; code: string }) => verifyAdminVoucherCode(id, code.trim()),
    ...options,
  });
}
