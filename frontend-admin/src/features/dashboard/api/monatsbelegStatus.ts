import { customInstance } from '@/lib/axios';

/** Matches backend `MonatsbelegStatusDto` (GET /api/rksv/monatsbeleg/status/{id}). */
export type MonatsbelegStatusDto = {
    isRequired: boolean;
    daysUntilDeadline: number;
    lastMonatsbelegDate: string | null;
    warningLevel: string;
};

export async function getMonatsbelegStatus(cashRegisterId: string): Promise<MonatsbelegStatusDto> {
    return customInstance<MonatsbelegStatusDto>({
        url: `/api/rksv/monatsbeleg/status/${cashRegisterId}`,
        method: 'GET',
    });
}
