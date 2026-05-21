import { customInstance } from '@/lib/axios';

/** Matches backend `MonatsbelegStatusDto` (GET /api/rksv/monatsbeleg/status/{id}). */
export type MissingMonth = {
    year: number;
    month: number;
    isOverdue: boolean;
    deadline: string;
};

export type MonatsbelegStatusDto = {
    lastCompletedMonth: string | null;
    nextRequiredMonth: string | null;
    missingMonths: MissingMonth[];
    requiresAttention: boolean;
    totalMissingCount: number;
    isRequired?: boolean;
    daysUntilDeadline?: number;
    lastMonatsbelegDate?: string | null;
    warningLevel?: string;
    currentMonthExists?: boolean;
    lastMonthExists?: boolean;
    currentMonthOverdue?: boolean;
    lastMonthMissing?: boolean;
    warningMessage?: string | null;
};

export type MonatsbelegRegisterStatusItemDto = {
    cashRegisterId: string;
    status: MonatsbelegStatusDto;
};

export async function getMonatsbelegStatus(cashRegisterId: string): Promise<MonatsbelegStatusDto> {
    return customInstance<MonatsbelegStatusDto>({
        url: `/api/rksv/monatsbeleg/status/${cashRegisterId}`,
        method: 'GET',
    });
}

/** Single request for dashboard Monatsbeleg table (replaces per-register N+1 from the browser). */
export async function getMonatsbelegStatusOverview(): Promise<MonatsbelegRegisterStatusItemDto[]> {
    return customInstance<MonatsbelegRegisterStatusItemDto[]>({
        url: '/api/rksv/monatsbeleg/status-overview',
        method: 'GET',
    });
}
