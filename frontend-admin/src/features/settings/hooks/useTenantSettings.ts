'use client';

import { useMemo } from 'react';
import { useGetApiCompanySettings } from '@/api/generated/company-settings/company-settings';
import { useGetApiSettingsTaxRates } from '@/api/generated/settings/settings';
import { useGetApiTseHealth } from '@/api/generated/tse/tse';
import { useCashRegisterSelection } from '@/hooks/useCashRegisterSelection';

export type TenantSettingsView = {
    companyName: string;
    vatId: string;
    address: string;
    taxRate: number | null;
    isActive: boolean;
    registerId: string | null;
    registerNumber: string | null;
    registerLocation: string | null;
    tseConnected: boolean;
    tseStatusLabel: string;
    tseType: string | null;
    tseSerial: string | null;
    certificateValidUntil: string | null;
};

function resolvePrimaryTaxRate(taxRates: Record<string, number> | undefined): number | null {
    if (!taxRates) {
        return null;
    }
    if (typeof taxRates.standard === 'number') {
        return taxRates.standard;
    }
    const values = Object.values(taxRates).filter((value) => typeof value === 'number');
    return values.length > 0 ? Math.max(...values) : null;
}

function mapTseConnection(status: string | null | undefined): boolean {
    const normalized = (status ?? '').trim();
    return normalized === 'Online' || normalized === 'Degraded';
}

export function useTenantSettings(options?: { enabled?: boolean }) {
    const enabled = options?.enabled ?? true;

    const companyQuery = useGetApiCompanySettings({ query: { enabled } });
    const taxRatesQuery = useGetApiSettingsTaxRates({ query: { enabled } });
    const tseQuery = useGetApiTseHealth({ query: { enabled } });
    const registerSelection = useCashRegisterSelection({
        enabled,
        autoSelectSingle: true,
    });

    const data = useMemo((): TenantSettingsView | null => {
        const company = companyQuery.data;
        if (!company) {
            return null;
        }

        const tseStatusLabel = (tseQuery.data?.status ?? '').trim();
        const register = registerSelection.selectedRegister;

        return {
            companyName: company.companyName?.trim() ?? '',
            vatId: company.companyTaxNumber?.trim() || company.companyVatNumber?.trim() || '',
            address: company.companyAddress?.trim() ?? '',
            taxRate: resolvePrimaryTaxRate(taxRatesQuery.data as Record<string, number> | undefined),
            isActive: company.isActive !== false,
            registerId: registerSelection.selectedRegisterId ?? null,
            registerNumber: register?.registerNumber?.trim() ?? null,
            registerLocation: register?.location?.trim() ?? null,
            tseConnected: mapTseConnection(tseStatusLabel),
            tseStatusLabel: tseStatusLabel || '—',
            tseType: company.defaultTseDeviceId?.trim() || null,
            tseSerial: null,
            certificateValidUntil: null,
        };
    }, [
        companyQuery.data,
        registerSelection.selectedRegister,
        registerSelection.selectedRegisterId,
        taxRatesQuery.data,
        tseQuery.data?.status,
    ]);

    const isLoading =
        companyQuery.isLoading ||
        taxRatesQuery.isLoading ||
        tseQuery.isLoading ||
        registerSelection.isLoading;

    const refetch = async () => {
        await Promise.all([
            companyQuery.refetch(),
            taxRatesQuery.refetch(),
            tseQuery.refetch(),
            registerSelection.refetch(),
        ]);
    };

    return {
        data,
        companySettings: companyQuery.data,
        registers: registerSelection.registers,
        registerOptions: registerSelection.registerOptions,
        selectedRegisterId: registerSelection.selectedRegisterId,
        setSelectedRegisterId: registerSelection.setSelectedRegisterId,
        selectedRegister: registerSelection.selectedRegister,
        isLoading,
        isFetching:
            companyQuery.isFetching ||
            taxRatesQuery.isFetching ||
            tseQuery.isFetching ||
            registerSelection.isFetching,
        isError: companyQuery.isError,
        error: companyQuery.error,
        refetch,
    };
}
