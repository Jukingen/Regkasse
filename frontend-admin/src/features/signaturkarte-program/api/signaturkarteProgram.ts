import { customInstance } from '@/lib/axios';

import type {
  SignaturkarteProgramDevice,
  SignaturkarteProgramMarkCompliantResponse,
  SignaturkarteProgramStatus,
} from '../types';

function normalizeTotals(raw: Record<string, unknown> | undefined): SignaturkarteProgramStatus['totals'] {
  const t = raw ?? {};
  return {
    compliant: Number(t.compliant ?? t.Compliant ?? 0),
    nonCompliant: Number(t.nonCompliant ?? t.NonCompliant ?? 0),
    excluded: Number(t.excluded ?? t.Excluded ?? 0),
    revoked: Number(t.revoked ?? t.Revoked ?? 0),
    total: Number(t.total ?? t.Total ?? 0),
  };
}

export function normalizeSignaturkarteProgramStatus(raw: unknown): SignaturkarteProgramStatus | null {
  if (!raw || typeof raw !== 'object') return null;
  const body = raw as Record<string, unknown>;
  const deadline = String(body.deadlineUtc ?? body.DeadlineUtc ?? '');
  if (!deadline) return null;
  const severityRaw = body.bannerSeverity ?? body.BannerSeverity;
  const bannerSeverity =
    severityRaw === 'info' || severityRaw === 'warning' || severityRaw === 'critical'
      ? severityRaw
      : null;

  return {
    enabled: body.enabled === true || body.Enabled === true,
    displayName: String(body.displayName ?? body.DisplayName ?? 'Mai 2027 Signaturkarte'),
    deadlineUtc: deadline,
    daysRemaining: Number(body.daysRemaining ?? body.DaysRemaining ?? 0),
    bannerSeverity,
    totals: normalizeTotals(
      (body.totals ?? body.Totals) as Record<string, unknown> | undefined
    ),
    milestonesNext:
      body.milestonesNext == null && body.MilestonesNext == null
        ? null
        : Number(body.milestonesNext ?? body.MilestonesNext),
    isCertificateExpiry: body.isCertificateExpiry === true || body.IsCertificateExpiry === true,
    separationNote: String(
      body.separationNote ??
        body.SeparationNote ??
        'Mai 2027 Signaturkarte program — independent of certificate expiry.'
    ),
  };
}

export function normalizeSignaturkarteProgramDevice(raw: unknown): SignaturkarteProgramDevice | null {
  if (!raw || typeof raw !== 'object') return null;
  const body = raw as Record<string, unknown>;
  const deviceId = String(body.deviceId ?? body.DeviceId ?? '');
  if (!deviceId) return null;
  return {
    deviceId,
    tenantId: (body.tenantId ?? body.TenantId ?? null) as string | null,
    tenantSlug: (body.tenantSlug ?? body.TenantSlug ?? null) as string | null,
    tenantName: (body.tenantName ?? body.TenantName ?? null) as string | null,
    serialNumber: String(body.serialNumber ?? body.SerialNumber ?? ''),
    provider: (body.provider ?? body.Provider ?? null) as string | null,
    deviceType: (body.deviceType ?? body.DeviceType ?? null) as string | null,
    certificateStatus: (body.certificateStatus ?? body.CertificateStatus ?? null) as string | null,
    expiresAt: (body.expiresAt ?? body.ExpiresAt ?? null) as string | null,
    programCompliantAtUtc: (body.programCompliantAtUtc ??
      body.ProgramCompliantAtUtc ??
      null) as string | null,
    programCompliantBy: (body.programCompliantBy ?? body.ProgramCompliantBy ?? null) as string | null,
    programNote: (body.programNote ?? body.ProgramNote ?? null) as string | null,
    status: String(body.status ?? body.Status ?? 'Open'),
    daysToDeadline: Number(body.daysToDeadline ?? body.DaysToDeadline ?? 0),
    certificateExpiresBeforeDeadline:
      body.certificateExpiresBeforeDeadline === true ||
      body.CertificateExpiresBeforeDeadline === true,
  };
}

export async function getSignaturkarteProgramStatus(
  signal?: AbortSignal
): Promise<SignaturkarteProgramStatus> {
  const data = await customInstance<unknown>({
    url: '/api/admin/tse/signaturkarte-program/status',
    method: 'GET',
    signal,
  });
  const normalized = normalizeSignaturkarteProgramStatus(data);
  if (!normalized) throw new Error('Invalid Signaturkarte program status payload');
  return normalized;
}

export async function listSignaturkarteProgramDevices(params?: {
  status?: string;
  tenantId?: string;
  signal?: AbortSignal;
}): Promise<SignaturkarteProgramDevice[]> {
  const data = await customInstance<unknown>({
    url: '/api/admin/tse/signaturkarte-program/devices',
    method: 'GET',
    params: {
      ...(params?.status ? { status: params.status } : {}),
      ...(params?.tenantId ? { tenantId: params.tenantId } : {}),
    },
    signal: params?.signal,
  });
  const list = Array.isArray(data) ? data : [];
  return list
    .map(normalizeSignaturkarteProgramDevice)
    .filter((d): d is SignaturkarteProgramDevice => d != null);
}

export async function markSignaturkarteProgramCompliant(
  deviceId: string,
  note?: string
): Promise<SignaturkarteProgramMarkCompliantResponse> {
  return customInstance<SignaturkarteProgramMarkCompliantResponse>({
    url: `/api/admin/tse/signaturkarte-program/devices/${deviceId}/mark-compliant`,
    method: 'POST',
    data: { note: note ?? null },
  });
}

export async function exportSignaturkarteProgramCsv(params?: {
  status?: string;
  tenantId?: string;
}): Promise<void> {
  const blob = await customInstance<Blob>({
    url: '/api/admin/tse/signaturkarte-program/export.csv',
    method: 'GET',
    params: {
      ...(params?.status ? { status: params.status } : {}),
      ...(params?.tenantId ? { tenantId: params.tenantId } : {}),
    },
    responseType: 'blob',
  });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = 'signaturkarte-program.csv';
  a.click();
  URL.revokeObjectURL(url);
}
