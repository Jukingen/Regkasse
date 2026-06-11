'use client';

import { useQuery } from '@tanstack/react-query';
import { AXIOS_INSTANCE } from '@/lib/axios';

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
    status: 'Pending' | 'Processing' | 'Completed' | 'Failed';
    errorMessage?: string | null;
    hasStoredFile: boolean;
    scheduleId?: string | null;
    includeSpecialReceipts: boolean;
    includeDailyClosings: boolean;
};

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
            const response = await AXIOS_INSTANCE.get<DepExportHistoryListResponse>('/api/admin/rksv/dep-export/history', {
                params: { cashRegisterId, page, pageSize: 20 },
            });
            return response.data;
        },
        staleTime: 30_000,
    });
}

export async function createDepExportSchedule(request: CreateDepExportScheduleRequest): Promise<DepExportScheduleItem> {
    const response = await AXIOS_INSTANCE.post<DepExportScheduleItem>('/api/admin/rksv/dep-export/schedule', request);
    return response.data;
}

export async function deactivateDepExportSchedule(scheduleId: string): Promise<void> {
    await AXIOS_INSTANCE.delete(`/api/admin/rksv/dep-export/schedule/${scheduleId}`);
}

export function useDepExportSchedules() {
    return useQuery({
        queryKey: depExportSchedulesQueryKey,
        queryFn: async (): Promise<DepExportScheduleItem[]> => {
            const response = await AXIOS_INSTANCE.get<DepExportScheduleItem[]>('/api/admin/rksv/dep-export/schedules');
            return response.data;
        },
        staleTime: 30_000,
    });
}

export type DepExportHistoryDetail = DepExportHistoryItem;

export async function fetchDepExportHistoryDetail(historyId: string): Promise<DepExportHistoryDetail> {
    const response = await AXIOS_INSTANCE.get<DepExportHistoryDetail>(`/api/admin/rksv/dep-export/history/${historyId}`);
    return response.data;
}

export async function downloadDepExportHistoryFile(historyId: string, fileName: string): Promise<void> {
    const response = await AXIOS_INSTANCE.get<Blob>(
        `/api/admin/rksv/dep-export/history/${historyId}/download`,
        { responseType: 'blob' },
    );
    const blob = new Blob([response.data], { type: 'application/json' });
    const url = globalThis.URL.createObjectURL(blob);
    const anchor = globalThis.document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    globalThis.URL.revokeObjectURL(url);
}
