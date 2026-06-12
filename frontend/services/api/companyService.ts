import { apiClient } from './config';
import { API_PATHS } from './apiPaths';

/** Matches backend <c>PosCompanyInfoDto</c> (GET /api/pos/company). */
export interface PosCompanyInfo {
    companyName: string;
    companyAddress: string;
    taxNumber: string;
    receiptFooter?: string | null;
}

function readString(raw: Record<string, unknown>, ...keys: string[]): string {
    for (const key of keys) {
        const value = raw[key];
        if (typeof value === 'string') return value;
    }
    return '';
}

export function parsePosCompanyInfo(raw: unknown): PosCompanyInfo {
    const record =
        raw && typeof raw === 'object' && !Array.isArray(raw)
            ? (raw as Record<string, unknown>)
            : {};

    const footer = record.receiptFooter ?? record.ReceiptFooter;

    return {
        companyName: readString(record, 'companyName', 'CompanyName'),
        companyAddress: readString(record, 'companyAddress', 'CompanyAddress'),
        taxNumber: readString(record, 'taxNumber', 'TaxNumber'),
        receiptFooter:
            typeof footer === 'string' ? footer : footer == null ? null : String(footer),
    };
}

/**
 * Tenant RKSV company header for POS UI.
 * <c>X-Tenant-Id</c> (dev slug) and JWT are applied by the axios request interceptor in <c>config.ts</c>.
 */
export async function getCompanySettings(): Promise<PosCompanyInfo> {
    const raw = await apiClient.get<unknown>(API_PATHS.COMPANY.INFO);
    return parsePosCompanyInfo(raw);
}
