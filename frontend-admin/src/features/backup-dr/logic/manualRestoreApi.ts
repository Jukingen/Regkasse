/**
 * Manual restore approval API (Super Admin) — manual OpenAPI integration until Orval regen.
 */

import { customInstance } from '@/lib/axios';

export const MANUAL_RESTORE_REQUEST_PATH = '/api/admin/restore/request' as const;
export const MANUAL_RESTORE_HISTORY_PATH = '/api/admin/restore/history' as const;

export function getManualRestoreRequestQueryKey(requestId: string) {
  return [`/api/admin/restore/request`, requestId] as const;
}

export interface RestoreRequestBody {
  backupRunId: string;
  targetDatabaseName: string;
  reason?: string;
  validationOnly: boolean;
}

export interface RestoreApprovalBody {
  approvalToken: string;
  action: 'approve' | 'reject';
  reason?: string;
}

export interface RestoreRequestStatusDto {
  requestId: string;
  status: string;
  requestedAt: string;
  requestedByUserId?: string | null;
  requestedByEmail?: string | null;
  approvedByUserId?: string | null;
  approvedAt?: string | null;
  rejectionReason?: string | null;
  reason?: string | null;
  result?: string | null;
  backupRunId: string;
  targetDatabaseName: string;
  validationOnly: boolean;
  restoreVerificationRunId?: string | null;
}

export interface RestoreRequestHistoryResponseDto {
  items: RestoreRequestStatusDto[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export async function postManualRestoreRequest(
  body: RestoreRequestBody,
): Promise<RestoreRequestStatusDto> {
  return customInstance<RestoreRequestStatusDto>({
    url: MANUAL_RESTORE_REQUEST_PATH,
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    data: body,
  });
}

export async function postManualRestoreApproval(
  requestId: string,
  body: RestoreApprovalBody,
): Promise<RestoreRequestStatusDto> {
  return customInstance<RestoreRequestStatusDto>({
    url: `/api/admin/restore/approve/${requestId}`,
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    data: body,
  });
}

export async function getManualRestoreRequest(
  requestId: string,
): Promise<RestoreRequestStatusDto> {
  return customInstance<RestoreRequestStatusDto>({
    url: `/api/admin/restore/request/${requestId}`,
    method: 'GET',
  });
}

export async function getManualRestoreHistory(
  page = 1,
  pageSize = 20,
): Promise<RestoreRequestHistoryResponseDto> {
  return customInstance<RestoreRequestHistoryResponseDto>({
    url: MANUAL_RESTORE_HISTORY_PATH,
    method: 'GET',
    params: { page, pageSize },
  });
}
