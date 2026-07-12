'use client';

import { useMemo } from 'react';
import type { MenuProps } from 'antd';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { isTenantLicenseBlockingModule } from '@/features/cash-registers/hooks/useCashRegisterModuleAccess';
import { useTenantLicenseStatus } from '@/features/license/hooks/useLicenseStatus';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { isMenuItemAllowed } from '@/shared/auth/menuPermissions';
import {
    canShowPlatformAdminMenu,
    canShowRksvMenu,
    canViewUsers,
    isSuperAdmin,
} from '@/features/auth/constants/roles';
import { OPERATOR_VERIFICATIONS_COPY } from '@/shared/operatorTruthCopy';
import { buildAdminSidebarMenuItems } from '@/shared/buildAdminSidebar';
import {
    collectSelectableRouteKeysFromMenuItems,
    filterSidebarMenuItems,
    type SidebarPermissionContext,
} from '@/shared/adminSidebarNavigation';
import type { MenuSearchIndex } from '@/components/admin-layout/GlobalSearch.types';
import {
    buildMenuSearchIndexSource,
    filterMenuSearchIndexByRouteKeys,
} from '@/shared/searchUtils';

const EMPTY_PERMISSIONS: string[] = [];

function stripKassenverwaltungFromMenu(items: MenuProps['items']): MenuProps['items'] {
    return (
        items
            ?.map((item) => {
                if (!item || typeof item !== 'object' || !('children' in item) || !Array.isArray(item.children)) {
                    return item;
                }

                return {
                    ...item,
                    children: item.children.filter(
                        (child) =>
                            !child ||
                            typeof child !== 'object' ||
                            !('key' in child) ||
                            child.key !== '/kassenverwaltung',
                    ),
                };
            })
            .filter(
                (item) =>
                    !item ||
                    typeof item !== 'object' ||
                    !('key' in item) ||
                    item.key !== '/kassenverwaltung',
            ) ?? []
    );
}

/**
 * Flat, permission-filtered menu search index aligned with `filterSidebarMenuItems` + sidebar post-filters.
 */
export function useMenuSearchIndex(): MenuSearchIndex {
    const { t, textLocale } = useI18n();
    const { user } = useAuth();
    const { userPermissions } = usePermissions();
    const { isSuperAdminUser } = useCurrentTenant();
    const { data: tenantLicense } = useTenantLicenseStatus();

    const permissions = userPermissions.length > 0 ? userPermissions : EMPTY_PERMISSIONS;
    const usePermissionFirst = permissions.length > 0;
    const userRole = user?.role ?? '';
    const hideKassenverwaltung = isTenantLicenseBlockingModule(tenantLicense, isSuperAdminUser);

    const sidebarPermissionCtx = useMemo<SidebarPermissionContext>(
        () => ({
            usePermissionFirst,
            permissions,
            userRole,
            isMenuItemAllowed,
            canViewUsers,
            canShowRksvMenu,
            canShowPlatformAdminMenu,
            isSuperAdminRole: isSuperAdmin,
        }),
        [usePermissionFirst, permissions, userRole],
    );

    const allowedMenuKeys = useMemo(() => {
        const { menuItems: allMenuItems } = buildAdminSidebarMenuItems({
            t,
            verificationNavLabel: OPERATOR_VERIFICATIONS_COPY.navMenuLabel,
        });

        let filtered = filterSidebarMenuItems(allMenuItems, sidebarPermissionCtx) ?? [];
        if (hideKassenverwaltung) {
            filtered = stripKassenverwaltungFromMenu(filtered) ?? [];
        }

        return new Set(collectSelectableRouteKeysFromMenuItems(filtered));
    }, [t, sidebarPermissionCtx, hideKassenverwaltung]);

    const source = useMemo(() => buildMenuSearchIndexSource(t), [t, textLocale]);

    const items = useMemo(
        () => filterMenuSearchIndexByRouteKeys(source.items, allowedMenuKeys),
        [source.items, allowedMenuKeys],
    );

    return useMemo(
        () => ({
            items,
            locale: textLocale,
            allowedMenuKeys,
        }),
        [items, textLocale, allowedMenuKeys],
    );
}
