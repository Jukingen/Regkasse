'use client';

import { DeleteOutlined, HistoryOutlined, PlusOutlined, PushpinOutlined, SaveOutlined, ThunderboltOutlined } from '@ant-design/icons';
import {
  Alert,
  Button,
  Card,
  Checkbox,
  Col,
  Collapse,
  Drawer,
  Empty,
  Row,
  Select,
  Space,
  Spin,
  Switch,
  Tabs,
  Tag,
  Tooltip,
  Typography,
} from 'antd';
/**
 * Role management drawer: left = role list (System + Custom), right = grouped permission checklist.
 * System roles are readonly; custom roles are editable. No legacy/deprecated role category.
 */
import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import Link from 'next/link';

import { SimpleList as List } from '@/components/ui/SimpleList';
import { createPermissionConfigBackup } from '@/features/users/api/permissionConfigBackupsApi';
import { useAntdApp } from '@/hooks/useAntdApp';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n/I18nProvider';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { validateCatalogAlignment } from '@/shared/auth/validateCatalogAlignment';

import type { PermissionCatalogItemDto, RoleWithPermissionsDto } from '../api/usersGateway';
import { ROLE_PRESETS, type RolePreset, findRolePresetById, getPresetKeysInCatalog } from '../constants/rolePresets';
import { comparePermissionGroupSlugs, permissionCatalogGroupToSlug } from '../utils/permissionCatalogGroup';
import {
  resolvePermissionDisplayLabel,
  resolvePermissionGroupLabel,
} from '../utils/permissionDisplayLabel';
import {
  buildPermissionSearchEntries,
  PERMISSION_SEARCH_DEBOUNCE_MS,
  searchPermissions,
} from '../utils/permissionSearchIndex';
import { buildPermissionUiGroupsFromCatalog, PERMISSION_GROUPS } from '@/shared/auth/permissionGroupRegistry';
import type { PermissionGroupKey } from '@/shared/auth/permissionGroupRegistry';
import { HighlightedText } from './HighlightedText';
import { RolePermissionHistoryTab } from './RolePermissionHistoryTab';
import { PermissionBatchToolbar } from './PermissionBatchToolbar';
import {
  PermissionCatalogToolbar,
  type PermissionCommandItem,
  type PermissionQuickFilterPreset,
  type PermissionStatusFilter,
} from './PermissionCatalogToolbar';
import { PermissionChangesPanel } from './PermissionChangesPanel';
import { PermissionCommonTasksPanel, type PermissionCommonTaskId } from './PermissionCommonTasksPanel';
import { PermissionGuidedTour } from './PermissionGuidedTour';
import { PermissionHealthCheckAlert } from './PermissionHealthCheckAlert';
import { PermissionListRow } from './PermissionListRow';
import { MenuPermissionConsistencyButton } from './MenuPermissionConsistencyButton';
import { PermissionMenuTags } from './PermissionMenuTags';
import { RoleImpactAnalysisModal } from './RoleImpactAnalysisModal';
import { RoleMenuPreviewPanel } from './RoleMenuPreviewPanel';
import { RolePackagesSection } from './RolePackagesSection';
import { RolePermissionComparePanel } from './RolePermissionComparePanel';
import { RolePresetPreviewCard } from './RolePresetPreviewCard';
import { useDebounce } from '@/hooks/useDebounce';
import { isPermissionImpliedOnly } from '@/shared/auth/permissionImplications';
import { resolveSidebarIconElement } from '@/shared/buildAdminSidebar';
import { getMenuChipsForPermissionGroup, getMenuItemsAffectedByPermission, getPermissionsAffectingMenu, listSidebarMenuFilterOptions } from '../utils/permissionMenuImpact';
import {
  loadPinnedMenuFilters,
  togglePinnedMenuFilter,
} from '../utils/pinnedMenuFilters';
import {
  comparePermissionSets,
  diffKindToHighlight,
} from '../utils/permissionRoleDiff';
import { useQueryClient } from '@tanstack/react-query';
import { rolesWithPermissionsQueryKey } from '../api/usersGateway';

/** Stable empty refs so default props do not create new [] each render (avoids effect dependency churn). */
const EMPTY_ROLES: RoleWithPermissionsDto[] = [];
const EMPTY_CATALOG: PermissionCatalogItemDto[] = [];

const CANONICAL_ROLE_NAMES = new Set([
  'SuperAdmin',
  'Manager',
  'Cashier',
  'Waiter',
  'Kitchen',
  'ReportViewer',
  'Accountant',
]);

function setsEqual(a: Set<string>, b: Set<string>): boolean {
  if (a.size !== b.size) return false;
  let equal = true;
  a.forEach((x) => {
    if (!b.has(x)) equal = false;
  });
  return equal;
}

/** Summary category keys for the detail panel (order and which permission groups map to them). */
const SUMMARY_CATEGORY_KEYS = [
  { key: 'pos', summaryKey: 'posLogin' as const, groupKeys: [] as string[] },
  { key: 'admin', summaryKey: 'adminLogin' as const, groupKeys: [] as string[] },
  { key: 'reports', summaryKey: 'reports' as const, groupKeys: ['audit_berichte'] },
  { key: 'cash', summaryKey: 'cashShift' as const, groupKeys: ['kassenverwaltung'] },
  { key: 'customer', summaryKey: 'customer' as const, groupKeys: ['kunden_vorteile'] },
  { key: 'catalog', summaryKey: 'catalog' as const, groupKeys: ['sortiment_preise', 'bestellung_verkauf'] },
  { key: 'settings', summaryKey: 'settingsAdmin' as const, groupKeys: ['einstellungen', 'mitarbeiter'] },
] as const;

function groupCatalogByGroup(
  catalog: PermissionCatalogItemDto[]
): Map<string, PermissionCatalogItemDto[]> {
  const uiGroups = buildPermissionUiGroupsFromCatalog(catalog);
  const map = new Map<string, PermissionCatalogItemDto[]>();
  for (const g of uiGroups) {
    map.set(g.slug, g.items);
  }
  return map;
}

type Props = {
  open: boolean;
  onClose: () => void;
  roles: RoleWithPermissionsDto[] | undefined;
  catalog: PermissionCatalogItemDto[] | undefined;
  rolesLoading: boolean;
  catalogLoading: boolean;
  rolesError: boolean;
  catalogError: boolean;
  onRetry: () => void;
  canCreateRole: boolean;
  canDeleteRole: boolean;
  canEditRolePermissions: boolean;
  onCreateRole: () => void;
  onSavePermissions: (roleName: string, permissions: string[]) => Promise<void>;
  onDeleteRole: (roleName: string) => Promise<void>;
  saveLoading?: boolean;
  deleteLoading?: boolean;
  /** Full-page hub vs right drawer (default drawer). */
  presentation?: 'drawer' | 'page';
  /** Deep-link: filter permissions that affect this sidebar menuKey. */
  initialMenuFilter?: string | null;
  /** Deep-link: focus a permission key. */
  initialPermissionFocus?: string | null;
};

