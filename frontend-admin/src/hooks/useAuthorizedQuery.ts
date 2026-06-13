'use client';

import { useMemo } from 'react';
import { useQuery, type UseQueryOptions, type UseQueryResult } from '@tanstack/react-query';

import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { usePermissions } from '@/hooks/usePermissions';

export interface AuthorizationGateOptions {
    requiredRole?: string | string[];
    requiredPermission?: string | string[];
}

export interface AuthorizedQueryOptions<TQueryFnData = unknown, TError = Error, TData = TQueryFnData>
    extends Omit<UseQueryOptions<TQueryFnData, TError, TData>, 'enabled'>,
        AuthorizationGateOptions {
    enabled?: boolean;
}

export type AuthorizedQueryResult<TData = unknown, TError = Error> = UseQueryResult<TData, TError> & {
    isAuthorized: boolean;
};

function matchesRequiredRole(
    userRole: string | undefined,
    requiredRole: string | string[] | undefined,
): boolean {
    if (!requiredRole) return true;
    if (isSuperAdmin(userRole)) return true;
    if (Array.isArray(requiredRole)) {
        if (requiredRole.length === 0) return true;
        return requiredRole.includes(userRole ?? '');
    }
    return userRole === requiredRole;
}

function matchesRequiredPermission(
    hasPermission: (permission: string) => boolean,
    hasAnyPermission: (permissions: string[]) => boolean,
    requiredPermission: string | string[] | undefined,
): boolean {
    if (!requiredPermission) return true;
    if (Array.isArray(requiredPermission)) {
        return hasAnyPermission(requiredPermission);
    }
    return hasPermission(requiredPermission);
}

/** Evaluates role/permission gates without starting a query — useful for hybrid local/remote hooks. */
export function useAuthorizationGate(options: AuthorizationGateOptions = {}): { isAuthorized: boolean } {
    const { user, isInitialized } = useAuth();
    const { hasPermission, hasAnyPermission } = usePermissions();

    const isAuthorized = useMemo(() => {
        if (!isInitialized || !user) return false;

        const roleOk = matchesRequiredRole(user.role, options.requiredRole);
        const permissionOk = matchesRequiredPermission(
            hasPermission,
            hasAnyPermission,
            options.requiredPermission,
        );

        return roleOk && permissionOk;
    }, [
        isInitialized,
        user,
        options.requiredRole,
        options.requiredPermission,
        hasPermission,
        hasAnyPermission,
    ]);

    return { isAuthorized };
}

export const useAuthorizedQuery = <TQueryFnData = unknown, TError = Error, TData = TQueryFnData>(
    options: AuthorizedQueryOptions<TQueryFnData, TError, TData>,
): AuthorizedQueryResult<TData, TError> => {
    const { requiredRole, requiredPermission, enabled, ...queryOptions } = options;
    const { isAuthorized } = useAuthorizationGate({ requiredRole, requiredPermission });

    const query = useQuery({
        ...queryOptions,
        enabled: enabled !== false && isAuthorized,
        retry: false,
    });

    return {
        ...query,
        isAuthorized,
    };
};
