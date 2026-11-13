/**
 * Sidebar registry index for license lockdown menu visibility.
 *
 * The live Ant Design tree is built from `adminSidebarRegistry` + `buildAdminSidebar`.
 * Lockdown hide/disable rules live in `sidebarLicenseLockdown` and are applied after
 * permission filtering in `AdminSidebar`.
 */
export {
  SIDEBAR_LICENSE_LOCKDOWN_MENU_POLICY,
  filterSidebarMenuItemsForLicenseLockdown,
  isLicenseLockdownSidebarActive,
  shouldDisableSidebarKeyForLicenseLockdown,
  shouldHideSidebarKeyForLicenseLockdown,
} from '@/shared/sidebarLicenseLockdown';
