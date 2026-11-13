'use client';

import { useQuery } from '@tanstack/react-query';

import { AXIOS_INSTANCE } from '@/lib/axios';
import { parseFilenameFromContentDisposition } from '@/lib/download/progressiveDownload';
import { triggerBlobDownload } from '@/lib/download/exportDownload';

export type DepExportHistoryItem = {
  id: string;
  cashRegisterId: string;
  registerNumber?: string | null;
  fromUtc: string;
  toUtc: string;
  exportedAt: string;
  exportedByUserId: string;
  fileName: string;
  fileSizeBytes: number;
  signatureCount: number;
  groupCount: number;
  legacyJwsCount?: number;
  prueftoolCompatible?: boolean;
  /** String enum preferred; numeric 0–3 accepted for legacy API payloads. */
  status: 'Pending' | 'Processing' | 'Completed' | 'Failed' | 0 | 1 | 2 | 3;
  errorMessage?: string | null;
  hasStoredFile: boolean;
  hasActiveDownloadToken?: boolean;
  downloadTokenExpiresAtUtc?: string | null;
  downloadUrl?: string | null;
  expiresAt?: string | null;
  downloadedAt?: string | null;
  downloadCount?: number;
  canDelete?: boolean;
  scheduleId?: string | null;
  includeSpecialReceipts: boolean;
  includeDailyClosings: boolean;
  isSimulated?: boolean;
  simulationNote?: string | null;
  validationStatus?: string | null;
  validatedAt?: string | null;
};

/** Backend may emit string enums ("Completed") or legacy numeric (Completed = 2). */
export function isDepExportCompleted(
  status: DepExportHistoryItem['status'] | number | string | null | undefined
): boolean {
  return status === 'Completed' || status === 2 || status === '2';
}

/** @deprecated Prefer {@link isDepExportCompleted}. */
export const isDepExportHistoryCompleted = isDepExportCompleted;

export function normalizeDepExportHistoryStatus(
  status: DepExportHistoryItem['status'] | number | string | null | undefined
): DepExportHistoryItem['status'] {
  if (status === 'Pending' || status === 0 || status === '0') return 'Pending';
  if (status === 'Processing' || status === 1 || status === '1') return 'Processing';
  if (status === 'Completed' || status === 2 || status === '2') return 'Completed';
  if (status === 'Failed' || status === 3 || status === '3') return 'Failed';
  return 'Completed';
}

export type DepExportHistoryListResponse = {
  items: DepExportHistoryItem[];
  totalCount: number;
};

export type DepExportScheduleItem = {
  id: string;
  cashRegisterId: string;
  scheduleType: 'Daily' | 'Weekly' | 'Monthly' | 'Yearly' | string;
  dayOfMonth: number;
  timeOfDay: string;
  isActive: boolean;
  recipientEmails?: string | null;
  lastRunAt: string;
  nextRunAt?: string | null;
  createdAt: string;
};

export type CreateDepExportScheduleRequest = {
  cashRegisterId: string;
  scheduleType: string;
  dayOfMonth: number;
  timeOfDay: string;
  recipientEmails?: string | null;
};

export const depExportHistoryQueryKey = (cashRegisterId?: string, page = 1) =>
  ['rksv', 'dep-export', 'history', cashRegisterId ?? 'all', page] as const;

export const depExportSchedulesQueryKey = ['rksv', 'dep-export', 'schedules'] as const;

export function useDepExportHistory(cashRegisterId?: string, page = 1) {
  return useQuery({
    queryKey: depExportHistoryQueryKey(cashRegisterId, page),
    queryFn: async (): Promise<DepExportHistoryListResponse> => {
      const response = await AXIOS_INSTANCE.get<DepExportHistoryListResponse>(
        '/api/admin/rksv/dep-export/history',
        {
          params: { cashRegisterId, page, pageSize: 20 },
        }
      );
      return response.data;
    },
    staleTime: 30_000,
  });
}

export async function createDepExportSchedule(
  request: CreateDepExportScheduleRequest
): Promise<DepExportScheduleItem> {
  const response = await AXIOS_INSTANCE.post<DepExportScheduleItem>(
    '/api/admin/rksv/dep-export/schedule',
    request
  );
  return response.data;
}

