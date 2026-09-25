import { customInstance } from '@/lib/axios';

export const kassenSicherheitTenantsKey = ['admin', 'kassensicherheit', 'tenants'] as const;

export function kassenSicherheitStatusKey(tenantId: string) {
  return ['admin', 'kassensicherheit', 'status', tenantId] as const;
}

export function kassenSicherheitRecentKey(tenantId: string) {
  return ['admin', 'kassensicherheit', 'recent', tenantId] as const;
}

export type KassenSicherheitStatus = {
  tenantId: string;
  flagEnabled: boolean;
  hasTenantOverride: boolean;
  deTssId?: string | null;
  deClientId?: string | null;
  provider: string;
  environment: string;
};

export type KassenSicherheitRecentTransaction = {
  transactionId?: string | null;
  receiptNumber: string;
  status?: string | null;
  createdAt: string;
};

export type KassenSicherheitExportResult = {
  status: string;
};

export function fetchKassenSicherheitStatus(
  tenantId: string,
  signal?: AbortSignal,
): Promise<KassenSicherheitStatus> {
  return customInstance<KassenSicherheitStatus>({
    url: '/api/admin/kassensicherheit/status',
    method: 'GET',
    params: { tenantId },
    signal,
  });
}

export function putKassenSicherheitConfig(body: {
  tenantId: string;
  deTssId?: string | null;
  deClientId?: string | null;
}): Promise<KassenSicherheitStatus> {
  return customInstance<KassenSicherheitStatus>({
    url: '/api/admin/kassensicherheit/config',
    method: 'PUT',
    data: body,
  });
}

export function fetchKassenSicherheitRecent(
  tenantId: string,
  signal?: AbortSignal,
): Promise<KassenSicherheitRecentTransaction[]> {
  return customInstance<KassenSicherheitRecentTransaction[]>({
    url: '/api/admin/kassensicherheit/recent-transactions',
    method: 'GET',
    params: { tenantId },
    signal,
  });
}

export function postKassenSicherheitExport(tenantId: string): Promise<KassenSicherheitExportResult> {
  return customInstance<KassenSicherheitExportResult>({
    url: '/api/admin/kassensicherheit/export-dsfinvk',
    method: 'POST',
    data: { tenantId },
  });
}