export function RoleManagementDrawer({
  open,
  onClose,
  roles = EMPTY_ROLES,
  catalog = EMPTY_CATALOG,
  rolesLoading,
  catalogLoading,
  rolesError,
  catalogError,
  onRetry,
  canCreateRole,
  canDeleteRole,
  canEditRolePermissions,
  onCreateRole,
  onSavePermissions,
  onDeleteRole,
  saveLoading = false,
  deleteLoading = false,
  presentation = 'drawer',
  initialMenuFilter = null,
  initialPermissionFocus = null,
}: Props) {
  const { modal, message } = useAntdApp();
  const queryClient = useQueryClient();

  const { t } = useI18n();
  const { hasPermission } = usePermissions();
  const canViewPermissionHistory = hasPermission(PERMISSIONS.AUDIT_VIEW);
  const canManageConfigBackups = hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
  const [impactOpen, setImpactOpen] = useState(false);
  const [backupLoading, setBackupLoading] = useState(false);
  const isActive = presentation === 'page' || open;

  const roleDisplayLabel = useCallback(
    (roleName: string) =>
      CANONICAL_ROLE_NAMES.has(roleName) ? t(`users.roles.displayNames.${roleName}`) : roleName,
    [t]
  );

  const formatUserCount = useCallback(
    (n: number) =>
      n === 1
        ? t('users.roleDrawer.userCountOne')
        : t('users.roleDrawer.userCountMany', { count: n }),
    [t]
  );

  const formatPermissionGroupCount = useCallback(
    (n: number) =>
      n === 1
        ? t('users.roleDrawer.permissionGroupCountOne')
        : t('users.roleDrawer.permissionGroupCountMany', { count: n }),
    [t]
  );

  const getUiAccessBadge = useCallback(
    (role: RoleWithPermissionsDto): { label: string; color: string } | null => {
      const cap = role.uiCapabilities;
      if (!cap) return null;
      if (cap.posLogin && cap.adminLogin)
        return { label: t('users.roleDrawer.badgePosAndAdmin'), color: 'blue' };
      if (cap.posLogin) return { label: t('users.roleDrawer.badgePosUi'), color: 'green' };
      if (cap.adminLogin) return { label: t('users.roleDrawer.badgeAdminUi'), color: 'geekblue' };
      return null;
    },
    [t]
  );

  const getGroupLabel = useCallback(
    (groupKey: string) => resolvePermissionGroupLabel(groupKey, t),
    [t]
  );

  const getRoleCapabilityHint = useCallback(
    (role: RoleWithPermissionsDto): string => {
      const cap = role.uiCapabilities;
      const groups = role.permissionGroups ?? [];
      const groupKeys = new Set(groups.map((g) => g.groupKey));
      const has = (key: string) => groupKeys.has(key);
      const pos = cap?.posLogin === true;
      const admin = cap?.adminLogin === true;
      if (pos && admin) return t('users.roleDrawer.capabilityHintBoth');
      if (pos && has('kassenverwaltung')) return t('users.roleDrawer.capabilityHintPosCash');
      if (pos) return t('users.roleDrawer.capabilityHintPosOnly');
      if (admin && has('audit_berichte')) return t('users.roleDrawer.capabilityHintAdminReports');
      if (admin && (has('mitarbeiter') || has('einstellungen') || has('system')))
        return t('users.roleDrawer.capabilityHintAdminFull');
      if (admin) return t('users.roleDrawer.capabilityHintAdminCatalog');
      if (has('audit_berichte')) return t('users.roleDrawer.summary.reports');
      if (has('kassenverwaltung')) return t('users.roleDrawer.summary.cashShift');
      if (groups.length > 0) return formatPermissionGroupCount(groups.length);
      return '';
    },
    [t, formatPermissionGroupCount]
  );

  const getSummaryValue = useCallback(
    (role: RoleWithPermissionsDto, cat: (typeof SUMMARY_CATEGORY_KEYS)[number]): string => {
      if (cat.key === 'pos')
        return role.uiCapabilities?.posLogin === true
          ? t('users.roleDrawer.loginYes')
          : t('users.roleDrawer.loginNo');
      if (cat.key === 'admin')
        return role.uiCapabilities?.adminLogin === true
          ? t('users.roleDrawer.loginYes')
          : t('users.roleDrawer.loginNo');
      const groups = role.permissionGroups ?? [];
      const hasAny = cat.groupKeys.some((gk) =>
        groups.some((pg) => pg.groupKey === gk && pg.permissions.length > 0)
      );
      return hasAny ? t('users.roleDrawer.loginYes') : t('users.roleDrawer.summaryNone');
    },
    [t]
  );

  const sortedRoles = useMemo(
    () =>
      [...roles].sort((a, b) => {
        const systemA = a.isSystemRole ? 0 : 1;
        const systemB = b.isSystemRole ? 0 : 1;
        if (systemA !== systemB) return systemA - systemB;
        return a.roleName.localeCompare(b.roleName, 'de');
      }),
    [roles]
  );

  const systemRolesList = useMemo(
    () => sortedRoles.filter((r) => r.isSystemRole || r.isImmutable),
    [sortedRoles]
  );
  const customRolesList = useMemo(
    () => sortedRoles.filter((r) => !(r.isSystemRole || r.isImmutable)),
    [sortedRoles]
  );

  const [selectedRoleName, setSelectedRoleName] = useState<string | null>(null);
  const [draftPermissions, setDraftPermissions] = useState<Set<string>>(() => new Set());
  const [presetSelectValue, setPresetSelectValue] = useState<string | null>(null);
  const [compareRoleName, setCompareRoleName] = useState<string | null>(null);
  const [visualDiffEnabled, setVisualDiffEnabled] = useState(true);
  const [showOnlyDifferences, setShowOnlyDifferences] = useState(false);
  const [showAffectedMenus, setShowAffectedMenus] = useState(true);
  const [menuFilter, setMenuFilter] = useState<string | 'all'>('all');
  const [pinnedMenus, setPinnedMenus] = useState<string[]>(() => loadPinnedMenuFilters());
  const [editorTab, setEditorTab] = useState<'permissions' | 'menuPreview' | 'history'>(
    'permissions'
  );
  const [permissionSearch, setPermissionSearch] = useState('');
  const debouncedPermissionSearch = useDebounce(permissionSearch, PERMISSION_SEARCH_DEBOUNCE_MS);
  const [appliedPermissionSearch, setAppliedPermissionSearch] = useState<string | null>(null);
  const effectivePermissionSearch =
    appliedPermissionSearch !== null ? appliedPermissionSearch : debouncedPermissionSearch;
  const [groupFilter, setGroupFilter] = useState<string | 'all'>('all');
  const [assignmentFilter, setAssignmentFilter] = useState<'all' | 'allowed' | 'denied'>('all');
  const [activeGroupKeys, setActiveGroupKeys] = useState<string[]>([]);
  const [focusedPermissionKey, setFocusedPermissionKey] = useState<string | null>(null);
  const [selectedPermissions, setSelectedPermissions] = useState<Set<string>>(() => new Set());
  const [guidedTourOpen, setGuidedTourOpen] = useState(false);

  // Single primitive key for role list identity (stable when names unchanged).
  const roleNamesKey = useMemo(() => {
    const sorted = [...roles].sort((a, b) => {
      const systemA = a.isSystemRole ? 0 : 1;
      const systemB = b.isSystemRole ? 0 : 1;
      if (systemA !== systemB) return systemA - systemB;
      return a.roleName.localeCompare(b.roleName, 'de');
    });
    return sorted.map((r) => r.roleName).join('\u0001');
  }, [roles]);

  // Permissions content key for selected role only (avoids effect depending on full roles array reference).
  const selectedRolePermissionsKey = useMemo(() => {
    if (!selectedRoleName) return '';
    const r = roles.find((rr) => rr.roleName === selectedRoleName);
    return r ? [...(r.permissions ?? [])].sort().join(',') : '';
  }, [roles, selectedRoleName]);

  // Derived selection + capabilities in one memo (reduces duplicate finds).
  const selectedRole = useMemo(
    () => (selectedRoleName ? roles.find((r) => r.roleName === selectedRoleName) : undefined),
    [roles, selectedRoleName]
  );
  const isSystemRole = selectedRole?.isSystemRole ?? false;
  // Backend: all system roles immutable → canEditPermissions false; custom uses DTO flag.
  const canEditRole = !isSystemRole && (selectedRole?.canEditPermissions ?? true);
  const selectedRoleCanDelete =
    !isSystemRole && (selectedRole?.canDelete ?? (selectedRole?.userCount ?? 0) === 0);

  // selectedRolePermissionsKey encodes permission list; avoid roles/selectedRole ref churn (React Query refetch).
  const savedPermissionsSet = useMemo(
    () => new Set(selectedRole?.permissions ?? []),
    // eslint-disable-next-line react-hooks/exhaustive-deps -- intentional: selectedRolePermissionsKey is the fingerprint
    [selectedRolePermissionsKey]
  );

  const dirty = useMemo(() => {
    if (!selectedRoleName) return false;
    if (draftPermissions.size !== savedPermissionsSet.size) return true;
    let dirtyFlag = false;
    draftPermissions.forEach((p) => {
      if (!savedPermissionsSet.has(p)) dirtyFlag = true;
    });
    if (dirtyFlag) return true;
    savedPermissionsSet.forEach((p) => {
      if (!draftPermissions.has(p)) dirtyFlag = true;
    });
    return dirtyFlag;
  }, [selectedRoleName, draftPermissions, savedPermissionsSet]);

  // Keep latest roles for handlers without putting `roles` in effect deps (avoids refetch reference churn → effect loops).
  const rolesRef = useRef(roles);
  rolesRef.current = roles;

  // --- Effect 1: selection only when open and list identity changes. No `roles` in deps — avoids infinite update depth when parent passes new array reference each render. ---
  useEffect(() => {
    if (!isActive) return;
    const names = roleNamesKey ? roleNamesKey.split('\u0001') : [];
    if (names.length === 0) {
      setSelectedRoleName((prev) => (prev === null ? prev : null));
      setDraftPermissions((prev) => (prev.size === 0 ? prev : new Set()));
      return;
    }
    const valid = selectedRoleName && names.includes(selectedRoleName);
    if (!valid) {
      const firstName = names[0]!;
      setSelectedRoleName(firstName);
      // Draft sync delegated to effect 2 when selectedRolePermissionsKey updates (derived from roles).
    }
  }, [isActive, roleNamesKey, selectedRoleName]);

  // --- Effect 2: sync draft from server. Deps exclude `roles` array — selectedRolePermissionsKey already encodes permission content; prevents loop when React Query returns new roles reference with same data. ---
  useEffect(() => {
    if (!open || !selectedRoleName) return;
    const role = rolesRef.current.find((r) => r.roleName === selectedRoleName);
    if (!role) return;
    const next = new Set(role.permissions ?? []);
    setDraftPermissions((prev) => (setsEqual(prev, next) ? prev : next));
  }, [isActive, selectedRoleName, selectedRolePermissionsKey]);

  // Clear permission search when switching roles or closing the panel.
  useEffect(() => {
    setPermissionSearch('');
    setAppliedPermissionSearch(null);
    setGroupFilter('all');
    setAssignmentFilter('all');
    setFocusedPermissionKey(null);
  }, [selectedRoleName, isActive]);

  // Apply deep-link menu / permission focus once when panel becomes active.
  const deepLinkAppliedRef = useRef(false);
  useEffect(() => {
    if (!isActive) {
      deepLinkAppliedRef.current = false;
      return;
    }
    if (deepLinkAppliedRef.current) return;
    if (initialMenuFilter) setMenuFilter(initialMenuFilter);
    if (initialPermissionFocus) setFocusedPermissionKey(initialPermissionFocus);
    deepLinkAppliedRef.current = true;
  }, [isActive, initialMenuFilter, initialPermissionFocus]);

  // Primitive fingerprint for catalog keys so effect does not re-run every render (Set reference unstable).
  const catalogKeysFingerprint = useMemo(() => {
    if (catalog.length === 0) return '';
    return [...catalog.map((c) => c.key)].sort().join(',');
  }, [catalog]);

  useEffect(() => {
    if (!open || catalogKeysFingerprint.length === 0) return;
    const keys = catalogKeysFingerprint.split(',');
    validateCatalogAlignment(keys, { warnUnknown: true });
  }, [isActive, catalogKeysFingerprint]);

  const groupedCatalog = useMemo(() => groupCatalogByGroup(catalog), [catalog]);
  // Set from fingerprint avoids depending on catalog array reference (React Query refetch).
  const catalogKeySet = useMemo(() => {
    if (!catalogKeysFingerprint) return new Set<string>();
    return new Set(catalogKeysFingerprint.split(','));
  }, [catalogKeysFingerprint]);

  const groupedCatalogEntries = useMemo(() => {
    const entries = Array.from(groupedCatalog.entries());
    return entries.sort(([slugA], [slugB]) => {
      const labelA = resolvePermissionGroupLabel(slugA, t);
      const labelB = resolvePermissionGroupLabel(slugB, t);
      return comparePermissionGroupSlugs(slugA, slugB, labelA, labelB);
    });
  }, [groupedCatalog, t]);

  const permissionSearchEntries = useMemo(
    () => buildPermissionSearchEntries(catalog),
    [catalog]
  );

  const matchedPermissionKeys = useMemo(() => {
    const matched = searchPermissions(permissionSearchEntries, effectivePermissionSearch, 'all');
    return new Set(matched.map((m) => m.key));
  }, [permissionSearchEntries, effectivePermissionSearch]);

  const groupFilterOptions = useMemo(
    () =>
      groupedCatalogEntries.map(([slug, items]) => ({
        value: slug,
        label: `${resolvePermissionGroupLabel(slug, t)} (${items.length})`,
      })),
    [groupedCatalogEntries, t]
  );

  const menuFilterOptions = useMemo(() => {
    const all = listSidebarMenuFilterOptions().map((opt) => ({
      value: opt.value,
      label: t(opt.labelKey),
      pinned: pinnedMenus.includes(opt.value),
    }));
    all.sort((a, b) => {
      if (a.pinned !== b.pinned) return a.pinned ? -1 : 1;
      return a.label.localeCompare(b.label);
    });
    return all.map(({ value, label, pinned }) => ({
      value,
      label: pinned ? `📌 ${label}` : label,
    }));
  }, [pinnedMenus, t]);

  const selectedMenuFilterLabel = useMemo(() => {
    if (menuFilter === 'all') return null;
    const opt = listSidebarMenuFilterOptions().find((o) => o.value === menuFilter);
    return opt ? t(opt.labelKey) : menuFilter;
  }, [menuFilter, t]);

  const compareRoleOptions = useMemo(
    () =>
      sortedRoles
        .filter((r) => r.roleName !== 'SuperAdmin')
        .map((r) => ({
          value: r.roleName,
          label: roleDisplayLabel(r.roleName),
        })),
    [sortedRoles, roleDisplayLabel]
  );

  const compareRolePermissions = useMemo(() => {
    if (!compareRoleName) return null;
    const role = roles.find((r) => r.roleName === compareRoleName);
    return role?.permissions ?? [];
  }, [compareRoleName, roles]);

  const rolePermissionDiff = useMemo(() => {
    if (!compareRoleName || !compareRolePermissions) return null;
    return comparePermissionSets(draftPermissions, compareRolePermissions);
  }, [compareRoleName, compareRolePermissions, draftPermissions]);

  const handleApplyFromCompareRole = useCallback(() => {
    if (!canEditRole || !compareRolePermissions || !compareRoleName) return;
    modal.confirm({
      title: t('users.roleDrawer.applyFromRoleConfirmTitle'),
      content: t('users.roleDrawer.applyFromRoleConfirmBody', {
        role: roleDisplayLabel(compareRoleName),
      }),
      okText: t('users.roleDrawer.applyFromRole'),
      onOk: () => {
        const keysInCatalog = compareRolePermissions.filter((k) => catalogKeySet.has(k));
        setDraftPermissions(new Set(keysInCatalog));
        setSelectedPermissions(new Set());
      },
    });
  }, [
    canEditRole,
    compareRolePermissions,
    compareRoleName,
    catalogKeySet,
    modal,
    t,
    roleDisplayLabel,
  ]);

  const menuMatchKeys = useMemo(() => {
    if (menuFilter === 'all') return null;
    const keys = new Set<string>();
    for (const item of catalog) {
      const affects = getMenuItemsAffectedByPermission(item.key).some(
        (m) => m.path === menuFilter
      );
      const isGate = getPermissionsAffectingMenu(menuFilter).some((r) => r.key === item.key);
      if (affects || isGate) keys.add(item.key);
    }
    return keys;
  }, [catalog, menuFilter]);

  const filteredGroupedCatalogEntries = useMemo(() => {
    const q = effectivePermissionSearch.trim();
    const diffMap = rolePermissionDiff?.byPermission;
    return groupedCatalogEntries
      .filter(([slug]) => groupFilter === 'all' || slug === groupFilter)
      .map(([slug, items]) => {
        let filtered = q ? items.filter((item) => matchedPermissionKeys.has(item.key)) : items;
        if (assignmentFilter === 'allowed') {
          filtered = filtered.filter((item) => draftPermissions.has(item.key));
        } else if (assignmentFilter === 'denied') {
          filtered = filtered.filter((item) => !draftPermissions.has(item.key));
        }
        if (showOnlyDifferences && diffMap) {
          filtered = filtered.filter((item) => {
            const kind = diffMap.get(item.key);
            return kind === 'onlyBase' || kind === 'onlyCompare';
          });
        }
        return [slug, filtered] as const;
      })
      .filter(([, items]) => {
        if (items.length === 0) return false;
        if (menuMatchKeys) return items.some((i) => menuMatchKeys.has(i.key));
        return true;
      });
  }, [
    groupedCatalogEntries,
    groupFilter,
    effectivePermissionSearch,
    matchedPermissionKeys,
    assignmentFilter,
    draftPermissions,
    showOnlyDifferences,
    rolePermissionDiff,
    menuMatchKeys,
  ]);

  const permissionTotalCount = catalog.length;
  const permissionVisibleCount = useMemo(
    () => filteredGroupedCatalogEntries.reduce((sum, [, items]) => sum + items.length, 0),
    [filteredGroupedCatalogEntries]
  );

  const menuFilterVisibleCount = useMemo(() => {
    if (!menuMatchKeys) return permissionVisibleCount;
    return filteredGroupedCatalogEntries.reduce(
      (sum, [, items]) => sum + items.filter((i) => menuMatchKeys.has(i.key)).length,
      0
    );
  }, [menuMatchKeys, filteredGroupedCatalogEntries, permissionVisibleCount]);

  const menuFilterTotalForMenu = menuMatchKeys?.size ?? 0;

  const visiblePermissionKeys = useMemo(
    () => filteredGroupedCatalogEntries.flatMap(([, items]) => items.map((i) => i.key)),
    [filteredGroupedCatalogEntries]
  );

  useEffect(() => {
    const keys = filteredGroupedCatalogEntries.map(([slug]) => slug);
    setActiveGroupKeys((prev) => {
      if (prev.length === 0) return keys;
      const still = prev.filter((k) => keys.includes(k));
      return still.length > 0 ? still : keys;
    });
  }, [filteredGroupedCatalogEntries]);

  const handlePermissionSearchChange = useCallback((next: string) => {
    setPermissionSearch(next);
    setAppliedPermissionSearch(null);
  }, []);

  const handlePermissionSearchApply = useCallback((value: string) => {
    setPermissionSearch(value);
    setAppliedPermissionSearch(value);
  }, []);

  const handleQuickFilter = useCallback((preset: PermissionQuickFilterPreset) => {
    switch (preset) {
      case 'denied':
        setAssignmentFilter('denied');
        break;
      case 'allowed':
        setAssignmentFilter('allowed');
        break;
      case 'allGroups':
        setGroupFilter('all');
        break;
      case 'reset':
        setPermissionSearch('');
        setAppliedPermissionSearch(null);
        setGroupFilter('all');
        setAssignmentFilter('all');
        break;
      default:
        break;
    }
  }, []);

  const jumpToPermission = useCallback(
    (key: string) => {
      const item = catalog.find((c) => c.key === key);
      if (!item) return;
      const slug = permissionCatalogGroupToSlug(item.group?.trim() || 'Other');
      setGroupFilter('all');
      setAssignmentFilter('all');
      setPermissionSearch('');
      setAppliedPermissionSearch(null);
      setFocusedPermissionKey(key);
      setActiveGroupKeys((prev) => (prev.includes(slug) ? prev : [...prev, slug]));
    },
    [catalog]
  );

  const jumpToGroup = useCallback((slug: string) => {
    setGroupFilter(slug);
    setActiveGroupKeys([slug]);
  }, []);

  const roleStatusOptions = useMemo(
    () => [
      { value: 'all' as PermissionStatusFilter, label: t('users.permissionsModal.filterStatusAll') },
      {
        value: 'allowed' as PermissionStatusFilter,
        label: t('users.permissionsModal.filterStatusAllowed'),
      },
      {
        value: 'denied' as PermissionStatusFilter,
        label: t('users.permissionsModal.filterStatusDenied'),
      },
    ],
    [t]
  );

  const permissionCommandItems = useMemo((): PermissionCommandItem[] => {
    const items: PermissionCommandItem[] = [
      {
        id: 'action-expand',
        label: t('users.permissionsModal.expandAllGroups'),
        group: 'action',
        keywords: ['expand', 'öffnen', 'alle'],
        run: () => setActiveGroupKeys(groupedCatalogEntries.map(([s]) => s)),
      },
      {
        id: 'action-collapse',
        label: t('users.permissionsModal.collapseAllGroups'),
        group: 'action',
        keywords: ['collapse', 'schließen'],
        run: () => setActiveGroupKeys([]),
      },
      {
        id: 'action-denied',
        label: t('users.permissionToolbar.quickFilterDenied'),
        group: 'action',
        keywords: ['denied', 'abgelehnt'],
        run: () => handleQuickFilter('denied'),
      },
      {
        id: 'action-allowed',
        label: t('users.permissionToolbar.quickFilterAllowed'),
        group: 'action',
        keywords: ['allowed', 'erlaubt'],
        run: () => handleQuickFilter('allowed'),
      },
      {
        id: 'action-reset',
        label: t('users.permissionToolbar.quickFilterReset'),
        group: 'action',
        keywords: ['reset', 'zurücksetzen'],
        run: () => handleQuickFilter('reset'),
      },
    ];

    for (const [slug] of groupedCatalogEntries) {
      const label = resolvePermissionGroupLabel(slug, t);
      items.push({
        id: `group-${slug}`,
        label,
        description: t('users.permissionToolbar.paletteJumpGroup'),
        group: 'group',
        keywords: [slug, label],
        run: () => jumpToGroup(slug),
      });
    }

    for (const item of catalog) {
      const label = resolvePermissionDisplayLabel(item.key, t);
      items.push({
        id: `perm-${item.key}`,
        label,
        description: item.key,
        group: 'permission',
        keywords: [item.key, item.resource, item.action, label],
        run: () => jumpToPermission(item.key),
      });
    }

    return items;
  }, [t, groupedCatalogEntries, catalog, handleQuickFilter, jumpToGroup, jumpToPermission]);

  const presetOptions = useMemo(
    () => ROLE_PRESETS.map((p) => ({ value: p.id, label: p.label })),
    []
  );

  const handleApplyPreset = useCallback(
    (preset: RolePreset) => {
      if (!canEditRole) return;
      const keysInCatalog = getPresetKeysInCatalog(preset, catalogKeySet);
      setDraftPermissions(new Set(keysInCatalog));
      setPresetSelectValue(null);
    },
    [canEditRole, catalogKeySet]
  );

  const handleCommonTask = useCallback(
    (taskId: PermissionCommonTaskId) => {
      if (!canEditRole) return;
      if (taskId === 'cashier-new-branch') {
        const preset = findRolePresetById('kasa-operasyon');
        if (preset) handleApplyPreset(preset);
        return;
      }
      if (taskId === 'copy-manager') {
        const manager = rolesRef.current.find((r) => r.roleName === 'Manager');
        if (!manager) {
          modal.warning({
            title: t('users.permissionOnboarding.taskCopyManagerMissingTitle'),
            content: t('users.permissionOnboarding.taskCopyManagerMissingBody'),
          });
          return;
        }
        setCompareRoleName('Manager');
        const keysInCatalog = (manager.permissions ?? []).filter((k) => catalogKeySet.has(k));
        setDraftPermissions(new Set(keysInCatalog));
        setSelectedPermissions(new Set());
        return;
      }
      if (taskId === 'prepare-audit') {
        const preset = findRolePresetById('muhasebe');
        if (preset) handleApplyPreset(preset);
      }
    },
    [canEditRole, handleApplyPreset, catalogKeySet, modal, t]
  );

  const selectedPreset = useMemo(
    () => findRolePresetById(presetSelectValue),
    [presetSelectValue]
  );

  const syncDraftToRole = useCallback((roleName: string) => {
    const next = rolesRef.current.find((r) => r.roleName === roleName);
    setDraftPermissions(new Set(next?.permissions ?? []));
  }, []);

  const handleSelectRole = (roleName: string) => {
    if (roleName === selectedRoleName) return;
    if (dirty) {
      modal.confirm({
        title: t('users.roleDrawer.confirmCloseWithDirty'),
        okText: t('users.roleDrawer.confirmDiscardOk'),
        cancelText: t('users.roleDrawer.confirmDiscardCancel'),
        onOk: () => {
          setSelectedRoleName(roleName);
          syncDraftToRole(roleName);
          setPresetSelectValue(null);
          setCompareRoleName(null);
          setSelectedPermissions(new Set());
          setEditorTab('permissions');
        },
      });
      return;
    }
    setSelectedRoleName(roleName);
    syncDraftToRole(roleName);
    setPresetSelectValue(null);
    setCompareRoleName(null);
    setSelectedPermissions(new Set());
    setEditorTab('permissions');
  };

  const handleClose = () => {
    if (dirty) {
      modal.confirm({
        title: t('users.roleDrawer.confirmCloseWithDirty'),
        okText: t('users.roleDrawer.confirmDiscardOk'),
        cancelText: t('users.roleDrawer.confirmDiscardCancel'),
        onOk: () => onClose(),
      });
      return;
    }
    onClose();
  };

  const handleTogglePermission = (key: string, checked: boolean) => {
    if (!canEditRole) return;
    setDraftPermissions((prev) => {
      const next = new Set(prev);
      if (checked) next.add(key);
      else next.delete(key);
      return next;
    });
  };

  const handleBulkGroup = useCallback(
    (items: PermissionCatalogItemDto[], checked: boolean) => {
      if (!canEditRole) return;
      setDraftPermissions((prev) => {
        const next = new Set(prev);
        for (const item of items) {
          if (checked) next.add(item.key);
          else next.delete(item.key);
        }
        return next;
      });
    },
    [canEditRole]
  );

  const toggleSelectPermission = useCallback((permission: string, selected: boolean) => {
    setSelectedPermissions((prev) => {
      const next = new Set(prev);
      if (selected) next.add(permission);
      else next.delete(permission);
      return next;
    });
  }, []);

  const setGroupSelection = useCallback((keys: string[], selected: boolean) => {
    setSelectedPermissions((prev) => {
      const next = new Set(prev);
      for (const key of keys) {
        if (selected) next.add(key);
        else next.delete(key);
      }
      return next;
    });
  }, []);

  const selectedVisibleKeys = useMemo(
    () => visiblePermissionKeys.filter((k) => selectedPermissions.has(k)),
    [visiblePermissionKeys, selectedPermissions]
  );

  useEffect(() => {
    setSelectedPermissions((prev) => {
      if (prev.size === 0) return prev;
      const visible = new Set(visiblePermissionKeys);
      let changed = false;
      const next = new Set<string>();
      for (const key of prev) {
        if (visible.has(key)) next.add(key);
        else changed = true;
      }
      return changed ? next : prev;
    });
  }, [visiblePermissionKeys]);

  const applyBatchAllow = useCallback(() => {
    if (!canEditRole || selectedVisibleKeys.length === 0) return;
    setDraftPermissions((prev) => {
      const next = new Set(prev);
      for (const key of selectedVisibleKeys) next.add(key);
      return next;
    });
    setSelectedPermissions(new Set());
  }, [canEditRole, selectedVisibleKeys]);

  const applyBatchDeny = useCallback(() => {
    if (!canEditRole || selectedVisibleKeys.length === 0) return;
    setDraftPermissions((prev) => {
      const next = new Set(prev);
      for (const key of selectedVisibleKeys) next.delete(key);
      return next;
    });
    setSelectedPermissions(new Set());
  }, [canEditRole, selectedVisibleKeys]);

  const applyBatchResetRole = useCallback(() => {
    if (!canEditRole || selectedVisibleKeys.length === 0) return;
    setDraftPermissions((prev) => {
      const next = new Set(prev);
      for (const key of selectedVisibleKeys) {
        if (savedPermissionsSet.has(key)) next.add(key);
        else next.delete(key);
      }
      return next;
    });
    setSelectedPermissions(new Set());
  }, [canEditRole, selectedVisibleKeys, savedPermissionsSet]);

  useEffect(() => {
    if (!isActive || !selectedRoleName || !canEditRole) return;
    const onKeyDown = (event: KeyboardEvent) => {
      const target = event.target as HTMLElement | null;
      if (
        target &&
        (target.tagName === 'INPUT' ||
          target.tagName === 'TEXTAREA' ||
          target.tagName === 'SELECT' ||
          target.isContentEditable)
      ) {
        return;
      }
      if (visiblePermissionKeys.length === 0) return;

      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'a') {
        event.preventDefault();
        setSelectedPermissions(new Set(visiblePermissionKeys));
        return;
      }
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'd') {
        event.preventDefault();
        setSelectedPermissions(new Set());
        return;
      }

      const currentIndex = focusedPermissionKey
        ? visiblePermissionKeys.indexOf(focusedPermissionKey)
        : -1;
      if (event.key === 'ArrowDown') {
        event.preventDefault();
        setFocusedPermissionKey(
          visiblePermissionKeys[Math.min(currentIndex + 1, visiblePermissionKeys.length - 1)]!
        );
        return;
      }
      if (event.key === 'ArrowUp') {
        event.preventDefault();
        setFocusedPermissionKey(visiblePermissionKeys[Math.max(currentIndex - 1, 0)]!);
        return;
      }
      if ((event.key === ' ' || event.key === 'Enter') && focusedPermissionKey) {
        event.preventDefault();
        handleTogglePermission(focusedPermissionKey, !draftPermissions.has(focusedPermissionKey));
      }
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [
    isActive,
    selectedRoleName,
    canEditRole,
    visiblePermissionKeys,
    focusedPermissionKey,
    draftPermissions,
  ]);

  const handleSave = async () => {
    if (!selectedRoleName || !canEditRole || !dirty) return;
    await onSavePermissions(selectedRoleName, Array.from(draftPermissions));
  };

  const handleQuickBackup = async () => {
    setBackupLoading(true);
    try {
      await createPermissionConfigBackup({
        name: t('users.roleDrawer.quickBackupName'),
        note: selectedRoleName
          ? t('users.roleDrawer.quickBackupNote', { role: selectedRoleName })
          : null,
      });
      message.success(t('users.roleDrawer.quickBackupSuccess'));
    } catch {
      message.error(t('users.roleDrawer.quickBackupError'));
    } finally {
      setBackupLoading(false);
    }
  };

  const handleDelete = () => {
    if (!selectedRoleName || !selectedRoleCanDelete) return;
    if (selectedRole && selectedRole.userCount > 0) {
      modal.warning({
        title: t('users.roleDrawer.roleHasUsers'),
        content: t('users.roleDrawer.roleDeleteBlockedReassignFirst'),
      });
      return;
    }
    modal.confirm({
      title: t('users.roleDrawer.deleteRole'),
      content: t('users.roleDrawer.deleteRoleConfirmBody', { roleName: selectedRoleName }),
      okText: t('users.roleDrawer.deleteRole'),
      okButtonProps: { danger: true },
      onOk: async () => {
        await onDeleteRole(selectedRoleName);
        const remaining = sortedRoles.filter((r) => r.roleName !== selectedRoleName);
        const first = remaining[0];
        setSelectedRoleName(first?.roleName ?? null);
        setDraftPermissions(new Set(first?.permissions ?? []));
      },
    });
  };

  const deleteButtonTooltip = !selectedRoleName
    ? undefined
    : isSystemRole
      ? t('users.roleDrawer.systemRoleProtectedNoDelete')
      : (selectedRole?.userCount ?? 0) > 0
        ? t('users.roleDrawer.roleDeleteBlockedReassignFirst')
        : undefined;

  const loading = rolesLoading || catalogLoading;
  const error = rolesError || catalogError;

  const actionFooter = (
    <Space wrap style={{ width: '100%', justifyContent: 'flex-end' }}>
      {canManageConfigBackups ? (
        <Button loading={backupLoading} onClick={() => void handleQuickBackup()}>
          {t('users.roleDrawer.quickBackup')}
        </Button>
      ) : null}
      {canCreateRole && (
        <Button icon={<PlusOutlined />} onClick={onCreateRole}>
          {t('users.roleDrawer.newRoleButton')}
        </Button>
      )}
      {canDeleteRole && (
        <Tooltip title={deleteButtonTooltip}>
          <span>
            <Button
              danger
              icon={<DeleteOutlined />}
              onClick={handleDelete}
              disabled={!selectedRoleCanDelete || !selectedRoleName}
              loading={deleteLoading}
            >
              {t('users.roleDrawer.deleteRole')}
            </Button>
          </span>
        </Tooltip>
      )}
      {canEditRolePermissions && dirty && selectedRoleName && canEditRole ? (
        <Button icon={<ThunderboltOutlined />} onClick={() => setImpactOpen(true)}>
          {t('users.roleDrawer.impactAnalysis')}
        </Button>
      ) : null}
      {canEditRolePermissions && (
        <Button
          type="primary"
          icon={<SaveOutlined />}
          onClick={handleSave}
          disabled={!dirty || !canEditRole || !selectedRoleName}
          loading={saveLoading}
          data-permission-tour="save"
        >
          {t('users.roleDrawer.savePermissions')}
        </Button>
      )}
    </Space>
  );

  const panelBody = (
    <>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 16 }}>
        {t('users.roleDrawer.description')}
      </Typography.Paragraph>

      {error && (
        <Alert
          type="error"
          title={t('users.list.errorLoad')}
          action={
            <Button size="small" onClick={onRetry}>
              {t('users.list.retry')}
            </Button>
          }
          style={{ marginBottom: 16 }}
        />
      )}

      {loading ? (
        <div style={{ display: 'flex', justifyContent: 'center', padding: 48 }}>
          <Spin spinning description={t('users.roleDrawer.loadingTip')}>
            <div style={{ minHeight: 80 }} />
          </Spin>
        </div>
      ) : (
        <div style={{ display: 'flex', gap: 24, minHeight: presentation === 'page' ? 520 : 400 }}>
          <div
            style={{
              width: 220,
              flexShrink: 0,
              borderRight: '1px solid #f0f0f0',
              paddingRight: 16,
            }}
          >
            <Typography.Text strong>{t('users.list.columnRole')}</Typography.Text>
            {sortedRoles.length === 0 ? (
              <Empty
                image={Empty.PRESENTED_IMAGE_SIMPLE}
                description={t('users.roleDrawer.noRoleSelectedEmpty')}
                style={{ marginTop: 8 }}
              />
            ) : (
              <div style={{ marginTop: 8 }}>
                {systemRolesList.length > 0 && (
                  <>
                    <Typography.Text
                      type="secondary"
                      style={{ fontSize: 11, display: 'block', marginBottom: 4 }}
                    >
                      {t('users.roleDrawer.systemRolesSection')}
                    </Typography.Text>
                    <Typography.Text
                      type="secondary"
                      style={{ fontSize: 10, display: 'block', marginBottom: 6, opacity: 0.85 }}
                    >
                      {t('users.roleDrawer.systemRolesSectionHint')}
                    </Typography.Text>
                    <List
                      size="small"
                      dataSource={systemRolesList}
                      renderItem={(r) => {
                        const uiBadge = getUiAccessBadge(r);
                        return (
                          <List.Item
                            key={r.roleName}
                            style={{
                              cursor: 'pointer',
                              background: r.roleName === selectedRoleName ? '#e6f7ff' : undefined,
                              borderRadius: 4,
                              padding: '4px 8px',
                            }}
                            onClick={() => handleSelectRole(r.roleName)}
                          >
                            <div style={{ width: '100%' }}>
                              <div
                                style={{
                                  display: 'flex',
                                  alignItems: 'center',
                                  gap: 6,
                                  flexWrap: 'wrap',
                                }}
                              >
                                <span>{roleDisplayLabel(r.roleName)}</span>
                                {uiBadge ? (
                                  <Tag color={uiBadge.color} style={{ margin: 0, fontSize: 11 }}>
                                    {uiBadge.label}
                                  </Tag>
                                ) : null}
                              </div>
                              <Typography.Text
                                type="secondary"
                                style={{ fontSize: 11, display: 'block', marginTop: 2 }}
                              >
                                {formatUserCount(r.userCount)}
                                {(r.permissionGroups?.length ?? 0) > 0 &&
                                  ` · ${formatPermissionGroupCount(r.permissionGroups!.length)}`}
                              </Typography.Text>
                              {getRoleCapabilityHint(r) && (
                                <Typography.Text
                                  type="secondary"
                                  style={{
                                    fontSize: 10,
                                    display: 'block',
                                    marginTop: 1,
                                    lineHeight: 1.3,
                                  }}
                                >
                                  {getRoleCapabilityHint(r)}
                                </Typography.Text>
                              )}
                            </div>
                          </List.Item>
                        );
                      }}
                    />
                  </>
                )}
                {customRolesList.length > 0 && (
                  <>
                    <Typography.Text
                      type="secondary"
                      style={{
                        fontSize: 11,
                        display: 'block',
                        marginTop: systemRolesList.length > 0 ? 14 : 0,
                        marginBottom: 4,
                      }}
                    >
                      {t('users.roleDrawer.customRolesSection')}
                    </Typography.Text>
                    <Typography.Text
                      type="secondary"
                      style={{ fontSize: 10, display: 'block', marginBottom: 6, opacity: 0.85 }}
                    >
                      {t('users.roleDrawer.customRolesSectionHint')}
                    </Typography.Text>
                    <List
                      size="small"
                      dataSource={customRolesList}
                      renderItem={(r) => {
                        const uiBadge = getUiAccessBadge(r);
                        return (
                          <List.Item
                            key={r.roleName}
                            style={{
                              cursor: 'pointer',
                              background: r.roleName === selectedRoleName ? '#e6f7ff' : undefined,
                              borderRadius: 4,
                              padding: '4px 8px',
                            }}
                            onClick={() => handleSelectRole(r.roleName)}
                          >
                            <div style={{ width: '100%' }}>
                              <div
                                style={{
                                  display: 'flex',
                                  alignItems: 'center',
                                  gap: 6,
                                  flexWrap: 'wrap',
                                }}
                              >
                                <span>{roleDisplayLabel(r.roleName)}</span>
                                {uiBadge ? (
                                  <Tag color={uiBadge.color} style={{ margin: 0, fontSize: 11 }}>
                                    {uiBadge.label}
                                  </Tag>
                                ) : (
                                  <Tag color="default" style={{ margin: 0, fontSize: 10 }}>
                                    {t('users.roleDrawer.badgeCustomRole')}
                                  </Tag>
                                )}
                              </div>
                              <Typography.Text
                                type="secondary"
                                style={{ fontSize: 11, display: 'block', marginTop: 2 }}
                              >
                                {formatUserCount(r.userCount)}
                                {(r.permissionGroups?.length ?? 0) > 0 &&
                                  ` · ${formatPermissionGroupCount(r.permissionGroups!.length)}`}
                              </Typography.Text>
                              {getRoleCapabilityHint(r) && (
                                <Typography.Text
                                  type="secondary"
                                  style={{
                                    fontSize: 10,
                                    display: 'block',
                                    marginTop: 1,
                                    lineHeight: 1.3,
                                  }}
                                >
                                  {getRoleCapabilityHint(r)}
                                </Typography.Text>
                              )}
                            </div>
                          </List.Item>
                        );
                      }}
                    />
                  </>
                )}
              </div>
            )}
          </div>

          <div style={{ flex: 1, minWidth: 0 }}>
            <div
              style={{
                display: 'flex',
                justifyContent: 'space-between',
                alignItems: 'center',
                flexWrap: 'wrap',
                gap: 8,
                marginBottom: 8,
              }}
            >
              <Typography.Text strong>
                {t('users.roleDrawer.permissionGroupsSection')}
              </Typography.Text>
              <Space size={8} wrap>
                {canViewPermissionHistory ? (
                  <Button
                    type={editorTab === 'history' ? 'primary' : 'default'}
                    icon={<HistoryOutlined />}
                    onClick={() => setEditorTab('history')}
                  >
                    {t('users.roleDrawer.openHistory')}
                  </Button>
                ) : null}
                {canViewPermissionHistory && presentation === 'page' ? (
                  <Link href="/admin/access/permission-history" prefetch={false}>
                    <Button type="link" style={{ paddingInline: 4 }}>
                      {t('users.roleDrawer.openHistoryPage')}
                    </Button>
                  </Link>
                ) : null}
                {canEditRolePermissions && selectedRoleName && canEditRole && (
                  <>
                  <MenuPermissionConsistencyButton
                    catalogKeys={[...catalogKeySet]}
                  />
                  <Select
                    placeholder={t('users.roleDrawer.presetPlaceholder')}
                    style={{ minWidth: 200 }}
                    value={presetSelectValue}
                    onChange={(presetId: string | null) => {
                      setPresetSelectValue(presetId);
                    }}
                    options={presetOptions}
                    allowClear
                    optionRender={(option) => {
                      const preset = findRolePresetById(String(option.value));
                      return (
                        <div>
                          <div>{option.label}</div>
                          {preset ? (
                            <div style={{ fontSize: 11, color: 'rgba(0,0,0,0.45)' }}>
                              {preset.description}
                            </div>
                          ) : null}
                        </div>
                      );
                    }}
                  />
                  <Button
                    type="default"
                    disabled={!selectedPreset}
                    onClick={() => {
                      if (selectedPreset) handleApplyPreset(selectedPreset);
                    }}
                  >
                    {t('users.roleDrawer.presetApply')}
                  </Button>
                  </>
                )}
              </Space>
            </div>
            {canEditRolePermissions && selectedRoleName && canEditRole && selectedPreset ? (
              <RolePresetPreviewCard
                preset={selectedPreset}
                catalogKeys={catalogKeySet}
                compact
              />
            ) : null}
            {selectedRoleName && canEditRolePermissions && canEditRole ? (
              <>
                <PermissionCommonTasksPanel
                  disabled={!canEditRole}
                  onTask={handleCommonTask}
                  onStartTour={() => setGuidedTourOpen(true)}
                />
                <PermissionHealthCheckAlert
                  granted={draftPermissions}
                  catalogSize={catalog.length}
                  catalogKeys={catalogKeySet}
                  allowPlatformCritical={isSystemRole || selectedRoleName === 'SuperAdmin'}
                  onApplySuggestedPreset={(presetId) => {
                    const preset = findRolePresetById(presetId);
                    if (preset) handleApplyPreset(preset);
                  }}
                />
                <PermissionChangesPanel
                  before={savedPermissionsSet}
                  after={draftPermissions}
                  visible={dirty}
                />
              </>
            ) : selectedRoleName ? (
              <Button
                type="link"
                size="small"
                style={{ marginBottom: 8, paddingInline: 0 }}
                onClick={() => setGuidedTourOpen(true)}
              >
                {t('users.permissionOnboarding.guidedTour')}
              </Button>
            ) : null}
            {selectedRoleName ? (
              <RolePackagesSection
                roleName={selectedRoleName}
                canEdit={Boolean(canEditRolePermissions && canManageConfigBackups)}
              />
            ) : null}
            {!selectedRoleName ? (
              <Empty
                image={Empty.PRESENTED_IMAGE_SIMPLE}
                description={
                  <span>
                    <div style={{ fontWeight: 500, marginBottom: 4 }}>
                      {t('users.roleDrawer.noRoleSelectedTitle')}
                    </div>
                    <div style={{ fontSize: 12, color: 'rgba(0,0,0,0.45)' }}>
                      {t('users.roleDrawer.noRoleSelectedDescription')}
                    </div>
                  </span>
                }
                style={{ marginTop: 24 }}
              />
            ) : (
              <Tabs
                activeKey={editorTab}
                onChange={(key) =>
                  setEditorTab(key as 'permissions' | 'menuPreview' | 'history')
                }
                items={[
                  {
                    key: 'permissions',
                    label: t('users.roleDrawer.tabPermissions'),
                    children: (
              <>
                {selectedRole && (
                  <>
                    <Typography.Title level={5} style={{ marginTop: 0, marginBottom: 8 }}>
                      {roleDisplayLabel(selectedRole.roleName)}
                    </Typography.Title>
                    <Typography.Paragraph
                      type="secondary"
                      style={{ fontSize: 12, marginBottom: 12 }}
                    >
                      {selectedRole.description ?? formatUserCount(selectedRole.userCount)}
                    </Typography.Paragraph>

                    {/* Compact summary row: POS/Admin login + capability areas */}
                    <div style={{ marginBottom: 16 }}>
                      <Row gutter={[8, 8]}>
                        {SUMMARY_CATEGORY_KEYS.map((cat) => (
                          <Col key={cat.key} xs={12} sm={8} md={6}>
                            <Card size="small" style={{ background: '#fafafa' }}>
                              <Typography.Text
                                type="secondary"
                                style={{ fontSize: 10, display: 'block' }}
                              >
                                {t(`users.roleDrawer.summary.${cat.summaryKey}`)}
                              </Typography.Text>
                              <Typography.Text style={{ fontSize: 12, fontWeight: 500 }}>
                                {getSummaryValue(selectedRole, cat)}
                              </Typography.Text>
                            </Card>
                          </Col>
                        ))}
                      </Row>
                    </div>

                    <Typography.Text strong style={{ display: 'block', marginBottom: 8 }}>
                      {t('users.roleDrawer.accessSection')}
                    </Typography.Text>
                    <div
                      style={{
                        marginBottom: 16,
                        padding: '8px 12px',
                        background: '#fafafa',
                        borderRadius: 6,
                      }}
                    >
                      <div
                        style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 4 }}
                      >
                        <Typography.Text type="secondary" style={{ minWidth: 100 }}>
                          {t('users.roleDrawer.posLoginLabel')}:
                        </Typography.Text>
                        <Typography.Text>
                          {selectedRole.uiCapabilities?.posLogin === true
                            ? t('users.roleDrawer.loginYes')
                            : t('users.roleDrawer.loginNo')}
                        </Typography.Text>
                      </div>
                      <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                        <Typography.Text type="secondary" style={{ minWidth: 100 }}>
                          {t('users.roleDrawer.adminLoginLabel')}:
                        </Typography.Text>
                        <Typography.Text>
                          {selectedRole.uiCapabilities?.adminLogin === true
                            ? t('users.roleDrawer.loginYes')
                            : t('users.roleDrawer.loginNo')}
                        </Typography.Text>
                      </div>
                    </div>

                    {selectedRole.permissionGroups && selectedRole.permissionGroups.length > 0 && (
                      <div style={{ marginBottom: 12, display: 'flex', flexWrap: 'wrap', gap: 4 }}>
                        {selectedRole.permissionGroups.map((pg) => (
                          <Tag key={pg.groupKey}>{getGroupLabel(pg.groupKey)}</Tag>
                        ))}
                      </div>
                    )}
                  </>
                )}

                {(isSystemRole || selectedRole?.isImmutable) && (
                  <Alert
                    type="info"
                    title={t('users.roleDrawer.badgeSystemRole')}
                    description={t('users.roleDrawer.systemRoleImmutableInfo')}
                    showIcon
                    style={{ marginBottom: 12 }}
                  />
                )}
                {selectedRole &&
                  !selectedRole.isSystemRole &&
                  !selectedRole.isImmutable &&
                  selectedRole.userCount > 0 && (
                    <Alert
                      type="info"
                      title={t('users.roleDrawer.roleHasUsers')}
                      description={t('users.roleDrawer.roleDeleteBlockedReassignFirst')}
                      showIcon
                      style={{ marginBottom: 12 }}
                    />
                  )}

                {/* Always render from full permission catalog; assigned state (draftPermissions) only drives checked. Never use selectedRole.permissionGroups for the list. */}
                {selectedRoleName ? (
                  <RolePermissionComparePanel
                    currentRoleName={selectedRoleName}
                    currentRoleLabel={roleDisplayLabel(selectedRoleName)}
                    basePermissions={draftPermissions}
                    compareRoleName={compareRoleName}
                    onCompareRoleChange={setCompareRoleName}
                    roleOptions={compareRoleOptions}
                    comparePermissions={compareRolePermissions}
                    visualDiffEnabled={visualDiffEnabled}
                    onVisualDiffEnabledChange={setVisualDiffEnabled}
                    showOnlyDifferences={showOnlyDifferences}
                    onShowOnlyDifferencesChange={setShowOnlyDifferences}
                    canApply={canEditRole}
                    onApplyFromRole={handleApplyFromCompareRole}
                  />
                ) : null}
                <PermissionCatalogToolbar
                  searchValue={permissionSearch}
                  onSearchChange={handlePermissionSearchChange}
                  onSearchApply={handlePermissionSearchApply}
                  searchPlaceholder={t('users.roleDrawer.permissionSearchPlaceholder')}
                  counterLabel={
                    menuFilter !== 'all' && selectedMenuFilterLabel
                      ? t('users.roleDrawer.menuFilterCount', {
                          visible: menuFilterVisibleCount,
                          total: menuFilterTotalForMenu || permissionVisibleCount,
                          menu: selectedMenuFilterLabel,
                        })
                      : t('users.roleDrawer.permissionSearchCounter', {
                          visible: permissionVisibleCount,
                          total: permissionTotalCount,
                        })
                  }
                  visibleCount={
                    menuFilter !== 'all' ? menuFilterVisibleCount : permissionVisibleCount
                  }
                  totalCount={
                    menuFilter !== 'all'
                      ? menuFilterTotalForMenu || permissionTotalCount
                      : permissionTotalCount
                  }
                  shortcutEnabled={isActive && !!selectedRoleName}
                  groupFilter={groupFilter}
                  onGroupFilterChange={setGroupFilter}
                  groupOptions={groupFilterOptions}
                  allGroupsLabel={t('users.permissionsModal.filterGroupAll')}
                  menuFilter={menuFilter}
                  onMenuFilterChange={setMenuFilter}
                  menuOptions={menuFilterOptions}
                  allMenusLabel={t('users.roleDrawer.menuFilterAll')}
                  menuFilterPlaceholder={t('users.roleDrawer.menuFilterPlaceholder')}
                  statusFilter={assignmentFilter}
                  onStatusFilterChange={(next) => {
                    if (next === 'individual') return;
                    setAssignmentFilter(next);
                  }}
                  statusOptions={roleStatusOptions}
                  onQuickFilter={handleQuickFilter}
                  quickFilterHidden={['individual']}
                  commandItems={permissionCommandItems}
                  expandAllLabel={t('users.permissionsModal.expandAllGroups')}
                  collapseAllLabel={t('users.permissionsModal.collapseAllGroups')}
                  onExpandAll={() =>
                    setActiveGroupKeys(filteredGroupedCatalogEntries.map(([s]) => s))
                  }
                  onCollapseAll={() => setActiveGroupKeys([])}
                  style={{ marginTop: 8, marginBottom: 8 }}
                />
                {menuFilter !== 'all' && selectedMenuFilterLabel ? (
                  <div
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      gap: 8,
                      flexWrap: 'wrap',
                      marginBottom: 8,
                    }}
                  >
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                      {t('users.roleDrawer.menuFilterBreadcrumbAll')}
                      {' › '}
                      {t('users.roleDrawer.menuFilterBreadcrumbMenu', {
                        menu: selectedMenuFilterLabel,
                        count: menuFilterVisibleCount,
                      })}
                    </Typography.Text>
                    <Button
                      size="small"
                      type="link"
                      icon={<PushpinOutlined />}
                      onClick={() =>
                        setPinnedMenus((prev) => togglePinnedMenuFilter(menuFilter, prev))
                      }
                    >
                      {pinnedMenus.includes(menuFilter)
                        ? t('users.roleDrawer.menuFilterUnpin')
                        : t('users.roleDrawer.menuFilterPin')}
                    </Button>
                    <Button size="small" type="link" onClick={() => setMenuFilter('all')}>
                      {t('users.roleDrawer.menuFilterAll')}
                    </Button>
                  </div>
                ) : null}
                <div
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    gap: 12,
                    marginBottom: 8,
                    flexWrap: 'wrap',
                  }}
                >
                  <Space size={8}>
                    <Switch
                      size="small"
                      checked={showAffectedMenus}
                      onChange={setShowAffectedMenus}
                      aria-label={t('users.roleDrawer.showAffectedMenus')}
                    />
                    <Typography.Text style={{ fontSize: 12 }}>
                      {t('users.roleDrawer.showAffectedMenus')}
                    </Typography.Text>
                  </Space>
                  {showAffectedMenus ? (
                    <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                      {t('users.roleDrawer.menuColumnHint')}
                    </Typography.Text>
                  ) : null}
                </div>
                {canEditRolePermissions && canEditRole ? (
                  <PermissionBatchToolbar
                    selectedCount={selectedVisibleKeys.length}
                    onAllow={applyBatchAllow}
                    onDeny={applyBatchDeny}
                    onResetToRoleDefault={applyBatchResetRole}
                    onClearSelection={() => setSelectedPermissions(new Set())}
                  />
                ) : null}
                <div style={{ maxHeight: 360, overflow: 'auto' }}>
                  {groupedCatalogEntries.length === 0 ? (
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                      {t('users.roleDrawer.noPermissionsInGroup')}
                    </Typography.Text>
                  ) : filteredGroupedCatalogEntries.length === 0 ? (
                    <Empty
                      image={Empty.PRESENTED_IMAGE_SIMPLE}
                      description={t('users.roleDrawer.permissionSearchEmpty')}
                      style={{ marginTop: 16 }}
                    />
                  ) : (
                    <Collapse
                      activeKey={activeGroupKeys}
                      onChange={(keys) =>
                        setActiveGroupKeys(
                          Array.isArray(keys) ? keys.map(String) : [String(keys)]
                        )
                      }
                      items={filteredGroupedCatalogEntries.map(([groupSlug, items]) => {
                        const keys = items.map((i) => i.key);
                        const selectedInGroup = keys.filter((k) =>
                          selectedPermissions.has(k)
                        ).length;
                        const allSelected = keys.length > 0 && selectedInGroup === keys.length;
                        const someSelected = selectedInGroup > 0 && !allSelected;
                        const groupDef = Object.prototype.hasOwnProperty.call(
                          PERMISSION_GROUPS,
                          groupSlug
                        )
                          ? PERMISSION_GROUPS[groupSlug as PermissionGroupKey]
                          : undefined;
                        const groupMenuChips = getMenuChipsForPermissionGroup(groupSlug);
                        const groupIcon = resolveSidebarIconElement(groupDef?.icon);
                        return {
                        key: groupSlug,
                        label: (
                          <span
                            className="permission-group-header"
                            style={{
                              fontWeight: 500,
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: 8,
                              flexWrap: 'wrap',
                              maxWidth: '100%',
                            }}
                          >
                            {canEditRole ? (
                              <span
                                onClick={(e) => e.stopPropagation()}
                                onKeyDown={(e) => e.stopPropagation()}
                              >
                                <Checkbox
                                  checked={allSelected}
                                  indeterminate={someSelected}
                                  aria-label={t('users.permissionsModal.selectGroup')}
                                  onChange={(e) => setGroupSelection(keys, e.target.checked)}
                                />
                              </span>
                            ) : null}
                            {groupIcon ? (
                              <span
                                className="group-icon"
                                style={{ display: 'inline-flex', fontSize: 15, color: 'rgba(0,0,0,0.65)' }}
                                aria-hidden
                              >
                                {groupIcon}
                              </span>
                            ) : null}
                            <span className="group-label">
                              <HighlightedText
                                text={`${resolvePermissionGroupLabel(groupSlug, t)} (${items.length})`}
                                query={effectivePermissionSearch}
                              />
                            </span>
                            {showAffectedMenus && groupMenuChips.length > 0 ? (
                              <span
                                className="group-menus"
                                onClick={(e) => e.stopPropagation()}
                                onKeyDown={(e) => e.stopPropagation()}
                                style={{ display: 'inline-flex', marginLeft: 4 }}
                              >
                                <PermissionMenuTags
                                  items={groupMenuChips}
                                  size="small"
                                  maxVisible={4}
                                  highlighted={
                                    focusedPermissionKey != null &&
                                    keys.includes(focusedPermissionKey)
                                  }
                                />
                              </span>
                            ) : null}
                          </span>
                        ),
                        extra: canEditRole ? (
                          <Space
                            size={4}
                            onClick={(e) => e.stopPropagation()}
                            onKeyDown={(e) => e.stopPropagation()}
                          >
                            <Button
                              size="small"
                              type="link"
                              onClick={() => setGroupSelection(keys, true)}
                            >
                              {t('users.permissionsModal.selectGroupAll')}
                            </Button>
                            <Button
                              size="small"
                              type="link"
                              onClick={() => setGroupSelection(keys, false)}
                            >
                              {t('users.permissionsModal.selectGroupNone')}
                            </Button>
                            <Button
                              size="small"
                              type="link"
                              onClick={() => handleBulkGroup(items, true)}
                            >
                              {t('users.permissionsModal.bulkAllowGroup')}
                            </Button>
                            <Button
                              size="small"
                              type="link"
                              danger
                              onClick={() => handleBulkGroup(items, false)}
                            >
                              {t('users.permissionsModal.bulkDenyGroup')}
                            </Button>
                          </Space>
                        ) : null,
                        children: (
                          <div role="list" style={{ margin: '0 -4px' }}>
                            {showAffectedMenus ? (
                              <div
                                style={{
                                  display: 'flex',
                                  alignItems: 'center',
                                  gap: 10,
                                  padding: '4px 10px 8px',
                                  fontSize: 11,
                                  fontWeight: 600,
                                  color: 'rgba(0,0,0,0.45)',
                                  borderBottom: '1px solid rgba(0,0,0,0.06)',
                                }}
                                aria-hidden
                              >
                                <span
                                  style={{
                                    width: canEditRole ? 72 : 48,
                                    flexShrink: 0,
                                  }}
                                />
                                <span style={{ flex: 1, minWidth: 0 }}>
                                  {t('users.roleDrawer.columnPermission')}
                                </span>
                                <span style={{ flex: '0 1 220px', minWidth: 120, maxWidth: 260 }}>
                                  {t('users.roleDrawer.columnMenu')}
                                </span>
                                <span style={{ minWidth: 160, textAlign: 'right' }}>
                                  {t('users.roleDrawer.columnStatus')}
                                </span>
                              </div>
                            ) : null}
                            {items.map((item) => {
                              const direct = draftPermissions.has(item.key);
                              const implied =
                                !direct && isPermissionImpliedOnly(item.key, draftPermissions);
                              const diffKind = rolePermissionDiff?.byPermission.get(item.key);
                              const highlight =
                                visualDiffEnabled && compareRoleName
                                  ? diffKindToHighlight(diffKind)
                                  : undefined;
                              const rowHighlight =
                                highlight === 'added' || highlight === 'removed'
                                  ? highlight
                                  : highlight === 'same' && showOnlyDifferences
                                    ? undefined
                                    : highlight;
                              return (
                              <PermissionListRow
                                key={item.key}
                                permission={item.key}
                                mode="checkbox"
                                checked={direct}
                                disabled={!canEditRole}
                                onChange={(checked) =>
                                  handleTogglePermission(item.key, checked)
                                }
                                searchQuery={effectivePermissionSearch}
                                focused={focusedPermissionKey === item.key}
                                onFocus={() => setFocusedPermissionKey(item.key)}
                                source={direct ? 'role' : implied ? 'implied' : 'none'}
                                heldPermissions={draftPermissions}
                                catalogDescription={item.description}
                                selectionEnabled={canEditRole}
                                selected={selectedPermissions.has(item.key)}
                                onSelectedChange={(selected) =>
                                  toggleSelectPermission(item.key, selected)
                                }
                                diffHighlight={rowHighlight}
                                showAffectedMenus={showAffectedMenus}
                                highlightAffectedMenus={
                                  (showAffectedMenus && focusedPermissionKey === item.key) ||
                                  (menuMatchKeys?.has(item.key) ?? false)
                                }
                                dimmed={Boolean(menuMatchKeys && !menuMatchKeys.has(item.key))}
                              />
                              );
                            })}
                          </div>
                        ),
                      };
                      })}
                    />
                  )}
                </div>
              </>
                    ),
                  },
                  {
                    key: 'menuPreview',
                    label: t('users.roleDrawer.tabMenuPreview'),
                    children: (
                      <RoleMenuPreviewPanel
                        roleName={selectedRoleName}
                        roleLabel={roleDisplayLabel(selectedRoleName)}
                        permissions={draftPermissions}
                      />
                    ),
                  },
                  {
                    key: 'history',
                    label: t('users.roleDrawer.tabHistory'),
                    children: selectedRoleName ? (
                      <RolePermissionHistoryTab
                        roleName={selectedRoleName}
                        roleId={selectedRole?.roleKey ?? null}
                        subjectLabel={roleDisplayLabel(selectedRoleName)}
                      />
                    ) : null,
                  },
                ]}
              />
            )}
          </div>
        </div>
      )}
    </>
  );

  if (presentation === 'page') {
    return (
      <Card>
        {panelBody}
        <div style={{ marginTop: 16, paddingTop: 16, borderTop: '1px solid #f0f0f0' }}>
          {actionFooter}
        </div>
        <PermissionGuidedTour open={guidedTourOpen} onClose={() => setGuidedTourOpen(false)} />
        {selectedRoleName ? (
          <RoleImpactAnalysisModal
            open={impactOpen}
            roleName={selectedRoleName}
            draftPermissions={draftPermissions}
            savedPermissions={savedPermissionsSet}
            onCancel={() => setImpactOpen(false)}
            saveLoading={saveLoading}
            onConfirmSave={async () => {
              await handleSave();
              setImpactOpen(false);
            }}
          />
        ) : null}
      </Card>
    );
  }

  return (
    <Drawer
      title={t('users.page.manageRoles')}
      placement="right"
      size={720}
      open={open}
      onClose={handleClose}
      destroyOnHidden
      footer={actionFooter}
    >
      {panelBody}
      <PermissionGuidedTour open={guidedTourOpen} onClose={() => setGuidedTourOpen(false)} />
      {selectedRoleName ? (
        <RoleImpactAnalysisModal
          open={impactOpen}
          roleName={selectedRoleName}
          draftPermissions={draftPermissions}
          savedPermissions={savedPermissionsSet}
          onCancel={() => setImpactOpen(false)}
          saveLoading={saveLoading}
          onConfirmSave={async () => {
            await handleSave();
            setImpactOpen(false);
          }}
        />
      ) : null}
    </Drawer>
  );
}
