import type { CreateMonatsbelegResponse } from '@/api/generated/model';

/** Late-creation fields on create response (until OpenAPI client is regenerated). */
export type CreateMonatsbelegResponseExtended = CreateMonatsbelegResponse & {
    isLateCreated?: boolean;
    daysLate?: number;
    intendedPeriodDate?: string | null;
    year?: number;
    month?: number;
    createdAtUtc?: string;
};

export type MonatsbelegLateSuccessResult = {
    year: number;
    month: number;
    daysLate: number;
    createdAt: string;
    receiptNumber: string;
    isLateCreated: boolean;
};

export function parseCreateMonatsbelegResponse(
    raw: CreateMonatsbelegResponse,
): CreateMonatsbelegResponseExtended {
    return raw as CreateMonatsbelegResponseExtended;
}

export function toMonatsbelegLateSuccessResult(
    response: CreateMonatsbelegResponseExtended,
    fallback: { year: number; month: number },
): MonatsbelegLateSuccessResult {
    return {
        year: response.year ?? fallback.year,
        month: response.month ?? fallback.month,
        daysLate: response.daysLate ?? 0,
        createdAt: response.createdAtUtc ?? new Date().toISOString(),
        receiptNumber: response.receiptNumber?.trim() || '—',
        isLateCreated: response.isLateCreated === true,
    };
}
