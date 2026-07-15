'use client';

import {
    createContext,
    useContext,
    useEffect,
    useMemo,
    useState,
    type ReactNode,
} from 'react';
import { useQuery } from '@tanstack/react-query';

import { authStorage } from '@/features/auth/services/authStorage';
import { isDevelopment } from '@/features/auth/services/devTenant';
import {
    currentTenantQueryKey,
    getCurrentTenant,
    type CurrentTenantDto,
} from '@/features/tenancy/api/getCurrentTenant';
import {
    useCurrentTenantState,
    type CurrentTenant,
} from '@/features/tenancy/hooks/useCurrentTenantState';
import { technicalConsole } from '@/shared/dev/technicalConsole';

export type Tenant = {
    id: string;
    slug: string;
    name: string;
    licenseValid: boolean;
    licenseValidUntilUtc: string | null;
};

export type TenantContextType = {
    tenant: Tenant | null;
    setTenant: (tenant: Tenant | null) => void;
    isLoading: boolean;
    error: Error | null;
    refresh: () => void;
};

const TenantApiContext = createContext<TenantContextType | null>(null);
const CurrentTenantContext = createContext<CurrentTenant | null>(null);

export type TenantProviderProps = {
    children: ReactNode;
};

function mapCurrentTenantDto(dto: CurrentTenantDto): Tenant {
    return {
        id: dto.id,
        slug: dto.slug,
        name: dto.name,
        licenseValid: dto.licenseValid,
        licenseValidUntilUtc: dto.licenseValidUntilUtc,
    };
}

/**
 * FA mandant context: server snapshot via GET /api/tenants/current plus enriched {@link CurrentTenant} for legacy hooks.
 */
export function TenantProvider({ children }: TenantProviderProps) {
    const [tenantOverride, setTenantOverride] = useState<Tenant | null>(null);
    const hasAuthToken = authStorage.hasToken();

    const { data, isLoading, error, refetch } = useQuery({
        queryKey: currentTenantQueryKey,
        queryFn: getCurrentTenant,
        enabled: hasAuthToken,
        staleTime: 60 * 1000,
        refetchOnMount: true,
        refetchOnWindowFocus: isDevelopment(),
    });

    const resolvedTenant = useMemo(() => {
        if (tenantOverride) {
            return tenantOverride;
        }
        return data ? mapCurrentTenantDto(data) : null;
    }, [tenantOverride, data]);

    const currentTenant = useCurrentTenantState(resolvedTenant, isLoading);

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

    const apiContextValue = useMemo<TenantContextType>(
        () => ({
            tenant: resolvedTenant,
            setTenant: setTenantOverride,
            isLoading,
            error: error instanceof Error ? error : error ? new Error(String(error)) : null,
            refresh: () => {
                void refetch();
            },
        }),
        [resolvedTenant, isLoading, error, refetch],
    );

    return (
        <TenantApiContext.Provider value={apiContextValue}>
            <CurrentTenantContext.Provider value={currentTenant}>
                {children}
            </CurrentTenantContext.Provider>
        </TenantApiContext.Provider>
    );
}

/** Server-backed mandant snapshot (GET /api/tenants/current). */
export function useTenant(): TenantContextType {
    const context = useContext(TenantApiContext);
    if (!context) {
        throw new Error('useTenant must be used within TenantProvider');
    }
    return context;
}

/** Enriched mandant read model for existing FA hooks ({@link useCurrentTenant}). */
export function useTenantProviderValue(): CurrentTenant | null {
    return useContext(CurrentTenantContext);
}
