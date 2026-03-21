import { apiClient } from './config';
import { unwrapApiResponseLayer } from './normalizePosPaymentMethods';

/** Row for POS register picker (GET /api/CashRegister). */
export interface CashRegisterRow {
    id: string;
    registerNumber: string;
    location?: string;
}

function isRecord(v: unknown): v is Record<string, unknown> {
    return v != null && typeof v === 'object' && !Array.isArray(v);
}

/** Extract registers array from Ok({ registers }) or wrapped / alternate shapes. */
function extractRegistersArrayFromCashRegisterBody(body: unknown): unknown[] {
    if (Array.isArray(body)) return body;
    if (!isRecord(body)) return [];
    const direct =
        body.registers ??
        body.Registers ??
        body.items ??
        body.Items ??
        body.data ??
        body.Data;
    if (Array.isArray(direct)) return direct;
    const once = unwrapApiResponseLayer(body);
    if (once !== body && once != null) return extractRegistersArrayFromCashRegisterBody(once);
    return [];
}

/**
 * List cash registers visible to the current user (requires CashRegisterView).
 */
export async function listPosCashRegisters(): Promise<CashRegisterRow[]> {
    const raw = await apiClient.get<unknown>('/CashRegister');
    let body: unknown = unwrapApiResponseLayer(raw);
    if (body !== raw) {
        body = unwrapApiResponseLayer(body);
    }
    const regs = extractRegistersArrayFromCashRegisterBody(body);
    const out: CashRegisterRow[] = [];
    for (const r of regs) {
        if (r == null || typeof r !== 'object') continue;
        const row = r as Record<string, unknown>;
        const id = String(row.id ?? row.Id ?? '').trim();
        if (!id) continue;
        const registerNumber = String(row.registerNumber ?? row.RegisterNumber ?? id).trim();
        const location = row.location != null || row.Location != null
            ? String(row.location ?? row.Location ?? '').trim()
            : undefined;
        out.push({ id, registerNumber, location: location || undefined });
    }
    return out;
}
