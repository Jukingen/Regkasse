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
};

export async function getMonatsbelegStatus(cashRegisterId: string): Promise<MonatsbelegStatusDto> {
    return customInstance<MonatsbelegStatusDto>({
        url: `/api/rksv/monatsbeleg/status/${cashRegisterId}`,
        method: 'GET',
    });
}
