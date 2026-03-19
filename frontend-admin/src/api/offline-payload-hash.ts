/**
 * Offline Payload-Hash maintenance API — analyze (with conflict/repairable detail) and CSV export.
 * Backend: /api/admin/offline-payload-hash (not in Orval spec).
 */

import { customInstance } from '@/lib/axios';

const BASE = '/api/admin/offline-payload-hash';

export interface PayloadHashConflictGroup {
  cashRegisterId: string;
  canonicalHash: string;
  mismatchRowIds: string[];
  occupantRowIds: string[];
  skipReason: string;
  latestCreatedAtUtc: string | null;
  severitySuggestion: string;
}

export interface PayloadHashRepairableItem {
  cashRegisterId: string;
  canonicalHash: string;
  rowId: string;
  createdAtUtc: string | null;
}

export interface OfflinePayloadHashAnalyzeResult {
  scanned: number;
  nullOrEmptyPayloadHash: number;
  runtimeMismatchCount: number;
  repairableNoConflictCount: number;
  skippedWouldConflictCount: number;
  sampleMismatchIds: string[];
  mismatchRatioPercent: number;
  legacyDataQualityRiskHigh: boolean;
  warningMessage: string | null;
  conflictGroups: PayloadHashConflictGroup[];
  repairableItems: PayloadHashRepairableItem[];
}

export interface AnalyzeRequest {
  maxRows?: number;
  cashRegisterId?: string | null;
}

export interface RepairRequest {
  maxRows?: number;
  cashRegisterId?: string | null;
  dryRun: boolean;
}

export interface OfflinePayloadHashRepairResult {
  scanned: number;
  updated: number;
  skippedConflict: number;
  skippedAlreadyAligned: number;
  skippedNullPayload: number;
  skippedNormalizeError: number;
  dryRun: boolean;
}

export async function analyzeOfflinePayloadHash(
  body: AnalyzeRequest = {}
): Promise<OfflinePayloadHashAnalyzeResult> {
  return customInstance<OfflinePayloadHashAnalyzeResult>({
    url: `${BASE}/analyze`,
    method: 'POST',
    data: {
      maxRows: body.maxRows ?? 10_000,
      cashRegisterId: body.cashRegisterId ?? undefined,
    },
  });
}

export async function repairOfflinePayloadHash(
  body: RepairRequest
): Promise<OfflinePayloadHashRepairResult> {
  return customInstance<OfflinePayloadHashRepairResult>({
    url: `${BASE}/repair`,
    method: 'POST',
    data: {
      maxRows: body.maxRows ?? 10_000,
      cashRegisterId: body.cashRegisterId ?? undefined,
      dryRun: body.dryRun,
    },
  });
}

/**
 * Download CSV export (same scope as analyze). Uses auth; returns blob for save-as.
 */
export async function downloadExportCsv(params: {
  maxRows?: number;
  cashRegisterId?: string | null;
}): Promise<Blob> {
  const { AXIOS_INSTANCE } = await import('@/lib/axios');
  const search = new URLSearchParams();
  if (params.maxRows != null) search.set('maxRows', String(params.maxRows));
  if (params.cashRegisterId) search.set('cashRegisterId', params.cashRegisterId);
  const qs = search.toString();
  const { data } = await AXIOS_INSTANCE.get<Blob>(`${BASE}/export${qs ? `?${qs}` : ''}`, {
    responseType: 'blob',
  });
  return data;
}
