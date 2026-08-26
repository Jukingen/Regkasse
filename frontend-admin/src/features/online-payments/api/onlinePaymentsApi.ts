'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  getApiAdminOnlinePayments,
  getApiAdminOnlinePaymentsId,
  getGetApiAdminOnlinePaymentsQueryKey,
  postApiAdminOnlinePaymentsTest,
} from '@/api/generated/admin/admin';
import type { AdminOnlinePaymentDto as GeneratedAdminOnlinePaymentDto } from '@/api/generated/model/adminOnlinePaymentDto';
import type { AdminOnlinePaymentListResponse as GeneratedAdminOnlinePaymentListResponse } from '@/api/generated/model/adminOnlinePaymentListResponse';
import type { AdminOnlinePaymentTestResponse as GeneratedAdminOnlinePaymentTestResponse } from '@/api/generated/model/adminOnlinePaymentTestResponse';

export type OnlinePaymentStatus = 'Created' | 'Pending' | 'Succeeded' | 'Failed' | string;

export type AdminOnlinePaymentDto = {
  id: string;
  tenantId: string;
  tenantName: string;
  tenantSlug: string;
  amount: number;
  currency: string;
  status: OnlinePaymentStatus;
  paymentMethod: string;
  provider: string;
  paymentIntentId?: string | null;
  onlineOrderId?: string | null;
  orderNumber?: string | null;
  isSynthetic: boolean;
  errorMessage?: string | null;
  lastWebhookEvent?: string | null;
  createdAtUtc: string;
  completedAtUtc?: string | null;
};

export type AdminOnlinePaymentListResponse = {
  items: AdminOnlinePaymentDto[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
};

export type AdminOnlinePaymentTestAction = 'create' | 'webhookSucceeded' | 'webhookFailed' | 'expire';

export type AdminOnlinePaymentTestRequest = {
  action: AdminOnlinePaymentTestAction;
  amount?: number;
  paymentMethod?: string;
  transactionId?: string;
};

export type AdminOnlinePaymentTestResponse = {
  succeeded: boolean;
  code?: string | null;
  error?: string | null;
  transaction?: AdminOnlinePaymentDto | null;
};

function normalizePayment(row: GeneratedAdminOnlinePaymentDto): AdminOnlinePaymentDto {
  return {
    id: row.id ?? '',
    tenantId: row.tenantId ?? '',
    tenantName: row.tenantName ?? '',
    tenantSlug: row.tenantSlug ?? '',
    amount: row.amount ?? 0,
    currency: row.currency ?? 'EUR',
    status: row.status ?? '',
    paymentMethod: row.paymentMethod ?? '',
    provider: row.provider ?? '',
    paymentIntentId: row.paymentIntentId,
    onlineOrderId: row.onlineOrderId,
    orderNumber: row.orderNumber,
    isSynthetic: row.isSynthetic ?? false,
    errorMessage: row.errorMessage,
    lastWebhookEvent: row.lastWebhookEvent,
    createdAtUtc: row.createdAtUtc ?? '',
    completedAtUtc: row.completedAtUtc,
  };
}

function normalizeList(
  data: GeneratedAdminOnlinePaymentListResponse,
  pageNumber: number,
  pageSize: number
): AdminOnlinePaymentListResponse {
  return {
    items: (data.items ?? []).map(normalizePayment),
    totalCount: data.totalCount ?? 0,
    pageNumber: data.pageNumber ?? pageNumber,
    pageSize: data.pageSize ?? pageSize,
  };
}

function normalizeTestResponse(
  data: GeneratedAdminOnlinePaymentTestResponse
): AdminOnlinePaymentTestResponse {
  return {
    succeeded: data.succeeded ?? false,
    code: data.code,
    error: data.error,
    transaction: data.transaction ? normalizePayment(data.transaction) : null,
  };
}

export function onlinePaymentsListQueryKey(pageNumber: number, pageSize: number) {
  return getGetApiAdminOnlinePaymentsQueryKey({ pageNumber, pageSize });
}

export async function fetchOnlinePayments(
  pageNumber: number,
  pageSize: number,
  signal?: AbortSignal
): Promise<AdminOnlinePaymentListResponse> {
  const data = await getApiAdminOnlinePayments({ pageNumber, pageSize }, undefined, signal);
  return normalizeList(data, pageNumber, pageSize);
}

export async function fetchOnlinePaymentById(
  id: string,
  signal?: AbortSignal
): Promise<AdminOnlinePaymentDto> {
  const data = await getApiAdminOnlinePaymentsId(id, undefined, signal);
  return normalizePayment(data);
}

export async function postOnlinePaymentTest(
  body: AdminOnlinePaymentTestRequest
): Promise<AdminOnlinePaymentTestResponse> {
  const data = await postApiAdminOnlinePaymentsTest({
    action: body.action,
    amount: body.amount,
    paymentMethod: body.paymentMethod,
    transactionId: body.transactionId,
  });
  return normalizeTestResponse(data);
}

export function useOnlinePaymentsList(pageNumber: number, pageSize: number, enabled: boolean) {
  return useQuery({
    queryKey: onlinePaymentsListQueryKey(pageNumber, pageSize),
    queryFn: ({ signal }) => fetchOnlinePayments(pageNumber, pageSize, signal),
    enabled,
  });
}

export function useOnlinePaymentTestMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: postOnlinePaymentTest,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['/api/admin/online-payments'] });
    },
  });
}
