import axios from 'axios';

import { AXIOS_INSTANCE } from '@/lib/axios';

export const SENSITIVE_EXPORT_KINDS = {
  GdprDataExport: 'gdpr-data-export',
  SystemBackup: 'system-backup',
  AuditLogExport: 'audit-log-export',
} as const;

export type SensitiveExportKind =
  (typeof SENSITIVE_EXPORT_KINDS)[keyof typeof SENSITIVE_EXPORT_KINDS];

export type SensitiveExportSecurityHeaders = {
  'X-Sensitive-Export-Ack'?: string;
  'X-2FA-Code'?: string;
  'X-Sensitive-Export-Approval-Id'?: string;
  'X-Download-Ticket'?: string;
};

export function buildSensitiveExportHeaders(input: {
  privacyAck?: boolean;
  twoFactorCode?: string;
  approvalId?: string;
  downloadTicket?: string;
}): SensitiveExportSecurityHeaders {
  const headers: SensitiveExportSecurityHeaders = {};
  if (input.privacyAck) headers['X-Sensitive-Export-Ack'] = 'true';
  if (input.twoFactorCode?.trim()) headers['X-2FA-Code'] = input.twoFactorCode.trim();
  if (input.approvalId?.trim()) headers['X-Sensitive-Export-Approval-Id'] = input.approvalId.trim();
  if (input.downloadTicket?.trim()) headers['X-Download-Ticket'] = input.downloadTicket.trim();
  return headers;
}

export function requiresCriticalTwoFactor(kind: SensitiveExportKind): boolean {
  return (
    kind === SENSITIVE_EXPORT_KINDS.SystemBackup || kind === SENSITIVE_EXPORT_KINDS.AuditLogExport
  );
}

export type SensitiveExportApprovalDto = {
  id: string;
  tenantId?: string | null;
  exportKind: string;
  requesterUserId: string;
  reason?: string | null;
  resourceId?: string | null;
  status: string;
  requestedAt: string;
  resolvedByUserId?: string | null;
  resolvedAt?: string | null;
  resolutionNote?: string | null;
  validUntil?: string | null;
};

export async function requestSensitiveExportApproval(input: {
  exportKind: SensitiveExportKind;
  resourceId?: string;
  reason?: string;
}): Promise<SensitiveExportApprovalDto> {
  const res = await AXIOS_INSTANCE.post<SensitiveExportApprovalDto>(
    '/api/admin/download-security/approvals',
    {
      exportKind: input.exportKind,
      resourceId: input.resourceId,
      reason: input.reason,
    }
  );
  return res.data;
}

export async function listPendingSensitiveExportApprovals(): Promise<SensitiveExportApprovalDto[]> {
  const res = await AXIOS_INSTANCE.get<SensitiveExportApprovalDto[]>(
    '/api/admin/download-security/approvals/pending'
  );
  return res.data;
}

export async function approveSensitiveExportApproval(
  id: string,
  note?: string
): Promise<SensitiveExportApprovalDto> {
  const res = await AXIOS_INSTANCE.post<SensitiveExportApprovalDto>(
    `/api/admin/download-security/approvals/${id}/approve`,
    { note }
  );
  return res.data;
}

export async function rejectSensitiveExportApproval(
  id: string,
  note?: string
): Promise<SensitiveExportApprovalDto> {
  const res = await AXIOS_INSTANCE.post<SensitiveExportApprovalDto>(
    `/api/admin/download-security/approvals/${id}/reject`,
    { note }
  );
  return res.data;
}

export function readSensitiveExportErrorCode(err: unknown): string | undefined {
  if (!err || typeof err !== 'object') return undefined;
  if ('code' in err && typeof (err as { code?: unknown }).code === 'string') {
    return (err as { code: string }).code;
  }
  const ax = err as { response?: { data?: unknown } };
  const data = ax.response?.data;
  if (data && typeof data === 'object' && data !== null && 'code' in data) {
    const code = (data as { code?: unknown }).code;
    return typeof code === 'string' ? code : undefined;
  }
  return undefined;
}

export async function resolveSensitiveExportErrorCode(err: unknown): Promise<string | undefined> {
  const sync = readSensitiveExportErrorCode(err);
  if (sync) return sync;
  if (!axios.isAxiosError(err)) return undefined;
  const raw = err.response?.data;
  if (raw instanceof Blob) {
    try {
      const parsed = JSON.parse(await raw.text()) as { code?: string };
      return typeof parsed.code === 'string' ? parsed.code : undefined;
    } catch {
      return undefined;
    }
  }
  return undefined;
}
