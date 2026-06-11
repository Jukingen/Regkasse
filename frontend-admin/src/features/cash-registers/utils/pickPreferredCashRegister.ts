import type { AdminCashRegisterListItem } from '@/features/cash-registers/api/cashRegisters';

export type CashRegisterPickCandidate = Pick<
    AdminCashRegisterListItem,
    'id' | 'tenantId' | 'isDefaultForTenant'
>;

/**
 * Resolves the register id FA should preselect for the current mandant.
 * Keeps an existing selection when it still belongs to the scoped list; otherwise auto-selects
 * only when {@link pickCashRegisterOnTenantSwitch} finds a sole register or a flagged default.
 */
export function pickPreferredCashRegisterId(
    registers: CashRegisterPickCandidate[],
    currentId: string | null | undefined,
    tenantId: string | null | undefined,
): string | null {
    if (registers.length === 0) {
        return null;
    }

    const scoped = tenantId
        ? registers.filter((row) => row.tenantId === tenantId)
        : registers;
    if (scoped.length === 0) {
        return null;
    }

    const normalizedCurrent = currentId?.trim();
    if (normalizedCurrent && scoped.some((row) => row.id === normalizedCurrent)) {
        return normalizedCurrent;
    }

    const effectiveTenantId = tenantId ?? scoped[0]?.tenantId;
    if (!effectiveTenantId) {
        return null;
    }

    return pickCashRegisterOnTenantSwitch(scoped, effectiveTenantId);
}

/**
 * Used when the mandant changes: auto-select only when a default is flagged or exactly one register exists.
 */
export function pickCashRegisterOnTenantSwitch(
    registers: CashRegisterPickCandidate[],
    tenantId: string,
): string | null {
    const scoped = registers.filter((row) => row.tenantId === tenantId);
    if (scoped.length === 0) {
        return null;
    }
    if (scoped.length === 1) {
        return scoped[0]?.id ?? null;
    }
    return scoped.find((row) => row.isDefaultForTenant === true)?.id ?? null;
}
