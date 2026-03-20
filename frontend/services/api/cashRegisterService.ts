import { apiClient } from './config';
import { unwrapApiResponseLayer } from './normalizePosPaymentMethods';

/** Row for POS register picker (GET /api/CashRegister). */
export interface CashRegisterRow {
    id: string;
    registerNumber: string;
    location?: string;
}

/**
 * List cash registers visible to the current user (requires CashRegisterView).
 */
export async function listPosCashRegisters(): Promise<CashRegisterRow[]> {
    const raw = await apiClient.get<unknown>('/CashRegister');
    const body = unwrapApiResponseLayer(raw) as Record<string, unknown> | null;
    if (body == null || typeof body !== 'object') return [];
    const regs = (body.registers ?? body.Registers) as unknown;
    if (!Array.isArray(regs)) return [];
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
