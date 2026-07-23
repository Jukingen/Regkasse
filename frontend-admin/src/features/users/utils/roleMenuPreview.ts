/**
 * Build a role sidebar visibility preview from draft permissions.
 */
import type { MenuProps } from 'antd';

import {
  canShowPlatformAdminMenu,
  canShowRksvMenu,
  canViewUsers,
  isSuperAdmin,
} from '@/features/auth/constants/roles';
import {
  type SidebarPermissionContext,
  filterSidebarMenuItems,
} from '@/shared/adminSidebarNavigation';
import { isMenuItemAllowed } from '@/shared/auth/menuPermissions';
import { tryRegistryMenuVisibility } from '@/shared/auth/menuPermissionRegistry';
import { buildAdminSidebarMenuItems } from '@/shared/buildAdminSidebar';
import { OPERATOR_VERIFICATIONS_COPY } from '@/shared/operatorTruthCopy';

export type MenuPreviewVisibility = 'visible' | 'hidden' | 'partial';

export type MenuPreviewNode = {
  key: string;
  label: string;
  visibility: MenuPreviewVisibility;
  children?: MenuPreviewNode[];
};

function buildSidebarCtx(role: string, permissions: readonly string[]): SidebarPermissionContext {
  return {
    usePermissionFirst: true,
    permissions: [...permissions],
    userRole: role,
    isMenuItemAllowed: (key, perms) => {
      const registry = tryRegistryMenuVisibility(
        key,
        { permissions: perms },
        { isSuperAdmin: isSuperAdmin(role) }
      );
      if (registry !== undefined) return registry;
      return isMenuItemAllowed(key, perms);
    },
    canViewUsers,
    canShowRksvMenu,
    canShowPlatformAdminMenu,
    isSuperAdminRole: isSuperAdmin,
  };
}

function extractLabel(node: { label?: unknown; title?: unknown; key?: string }, t: (k: string) => string): string {
  if (typeof node.label === 'string') return node.label;
  if (typeof node.title === 'string') return node.title;
  if (typeof node.key === 'string') return node.key;
  return t('users.roleDrawer.menuPreview.untitled');
}

function walkFullTree(
  items: MenuProps['items'] | undefined,
  visibleKeys: Set<string>,
  t: (k: string) => string
): MenuPreviewNode[] {
  const out: MenuPreviewNode[] = [];
  for (const it of items ?? []) {
    if (!it || typeof it !== 'object') continue;
    if ('type' in it && (it.type === 'divider' || it.type === 'group')) continue;
    const node = it as {
      key?: string | number;
      label?: unknown;
      title?: unknown;
      children?: MenuProps['items'];
    };
    const key = node.key != null ? String(node.key) : '';
    if (!key) continue;

    const children = node.children?.length
      ? walkFullTree(node.children, visibleKeys, t)
      : undefined;

    let visibility: MenuPreviewVisibility;
    if (children?.length) {
      const visCount = children.filter((c) => c.visibility === 'visible').length;
      const hidCount = children.filter((c) => c.visibility === 'hidden').length;
      const partialCount = children.filter((c) => c.visibility === 'partial').length;
      if (visCount === children.length) visibility = 'visible';
      else if (hidCount === children.length) visibility = 'hidden';
      else visibility = 'partial';
      if (partialCount > 0 && visibility === 'visible') visibility = 'partial';
    } else {
      visibility = visibleKeys.has(key) ? 'visible' : 'hidden';
    }

    out.push({
      key,
      label: extractLabel(node, t),
      visibility,
      children,
    });
  }
  return out;
}

function collectVisibleLeafKeys(items: MenuProps['items'] | undefined, into: Set<string>): void {
  for (const it of items ?? []) {
    if (!it || typeof it !== 'object') continue;
    if ('type' in it && it.type === 'divider') continue;
    const node = it as { key?: string | number; children?: MenuProps['items'] };
    if (node.children?.length) {
      collectVisibleLeafKeys(node.children, into);
      continue;
    }
    if (node.key != null) into.add(String(node.key));
  }
}

/**
 * Full sidebar tree annotated with visibility for the given role + permissions.
 */
export function buildRoleMenuPreview(
  roleName: string,
  permissions: readonly string[],
  t: (key: string) => string
): MenuPreviewNode[] {
  const { menuItems: allItems } = buildAdminSidebarMenuItems({
    t,
    verificationNavLabel: OPERATOR_VERIFICATIONS_COPY.navMenuLabel,
  });
  const filtered =
    filterSidebarMenuItems(allItems, buildSidebarCtx(roleName, permissions)) ?? [];
  const visibleKeys = new Set<string>();
  collectVisibleLeafKeys(filtered, visibleKeys);
  return walkFullTree(allItems, visibleKeys, t);
}

export type RoleMenuPreviewStats = {
  visible: number;
  partial: number;
  hidden: number;
};

export function summarizeMenuPreview(nodes: MenuPreviewNode[]): RoleMenuPreviewStats {
  const stats: RoleMenuPreviewStats = { visible: 0, partial: 0, hidden: 0 };
  const walk = (list: MenuPreviewNode[]) => {
    for (const n of list) {
      if (n.children?.length) walk(n.children);
      else {
        if (n.visibility === 'visible') stats.visible += 1;
        else if (n.visibility === 'partial') stats.partial += 1;
        else stats.hidden += 1;
      }
    }
  };
  walk(nodes);
  return stats;
}
