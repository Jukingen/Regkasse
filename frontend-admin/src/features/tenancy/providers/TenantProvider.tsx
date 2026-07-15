'use client';

import {
    createContext,
    useContext,
    useEffect,
    type ReactNode,
} from 'react';

import { isDevelopment } from '@/features/auth/services/devTenant';
import {
    useCurrentTenantState,
    type CurrentTenant,
} from '@/features/tenancy/hooks/useCurrentTenantState';
import { technicalConsole } from '@/shared/dev/technicalConsole';

const TenantContext = createContext<CurrentTenant | null>(null);

export type TenantProviderProps = {
    children: ReactNode;
};

/**
 * Single resolved mandant snapshot for the whole admin shell (license page, Firmen-Info, header).
 * Wrap authenticated routes inside {@link AuthProvider} so JWT + switcher list are available.
 */
export function TenantProvider({ children }: TenantProviderProps) {
    const currentTenant = useCurrentTenantState();

    useEffect(() => {
        if (!isDevelopment()) {
            return;
        }
        technicalConsole.devLog('Current tenant:', currentTenant);
        technicalConsole.devLog('Tenant ID from hook:', currentTenant.tenantId);
        technicalConsole.devLog('Tenant slug from hook:', currentTenant.tenantSlug);
    }, [
        currentTenant.tenantId,
        currentTenant.tenantSlug,
        currentTenant.tenantName,
        currentTenant.isTenantRecordLoading,
        currentTenant.isDevTenantOverride,
    ]);

    return (
        <TenantContext.Provider value={currentTenant}>{children}</TenantContext.Provider>
    );
}

export function useTenantProviderValue(): CurrentTenant | null {
    return useContext(TenantContext);
}
