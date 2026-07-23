'use client';

/**
 * Permission-filtered admin sidebar menu.
 * Built from `adminSidebarRegistry` + RKSV plugin; filtered via `filterSidebarMenuItems`.
 * Known IA areas resolve through `menuPermissionRegistry` (`useMenuPermissions` / `tryRegistryMenuVisibility`).
 */
import { Menu, type MenuProps, Typography } from 'antd';
import { usePathname, useSearchParams } from 'next/navigation';
import React, {
  Suspense,
  useCallback,
  useEffect,
  useLayoutEffect,
  useMemo,
  useState,
  useSyncExternalStore,
} from 'react';

import sidebarStyles from '@/app/(protected)/protected-layout-sidebar.module.css';
import {
  canShowPlatformAdminMenu,
  canShowRksvMenu,
  canViewUsers,
  isSuperAdmin,
} from '@/features/auth/constants/roles';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { isTenantLicenseBlockingModule } from '@/features/cash-registers/hooks/useCashRegisterModuleAccess';
import { useTenantLicenseStatus } from '@/features/license/hooks/useLicenseStatus';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import {
  type SidebarPermissionContext,
  collectSelectableRouteKeysFromMenuItems,
  computeSidebarOpenKeysMerge,
  filterSidebarMenuItems,
  resolveAdminMenuSelectedKeys,
} from '@/shared/adminSidebarNavigation';
import { isMenuItemAllowed, isRksvMenuAreaAllowed } from '@/shared/auth/menuPermissions';
import { logMenuPermissionMappingWarnings } from '@/shared/auth/menuPermissionMappingValidation';
import { tryRegistryMenuVisibility } from '@/shared/auth/menuPermissionRegistry';
import { SIDEBAR_NAV_ITEM_CATALOG, logSidebarMenuPermissionMapWarnings } from '@/shared/adminSidebarRegistry';
import { buildAdminSidebarMenuItems } from '@/shared/buildAdminSidebar';
import { OPERATOR_VERIFICATIONS_COPY } from '@/shared/operatorTruthCopy';
import type { RksvMenuGroup } from '@/shared/rksvMenuModel';
import { MenuPermissionGroupDebugPanel } from '@/components/MenuPermissionGroupDebugPanel';
import { PermissionExplorerDrawer } from '@/components/admin-layout/PermissionExplorerDrawer';
import {
  runAndLogMenuPermissionConsistencyCheck,
  shouldRunDailyConsistencyCheck,
} from '@/features/users/utils/menuPermissionConsistency';
import {
  getRoleMenuPreviewSession,
  subscribeRoleMenuPreview,
} from '@/features/users/utils/roleMenuPreviewSession';

const EMPTY_PERMISSIONS: string[] = [];

export type AdminSidebarMenuPanelProps = {
  menuTheme: 'light' | 'dark';
  menuInlineCollapsed: boolean;
  isMobile: boolean;
  onNavigate?: () => void;
};

type AdminSidebarMenuInnerProps = AdminSidebarMenuPanelProps & {
  menuItems: MenuProps['items'];
  selectableRouteKeys: readonly string[];
  openKeys: string[];
  setOpenKeys: React.Dispatch<React.SetStateAction<string[]>>;
  withSearchParams: boolean;
};

export type UseAdminSidebarMenuResult = {
  menuItems: MenuProps['items'];
  selectableRouteKeys: string[];
  hasAccessibleMenus: boolean;
  canSeeRksv: boolean;
  rksvMenuGroups: RksvMenuGroup[];
  openKeys: string[];
  setOpenKeys: React.Dispatch<React.SetStateAction<string[]>>;
};

