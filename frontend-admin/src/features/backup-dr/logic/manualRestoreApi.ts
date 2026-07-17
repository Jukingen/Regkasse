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

export const MANUAL_RESTORE_COMPLIANCE_CHECK_PATH =
  '/api/admin/restore/compliance-check' as const;

export function getRestoreComplianceCheckQueryKey(
  backupRunId: string,
  tenantId?: string | null,
) {
  return [
    MANUAL_RESTORE_COMPLIANCE_CHECK_PATH,
    backupRunId,
    tenantId ?? '',
  ] as const;
}

export interface RestoreComplianceCheckItemDto {
  name: string;
  passed: boolean;
  detail?: string | null;
}

export interface RestoreComplianceCheckResponseDto {
  succeeded: boolean;
  code?: string | null;
  error?: string | null;
  backupRunId?: string | null;
  tenantId?: string | null;
  checks: RestoreComplianceCheckItemDto[];
}

/** Pre-restore RKSV compliance (same-tenant, integrity, validation gates). */
export async function getRestoreComplianceCheck(
  backupRunId: string,
  tenantId?: string | null,
): Promise<RestoreComplianceCheckResponseDto> {
  return customInstance<RestoreComplianceCheckResponseDto>({
    url: MANUAL_RESTORE_COMPLIANCE_CHECK_PATH,
    method: 'GET',
    params: {
      backupRunId,
      ...(tenantId ? { tenantId } : {}),
    },
  });
}

export function getManualRestoreReportPath(requestId: string) {
  return `/api/admin/restore/request/${requestId}/report` as const;
}

export function getManualRestoreReportQueryKey(requestId: string) {
  return [getManualRestoreReportPath(requestId)] as const;
}

/** RKSV-oriented restore compliance report for a manual restore request. */
export interface RestoreReportResponseDto {
  restoreId: string;
  tenantId?: string | null;
  tenantName?: string | null;
  restoredAt?: string | null;
  restoredBy?: string | null;
  backupId: string;
  backupDate?: string | null;
  tablesRestored?: number | null;
  recordsRestored?: number | null;
  status: string;
  complianceChecked: boolean;
  rksvCompliant: boolean;
  rksvComplianceNotes?: string | null;
  complianceFindings?: string[];
  validationOnly: boolean;
  targetDatabaseName: string;
  restoreVerificationRunId?: string | null;
  drillStatus?: string | null;
  fiscalSqlPassed?: boolean | null;
  postRestoreContinuityChecksPassed?: boolean | null;
  correlationId?: string | null;
}

export async function getManualRestoreReport(
  requestId: string,
): Promise<RestoreReportResponseDto> {
  return customInstance<RestoreReportResponseDto>({
    url: getManualRestoreReportPath(requestId),
    method: 'GET',
  });
}