export async function deactivateDepExportSchedule(scheduleId: string): Promise<void> {
  await AXIOS_INSTANCE.delete(`/api/admin/rksv/dep-export/schedule/${scheduleId}`);
}

export function useDepExportSchedules() {
  return useQuery({
    queryKey: depExportSchedulesQueryKey,
    queryFn: async (): Promise<DepExportScheduleItem[]> => {
      const response = await AXIOS_INSTANCE.get<DepExportScheduleItem[]>(
        '/api/admin/rksv/dep-export/schedules'
      );
      return response.data;
    },
    staleTime: 30_000,
  });
}

export type DepExportHistoryDetail = DepExportHistoryItem;

export async function fetchDepExportHistoryDetail(
  historyId: string
): Promise<DepExportHistoryDetail> {
  const response = await AXIOS_INSTANCE.get<DepExportHistoryDetail>(
    `/api/admin/rksv/dep-export/history/${historyId}`
  );
  return response.data;
}

export async function fetchDepExportHistoryBlob(historyId: string): Promise<{
  blob: Blob;
  fileName: string | null;
}> {
  const response = await AXIOS_INSTANCE.get<Blob>(
    `/api/admin/rksv/dep-export/download/${historyId}`,
    { responseType: 'blob' }
  );
  const headerRaw = String(
    response.headers?.['content-disposition'] ?? response.headers?.['Content-Disposition'] ?? ''
  );
  const headerName = headerRaw
    ? parseFilenameFromContentDisposition(headerRaw, '')
    : '';
  return {
    blob: new Blob([response.data], { type: 'application/json' }),
    fileName: headerName.trim() || null,
  };
}

export async function downloadDepExportHistoryFile(
  historyId: string,
  fileName?: string | null
): Promise<void> {
  const { blob, fileName: headerName } = await fetchDepExportHistoryBlob(historyId);
  const resolvedName =
    headerName?.trim() ||
    fileName?.trim() ||
    `dep-export-${new Date().toISOString().slice(0, 10)}.json`;
  triggerBlobDownload(blob, resolvedName);
}

export async function deleteDepExportHistory(historyId: string): Promise<void> {
  await AXIOS_INSTANCE.delete(`/api/admin/rksv/dep-export/history/${historyId}`);
}

export type DepExportLastExportResponse = {
  hasExport: boolean;
  lastExportAt?: string | null;
  formatted?: string | null;
  fileName?: string | null;
  fileSizeBytes?: number | null;
  isSimulated?: boolean;
  downloadCount?: number;
  exportId?: string | null;
  cashRegisterId?: string | null;
  registerNumber?: string | null;
};

export type DepExportStatusResponse = {
  isSimulated: boolean;
  environment: string;
  simulationNote?: string | null;
  hasExport: boolean;
  lastExportAt?: string | null;
  lastExportWasSimulated?: boolean | null;
};

export const depExportStatusQueryKey = (cashRegisterId?: string) =>
  ['rksv', 'dep-export', 'status', cashRegisterId ?? 'all'] as const;

export const depExportLastExportQueryKey = (cashRegisterId?: string) =>
  ['rksv', 'dep-export', 'last-export', cashRegisterId ?? 'all'] as const;

export function useDepExportStatus(cashRegisterId?: string) {
  return useQuery({
    queryKey: depExportStatusQueryKey(cashRegisterId),
    queryFn: async (): Promise<DepExportStatusResponse> => {
      const response = await AXIOS_INSTANCE.get<DepExportStatusResponse>(
        '/api/admin/rksv/dep-export/status',
        { params: { cashRegisterId } }
      );
      return response.data;
    },
    staleTime: 30_000,
  });
}

export function useDepExportLastExport(cashRegisterId?: string) {
  return useQuery({
    queryKey: depExportLastExportQueryKey(cashRegisterId),
    queryFn: async (): Promise<DepExportLastExportResponse> => {
      const response = await AXIOS_INSTANCE.get<DepExportLastExportResponse>(
        '/api/admin/rksv/dep-export/last-export',
        { params: { cashRegisterId } }
      );
      return response.data;
    },
    staleTime: 30_000,
  });
}