/** Builds registry menu tree and filters leaves by role / permission (`canViewMenu` contract). */
export function useAdminSidebarMenu(): UseAdminSidebarMenuResult {
  const pathname = usePathname();
  const { user } = useAuth();
  const { userPermissions, isSuperAdmin: superAdmin } = usePermissions();
  const { t } = useI18n();

  const previewSession = useSyncExternalStore(
    subscribeRoleMenuPreview,
    getRoleMenuPreviewSession,
    () => null
  );

  useEffect(() => {
    if (process.env.NODE_ENV !== 'development') return;
    logMenuPermissionMappingWarnings(SIDEBAR_NAV_ITEM_CATALOG);
    logSidebarMenuPermissionMapWarnings();
  }, []);

  useEffect(() => {
    if (!shouldRunDailyConsistencyCheck()) return;
    runAndLogMenuPermissionConsistencyCheck();
  }, []);

  const permissions =
    previewSession?.permissions?.length
      ? previewSession.permissions
      : userPermissions.length > 0
        ? userPermissions
        : EMPTY_PERMISSIONS;
  const usePermissionFirst = permissions.length > 0;
  const effectiveRole = previewSession?.roleName || user?.role || '';
  const { isSuperAdminUser } = useCurrentTenant();
  const { data: tenantLicense } = useTenantLicenseStatus();
  const hideKassenverwaltungMenu = isTenantLicenseBlockingModule(tenantLicense, isSuperAdminUser);

  const { menuItems: allMenuItems, rksvMenuGroups } = useMemo(
    () =>
      buildAdminSidebarMenuItems({
        t,
        verificationNavLabel: OPERATOR_VERIFICATIONS_COPY.navMenuLabel,
      }),
    [t]
  );

  const canSeeRksv = useMemo(
    () => isRksvMenuAreaAllowed(usePermissionFirst ? permissions : undefined, effectiveRole),
    [usePermissionFirst, permissions, effectiveRole]
  );

  /**
   * Prefer `menuPermissionRegistry` for mapped IA keys/paths (same rules as `useMenuPermissions`).
   * Unmapped leaves keep path-level `ROUTE_PERMISSIONS` via `isMenuItemAllowed`.
   * Tagesabschluss → `daily-closing.view` via registry area `tagesabschluss`.
   */
  const isMenuItemAllowedWithRegistry = useCallback(
    (key: string, perms: string[] | undefined): boolean => {
      const registryVisible = tryRegistryMenuVisibility(
        key,
        { permissions: perms },
        { isSuperAdmin: isSuperAdmin(effectiveRole) || (previewSession ? false : superAdmin) }
      );
      if (registryVisible !== undefined) {
        return registryVisible;
      }
      return isMenuItemAllowed(key, perms);
    },
    [superAdmin, effectiveRole, previewSession]
  );

  const sidebarPermissionCtx = useMemo<SidebarPermissionContext>(
    () => ({
      usePermissionFirst,
      permissions,
      userRole: effectiveRole,
      isMenuItemAllowed: isMenuItemAllowedWithRegistry,
      canViewUsers,
      canShowRksvMenu,
      canShowPlatformAdminMenu,
      isSuperAdminRole: isSuperAdmin,
    }),
    [usePermissionFirst, permissions, effectiveRole, isMenuItemAllowedWithRegistry]
  );

  const menuItems = useMemo(() => {
    const filtered = filterSidebarMenuItems(allMenuItems, sidebarPermissionCtx) ?? [];
    if (!hideKassenverwaltungMenu) {
      return filtered;
    }

    return filtered
      .map((item) => {
        if (
          !item ||
          typeof item !== 'object' ||
          !('children' in item) ||
          !Array.isArray(item.children)
        ) {
          return item;
        }

        return {
          ...item,
          children: item.children.filter(
            (child) =>
              !child ||
              typeof child !== 'object' ||
              !('key' in child) ||
              child.key !== '/kassenverwaltung'
          ),
        };
      })
      .filter(
        (item) =>
          !item || typeof item !== 'object' || !('key' in item) || item.key !== '/kassenverwaltung'
      );
  }, [allMenuItems, hideKassenverwaltungMenu, sidebarPermissionCtx]);

  const selectableRouteKeys = useMemo(
    () => collectSelectableRouteKeysFromMenuItems(menuItems),
    [menuItems]
  );

  const [openKeys, setOpenKeys] = useState<string[]>([]);

  useLayoutEffect(() => {
    setOpenKeys((prev) =>
      computeSidebarOpenKeysMerge({
        pathname,
        prevOpenKeys: prev,
        canSeeRksv,
        rksvGroups: rksvMenuGroups,
      })
    );
  }, [pathname, canSeeRksv, rksvMenuGroups]);

  return {
    menuItems,
    selectableRouteKeys,
    hasAccessibleMenus: (menuItems?.length ?? 0) > 0,
    canSeeRksv,
    rksvMenuGroups,
    openKeys,
    setOpenKeys,
  };
}

