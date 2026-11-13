/** Shared constants/helpers for admin sider width (store + hook). */

export const ADMIN_SIDER_WIDTH_STORAGE_KEY = 'regkasse-admin-sidebar-width-v1';

export const ADMIN_SIDER_WIDTH_DEFAULT = 256;
export const ADMIN_SIDER_WIDTH_MIN = 220;
export const ADMIN_SIDER_WIDTH_MAX = 420;

export function clampAdminSiderWidth(n: number): number {
  return Math.min(ADMIN_SIDER_WIDTH_MAX, Math.max(ADMIN_SIDER_WIDTH_MIN, Math.round(n)));
}
