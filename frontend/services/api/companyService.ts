import { apiClient } from './config';
import { API_PATHS } from './apiPaths';
import type { PosWorkingHours } from '../../utils/viennaTagesabschlussReminder';
import type {
    PosWorkingHoursExtended,
    PosWorkingHoursSpecialDay,
} from '../../utils/workingHoursStatus';

/** Matches backend <c>PosCompanyInfoDto</c> (GET /api/pos/company). */
export interface PosCompanyInfo {
    companyName: string;
    companyAddress: string;
    taxNumber: string;
    receiptFooter?: string | null;
    timeZone?: string;
    workingHours?: PosWorkingHoursExtended | null;
}

function readString(raw: Record<string, unknown>, ...keys: string[]): string {
    for (const key of keys) {
        const value = raw[key];
        if (typeof value === 'string') return value;
    }
    return '';
}

function readDay(raw: unknown): PosWorkingHours['monday'] {
    const record =
        raw && typeof raw === 'object' && !Array.isArray(raw)
            ? (raw as Record<string, unknown>)
            : {};
    const openTime = readString(record, 'openTime', 'OpenTime') || '09:00';
    const closeTime = readString(record, 'closeTime', 'CloseTime') || '22:00';
    const isClosed = record.isClosed === true || record.IsClosed === true;
    return { openTime, closeTime, isClosed };
}

function readSpecialDay(raw: unknown): PosWorkingHoursSpecialDay | null {
    if (!raw || typeof raw !== 'object' || Array.isArray(raw)) return null;
    const record = raw as Record<string, unknown>;
    const date = readString(record, 'date', 'Date');
    if (!date) return null;
    const openRaw = record.openTime ?? record.OpenTime;
    const closeRaw = record.closeTime ?? record.CloseTime;
    return {
        date,
        isClosed: record.isClosed === true || record.IsClosed === true,
        openTime: typeof openRaw === 'string' ? openRaw : null,
        closeTime: typeof closeRaw === 'string' ? closeRaw : null,
    };
}

function parseWorkingHours(raw: unknown): PosWorkingHoursExtended | null {
    if (!raw || typeof raw !== 'object' || Array.isArray(raw)) return null;
    const record = raw as Record<string, unknown>;
    const reminder =
        typeof record.reminderHoursBeforeClosing === 'number'
            ? record.reminderHoursBeforeClosing
            : typeof record.ReminderHoursBeforeClosing === 'number'
              ? record.ReminderHoursBeforeClosing
              : 1;

    const stopOnline =
        typeof record.stopOnlineOrdersMinutesBeforeClose === 'number'
            ? record.stopOnlineOrdersMinutesBeforeClose
            : typeof record.StopOnlineOrdersMinutesBeforeClose === 'number'
              ? record.StopOnlineOrdersMinutesBeforeClose
              : 30;

    const closedMessage =
        readString(record, 'closedDayMessage', 'ClosedDayMessage') || 'Heute geschlossen';

    const specialRaw = record.specialDays ?? record.SpecialDays;
    const specialDays = Array.isArray(specialRaw)
        ? specialRaw.map(readSpecialDay).filter((d): d is PosWorkingHoursSpecialDay => d != null)
        : [];

    return {
        reminderHoursBeforeClosing: reminder,
        stopOnlineOrdersMinutesBeforeClose: stopOnline,
        autoClosePOSAtClosing:
            record.autoClosePOSAtClosing === true || record.AutoClosePOSAtClosing === true,
        closedDayMessage: closedMessage,
        specialDays,
        monday: readDay(record.monday ?? record.Monday),
        tuesday: readDay(record.tuesday ?? record.Tuesday),
        wednesday: readDay(record.wednesday ?? record.Wednesday),
        thursday: readDay(record.thursday ?? record.Thursday),
        friday: readDay(record.friday ?? record.Friday),
        saturday: readDay(record.saturday ?? record.Saturday),
        sunday: readDay(record.sunday ?? record.Sunday),
    };
}

export function parsePosCompanyInfo(raw: unknown): PosCompanyInfo {
    const record =
        raw && typeof raw === 'object' && !Array.isArray(raw)
            ? (raw as Record<string, unknown>)
            : {};

    const footer = record.receiptFooter ?? record.ReceiptFooter;
    const timeZone = readString(record, 'timeZone', 'TimeZone') || 'Europe/Vienna';

    return {
        companyName: readString(record, 'companyName', 'CompanyName'),
        companyAddress: readString(record, 'companyAddress', 'CompanyAddress'),
        taxNumber: readString(record, 'taxNumber', 'TaxNumber'),
        receiptFooter:
            typeof footer === 'string' ? footer : footer == null ? null : String(footer),
        timeZone,
        workingHours: parseWorkingHours(record.workingHours ?? record.WorkingHours),
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
