import { listAdminCashRegisters } from '@/features/cash-registers/api/cashRegisters';
import { writeQuickCashRegisterId } from '@/features/cash-registers/constants/quickSwitch';
import { pickCashRegisterOnTenantSwitch } from '@/features/cash-registers/utils/pickPreferredCashRegister';

/**
 * Loads tenant registers and persists the quick-switch selection before a mandant switch reload.
 * Auto-selects when {@link pickCashRegisterOnTenantSwitch} finds a default or sole register;
 * otherwise clears the stale selection so the user picks manually.
 */
export async function persistCashRegisterOnTenantSwitch(tenantId: string): Promise<string | null> {
    const normalizedTenantId = tenantId.trim();
    if (!normalizedTenantId) {
        writeQuickCashRegisterId(null, normalizedTenantId);
        return null;
    }

    try {
        const page = await listAdminCashRegisters({
            tenantId: normalizedTenantId,
            pageSize: 100,
            excludeStatus: 'Decommissioned',
        });
        const active = page.items.filter((row) => row.isActive !== false);
        const registerId = pickCashRegisterOnTenantSwitch(active, normalizedTenantId);
        writeQuickCashRegisterId(registerId, normalizedTenantId);
        return registerId;
    } catch {
        writeQuickCashRegisterId(null, normalizedTenantId);
        return null;
    }
}