function AdminSidebarMenuInner({
  menuTheme,
  menuInlineCollapsed,
  isMobile,
  onNavigate,
  menuItems,
  selectableRouteKeys,
  openKeys,
  setOpenKeys,
  withSearchParams,
}: AdminSidebarMenuInnerProps) {
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const search = withSearchParams ? searchParams.toString() : undefined;

  const selectedKeys = useMemo(
    () => resolveAdminMenuSelectedKeys(pathname, selectableRouteKeys, search),
    [pathname, selectableRouteKeys, search]
  );

  return (
    <Menu
      theme={menuTheme}
      mode="inline"
      className={sidebarStyles.siderMenu}
      selectedKeys={selectedKeys}
      {...(menuInlineCollapsed ? {} : { openKeys, onOpenChange: setOpenKeys })}
      items={menuItems}
      inlineCollapsed={menuInlineCollapsed}
      onClick={() => {
        if (isMobile) onNavigate?.();
      }}
    />
  );
}

export function AdminSidebarBrand({
  collapsed,
  isMobile,
}: {
  collapsed: boolean;
  isMobile: boolean;
}) {
  const { t } = useI18n();

  return (
    <div
      style={{
        height: 64,
        margin: 16,
        background: 'rgba(0, 0, 0, 0.05)',
        borderRadius: 6,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        fontWeight: 'bold',
        overflow: 'hidden',
        whiteSpace: 'nowrap',
      }}
    >
      {collapsed && !isMobile
        ? t('adminShell.branding.sidebarCompact')
        : t('adminShell.branding.sidebarExpanded')}
    </div>
  );
}

export function AdminSidebarEmptyState() {
  const { t } = useI18n();

  return (
    <div style={{ padding: 20, textAlign: 'center' }}>
      <Typography.Text type="secondary">
        {t('adminShell.sidebar.noAccessibleMenus')}
      </Typography.Text>
    </div>
  );
}

/** Inline menu filtered by permissions; wraps search-param aware selected key resolution. */
export function AdminSidebarMenuPanel(props: AdminSidebarMenuPanelProps) {
  const { menuItems, selectableRouteKeys, hasAccessibleMenus, openKeys, setOpenKeys } =
    useAdminSidebarMenu();

  if (!hasAccessibleMenus) {
    return <AdminSidebarEmptyState />;
  }

  return (
    <Suspense
      fallback={
        <AdminSidebarMenuInner
          {...props}
          menuItems={menuItems}
          selectableRouteKeys={selectableRouteKeys}
          openKeys={openKeys}
          setOpenKeys={setOpenKeys}
          withSearchParams={false}
        />
      }
    >
      <AdminSidebarMenuInner
        {...props}
        menuItems={menuItems}
        selectableRouteKeys={selectableRouteKeys}
        openKeys={openKeys}
        setOpenKeys={setOpenKeys}
        withSearchParams
      />
    </Suspense>
  );
}

/** Branding + permission-filtered menu (used inside layout Sider / mobile Drawer). */
export function AdminSidebar({
  collapsed,
  isMobile,
  menuTheme,
  menuInlineCollapsed,
  onNavigate,
}: AdminSidebarMenuPanelProps & {
  collapsed: boolean;
  isMobile: boolean;
}) {
  return (
    <>
      <AdminSidebarBrand collapsed={collapsed} isMobile={isMobile} />
      <MenuPermissionGroupDebugPanel />
      <AdminSidebarMenuPanel
        menuTheme={menuTheme}
        menuInlineCollapsed={menuInlineCollapsed}
        isMobile={isMobile}
        onNavigate={onNavigate}
      />
      <PermissionExplorerDrawer />
    </>
  );
}
