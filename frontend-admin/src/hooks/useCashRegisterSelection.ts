'use client';

import { useEffect, useMemo, useState } from 'react';

import type { AdminCashRegisterListItem } from '@/features/cash-registers/api/cashRegisters';
import {
    readQuickCashRegisterId,
    writeQuickCashRegisterId,
} from '@/features/cash-registers/constants/quickSwitch';
import { useAdminCashRegisterList } from '@/features/cash-registers/hooks/useAdminCashRegisterList';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';

export type CashRegisterSelectOption = {
    value: string;
    label: string;
    register: AdminCashRegisterListItem;
};

export type UseCashRegisterSelectionOptions = {
    enabled?: boolean;
    /** Super Admin mandant override; defaults to resolved current tenant. */
    tenantId?: string;
    /** Controlled selected register id. */
    value?: string;
    onChange?: (registerId: string | undefined, register?: AdminCashRegisterListItem) => void;
    /**
     * Parent owns selection state (e.g. value may be undefined until auto-select runs).
     * Defaults to true when {@link onChange} is provided.
     */
    controlled?: boolean;
    /** Preselect when multiple registers load (default false for optional filters). */
    autoSelect?: boolean;
    /** Always preselect the sole register, even when `autoSelect` is false (default true). */
    autoSelectSingle?: boolean;
    /** Persist selection to session quick-switch storage (tenant-scoped when possible). */
    persistSelection?: boolean;
};

function scopeRegistersForTenant(
    registers: AdminCashRegisterListItem[],
    tenantId: string | null | undefined,
): AdminCashRegisterListItem[] {
    if (!tenantId) {
        return registers;
    }
    return registers.filter((row) => row.tenantId === tenantId);
}

/**
 * Operational auto-select: valid stored/current id → sole register → default flag → first in list.
 */
function resolveAutoSelectRegisterId(
    registers: AdminCashRegisterListItem[],
    preferredId: string | null | undefined,
    tenantId: string | null | undefined,
): string | null {
    const scoped = scopeRegistersForTenant(registers, tenantId);
    if (scoped.length === 0) {
        return null;
    }

    const normalizedPreferred = preferredId?.trim();
    if (normalizedPreferred && scoped.some((row) => row.id === normalizedPreferred)) {
        return normalizedPreferred;
    }

    if (scoped.length === 1) {
        return scoped[0]?.id ?? null;
    }

    const defaultRegister = scoped.find((row) => row.isDefaultForTenant === true);
    if (defaultRegister?.id) {
        return defaultRegister.id;
    }

    return scoped[0]?.id ?? null;
}

function resolveInitialSelection(
    registers: AdminCashRegisterListItem[],
    options: {
        selectedRegisterId: string | undefined;
        storedId: string | null;
        tenantId: string | null | undefined;
        autoSelect: boolean;
        autoSelectSingle: boolean;
        persistSelection: boolean;
    },
): string | null {
    const { selectedRegisterId, storedId, tenantId, autoSelect, autoSelectSingle, persistSelection } =
        options;

    if (registers.length === 1 && autoSelectSingle) {
        return registers[0]?.id ?? null;
    }

    if (autoSelect) {
        return resolveAutoSelectRegisterId(
            registers,
            selectedRegisterId ?? storedId,
            tenantId,
        );
    }

    if (
        persistSelection &&
        storedId &&
        !selectedRegisterId &&
        registers.some((row) => row.id === storedId)
    ) {
        return storedId;
    }

    return null;
}

export function buildCashRegisterSelectOptions(
    registers: AdminCashRegisterListItem[],
): CashRegisterSelectOption[] {
    return registers.map((register) => ({
        value: register.id,
        label: `${register.registerNumber} — ${register.location ?? ''}`.trim(),
        register,
    }));
}

/**
 * Canonical register list + optional auto-selection for reporting and operational pages.
 * Uses `GET /api/admin/cash-registers` with JWT tenant resolution for Manager.
 */
export function useCashRegisterSelection(options: UseCashRegisterSelectionOptions = {}) {
    const {
        enabled = true,
        tenantId: tenantIdOverride,
        value,
        onChange,
        controlled,
        autoSelect = false,
        autoSelectSingle = true,
        persistSelection = false,
    } = options;

    const { tenantId: contextTenantId, isSuperAdminUser } = useCurrentTenant();
    const effectiveTenantId = tenantIdOverride ?? contextTenantId ?? undefined;
    const listAllTenants = isSuperAdminUser && !effectiveTenantId;

    const query = useAdminCashRegisterList({
        tenantId: effectiveTenantId,
        allowAllTenants: listAllTenants,
        allowTenantScopedDefault: !listAllTenants,
        enabled: enabled && (listAllTenants || !isSuperAdminUser || Boolean(effectiveTenantId)),
    });

    const registers = query.registers;
    const isControlled = controlled ?? onChange !== undefined;
    const [internalId, setInternalId] = useState<string | undefined>();

    const selectedRegisterId = isControlled ? (value ?? internalId) : internalId;

    const selectedRegister = useMemo(
        () => registers.find((row) => row.id === selectedRegisterId) ?? null,
        [registers, selectedRegisterId],
    );

    const registerOptions = useMemo(() => buildCashRegisterSelectOptions(registers), [registers]);

    useEffect(() => {
        if (query.isLoading || registers.length === 0) {
            return;
        }

        const storedId = persistSelection
            ? readQuickCashRegisterId(effectiveTenantId ?? null)
            : null;
        const resolved = resolveInitialSelection(registers, {
            selectedRegisterId,
            storedId,
            tenantId: effectiveTenantId ?? null,
            autoSelect,
            autoSelectSingle,
            persistSelection,
        });

        if (!resolved || resolved === selectedRegisterId) {
            return;
        }

        setInternalId(resolved);
        if (persistSelection) {
            writeQuickCashRegisterId(resolved, effectiveTenantId ?? null);
        }
        const register = registers.find((row) => row.id === resolved);
        onChange?.(resolved, register);
    }, [
        autoSelect,
        autoSelectSingle,
        effectiveTenantId,
        onChange,
        persistSelection,
        query.isLoading,
        registers,
        selectedRegisterId,
    ]);

    const setSelectedRegisterId = (next: string | undefined) => {
        setInternalId(next);
        if (persistSelection) {
            writeQuickCashRegisterId(next ?? null, effectiveTenantId ?? null);
        }
        const register = next ? registers.find((row) => row.id === next) : undefined;
        onChange?.(next, register);
    };

    return {
        registers,
        registerOptions,
        selectedRegister,
        selectedRegisterId,
        setSelectedRegisterId,
        isLoading: query.isLoading,
        isFetching: query.isFetching,
        error: query.error,
        hasRegisters: registers.length > 0,
        hasMultipleRegisters: registers.length > 1,
        isSingleRegister: registers.length === 1,
        refetch: query.refetch,
    };
}

