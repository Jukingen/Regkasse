'use client';

import { CheckOutlined, CloseOutlined, DeleteOutlined } from '@ant-design/icons';
import {
  Alert,
  Button,
  Checkbox,
  Collapse,
  DatePicker,
  Empty,
  Form,
  Input,
  Modal,
  Space,
  Table,
  Tag,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { type Dayjs } from 'dayjs';
import { useCallback, useEffect, useMemo, useState } from 'react';

import { FormSkeleton } from '@/components/Skeleton';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import type { PermissionCatalogItemDto } from '@/features/users/api/roleManagementApi';
import type { UserPermissionOverrideDto } from '@/features/users/api/userPermissionOverridesApi';
import { PermissionAuditHistoryPanel } from '@/features/users/components/PermissionAuditHistoryPanel';
import { PermissionBatchToolbar } from '@/features/users/components/PermissionBatchToolbar';
import { PermissionChangesPanel } from '@/features/users/components/PermissionChangesPanel';
import { PermissionGuidedTour } from '@/features/users/components/PermissionGuidedTour';
import { PermissionHealthCheckAlert } from '@/features/users/components/PermissionHealthCheckAlert';
import { PermissionListRow } from '@/features/users/components/PermissionListRow';
import { usePermissionsCatalog } from '@/features/users/hooks/usePermissionsCatalog';
import {
  useUserEffectivePermissions,
  useUserPermissionOverrideMutations,
} from '@/features/users/hooks/useUserEffectivePermissions';
import {
  comparePermissionGroupSlugs,
  permissionCatalogGroupToSlug,
} from '@/features/users/utils/permissionCatalogGroup';
import { buildPermissionUiGroupsFromCatalog } from '@/shared/auth/permissionGroupRegistry';
import {
  resolvePermissionDisplayLabel,
  resolvePermissionGroupLabel,
} from '@/features/users/utils/permissionDisplayLabel';
import {
  PERMISSION_SEARCH_DEBOUNCE_MS,
  buildPermissionSearchEntries,
  searchPermissions,
} from '@/features/users/utils/permissionSearchIndex';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useDebounce } from '@/hooks/useDebounce';
import { useI18n } from '@/i18n';
import { formatDate } from '@/i18n/formatting';
import { DAYJS_DATE_FORMAT } from '@/lib/dateFormatter';
import { permissionImplied } from '@/shared/auth/permissionImplication';
import {
  computePermissionOverrideStatus,
  permissionOverrideStatusColor,
  type PermissionOverrideStatus,
} from '@/features/users/utils/permissionOverrideStatus';

import { HighlightedText } from './HighlightedText';
import {
  PermissionCatalogToolbar,
  type PermissionCommandItem,
  type PermissionQuickFilterPreset,
  type PermissionStatusFilter,
} from './PermissionCatalogToolbar';

export type UserPermissionsModalProps = {
  open: boolean;
  userId: string;
  userName: string;
  userRole?: string | null;
  onClose: () => void;
};

type OverrideFormValues = {
  reason?: string;
  validFrom?: Dayjs | null;
  expiresAt?: Dayjs | null;
};

type PendingBulk = {
  permissions: string[];
  /** allow/deny upsert overrides; reset deletes overrides (role default). */
  action: 'allow' | 'deny' | 'reset';
};

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

function permissionStatus(
  permission: string,
  effectivePermissions: string[],
  overridesByPermission: Map<string, UserPermissionOverrideDto>
): PermissionStatusFilter {
  if (overridesByPermission.has(permission)) return 'individual';
  return permissionImplied(permission, effectivePermissions) ? 'allowed' : 'denied';
}

export function UserPermissionsModal(props: UserPermissionsModalProps) {
  if (!props.open) return null;
  return <UserPermissionsModalContent {...props} />;
}

function UserPermissionsModalContent({
  open,
  userId,
  userName,
  userRole,
  onClose,
}: UserPermissionsModalProps) {
  const { message, modal } = useAntdApp();
  const { t } = useI18n();

  const [permissionSearch, setPermissionSearch] = useState('');
  const debouncedSearch = useDebounce(permissionSearch, PERMISSION_SEARCH_DEBOUNCE_MS);
  const [appliedSearch, setAppliedSearch] = useState<string | null>(null);
  const effectiveSearch = appliedSearch !== null ? appliedSearch : debouncedSearch;
  const [groupFilter, setGroupFilter] = useState<string | 'all'>('all');
  const [statusFilter, setStatusFilter] = useState<PermissionStatusFilter>('all');
  const [activeKeys, setActiveKeys] = useState<string[]>([]);
  const [focusedPermission, setFocusedPermission] = useState<string | null>(null);
  const [selectedPermissions, setSelectedPermissions] = useState<Set<string>>(() => new Set());
  const [pendingToggle, setPendingToggle] = useState<{
    permission: string;
    isGranted: boolean;
  } | null>(null);
  const [pendingBulk, setPendingBulk] = useState<PendingBulk | null>(null);
  const [guidedTourOpen, setGuidedTourOpen] = useState(false);

  useEffect(() => {
    if (!open) {
      setPermissionSearch('');
      setAppliedSearch(null);
      setGroupFilter('all');
      setStatusFilter('all');
      setFocusedPermission(null);
      setSelectedPermissions(new Set());
    }
  }, [open]);

  const targetIsSuperAdmin = isSuperAdmin(userRole ?? undefined);
  const readOnly = targetIsSuperAdmin;

  const effectiveQuery = useUserEffectivePermissions(userId, open);
  const catalogQuery = usePermissionsCatalog({ enabled: open });
  const { upsertMutation, deleteMutation } = useUserPermissionOverrideMutations(userId);

  const rolePermissions = effectiveQuery.data?.rolePermissions ?? [];
  const effectivePermissions = effectiveQuery.data?.effectivePermissions ?? [];
  const overrides = effectiveQuery.data?.overrides ?? [];
  const catalog = catalogQuery.data ?? [];

  const overridesByPermission = useMemo(() => {
    const map = new Map<string, UserPermissionOverrideDto>();
    for (const o of overrides) {
      if (!map.has(o.permission)) map.set(o.permission, o);
    }
    return map;
  }, [overrides]);

  const searchEntries = useMemo(() => buildPermissionSearchEntries(catalog), [catalog]);

  const matchedKeys = useMemo(() => {
    const matched = searchPermissions(searchEntries, effectiveSearch, 'all');
    return new Set(matched.map((m) => m.key));
  }, [searchEntries, effectiveSearch]);

  const groupedCatalog = useMemo(() => groupCatalogByGroup(catalog), [catalog]);

  const groupOptions = useMemo(() => {
    const slugs = Array.from(groupedCatalog.keys()).sort((a, b) =>
      comparePermissionGroupSlugs(
        a,
        b,
        resolvePermissionGroupLabel(a, t),
        resolvePermissionGroupLabel(b, t)
      )
    );
    return slugs.map((slug) => ({
      value: slug,
      label: `${resolvePermissionGroupLabel(slug, t)} (${groupedCatalog.get(slug)?.length ?? 0})`,
    }));
  }, [groupedCatalog, t]);

  const filteredGroupedCatalog = useMemo(() => {
    const q = effectiveSearch.trim();
    return Array.from(groupedCatalog.entries())
      .filter(([slug]) => groupFilter === 'all' || slug === groupFilter)
      .map(([slug, items]) => {
        const filtered = items.filter((item) => {
          if (q && !matchedKeys.has(item.key)) return false;
          if (statusFilter === 'all') return true;
          return (
            permissionStatus(item.key, effectivePermissions, overridesByPermission) === statusFilter
          );
        });
        return [slug, filtered] as const;
      })
      .filter(([, items]) => items.length > 0)
      .sort(([slugA], [slugB]) =>
        comparePermissionGroupSlugs(
          slugA,
          slugB,
          resolvePermissionGroupLabel(slugA, t),
          resolvePermissionGroupLabel(slugB, t)
        )
      );
  }, [
    groupedCatalog,
    groupFilter,
    effectiveSearch,
    matchedKeys,
    statusFilter,
    effectivePermissions,
    overridesByPermission,
    t,
  ]);

  const visiblePermissionKeys = useMemo(
    () => filteredGroupedCatalog.flatMap(([, items]) => items.map((i) => i.key)),
    [filteredGroupedCatalog]
  );

  const permissionVisibleCount = visiblePermissionKeys.length;

  // Keep collapse panels in sync with visible groups (expand newly filtered groups).
  useEffect(() => {
    const keys = filteredGroupedCatalog.map(([slug]) => slug);
    setActiveKeys((prev) => {
      if (prev.length === 0) return keys;
      const stillVisible = prev.filter((k) => keys.includes(k));
      return stillVisible.length > 0 ? stillVisible : keys;
    });
  }, [filteredGroupedCatalog]);

  const handleDeleteOverride = useCallback(
    async (record: UserPermissionOverrideDto) => {
      try {
        await deleteMutation.mutateAsync(record.id);
        message.success(t('users.permissionsModal.removeSuccess'));
      } catch {
        message.error(t('users.permissionsModal.updateError'));
      }
    },
    [deleteMutation, t, message]
  );

  const handleOverrideConfirm = useCallback(
    async (values: OverrideFormValues) => {
      if (!pendingToggle) return;
      try {
        await upsertMutation.mutateAsync({
          permission: pendingToggle.permission,
          isGranted: pendingToggle.isGranted,
          reason: values.reason?.trim() || t('users.permissionsModal.defaultReason'),
          validFrom: values.validFrom ? values.validFrom.toISOString() : null,
          expiresAt: values.expiresAt ? values.expiresAt.toISOString() : null,
        });
        message.success(t('users.permissionsModal.updateSuccess'));
        setPendingToggle(null);
      } catch {
        message.error(t('users.permissionsModal.updateError'));
      }
    },
    [pendingToggle, upsertMutation, t, message]
  );

  const handleBulkConfirm = useCallback(
    async (values: OverrideFormValues) => {
      if (!pendingBulk || pendingBulk.action === 'reset') return;
      try {
        const isGranted = pendingBulk.action === 'allow';
        await Promise.all(
          pendingBulk.permissions.map((permission) =>
            upsertMutation.mutateAsync({
              permission,
              isGranted,
              reason: values.reason?.trim() || t('users.permissionsModal.defaultReason'),
              validFrom: values.validFrom ? values.validFrom.toISOString() : null,
              expiresAt: values.expiresAt ? values.expiresAt.toISOString() : null,
            })
          )
        );
        message.success(
          t('users.permissionsModal.bulkUpdateSuccess', {
            count: pendingBulk.permissions.length,
          })
        );
        setPendingBulk(null);
        setSelectedPermissions(new Set());
      } catch {
        message.error(t('users.permissionsModal.updateError'));
      }
    },
    [pendingBulk, upsertMutation, t, message]
  );

  const requestBulk = useCallback(
    (permissions: string[], action: 'allow' | 'deny' | 'reset') => {
      if (readOnly || permissions.length === 0) return;
      if (action === 'reset') {
        const withOverride = permissions.filter((p) => overridesByPermission.has(p));
        if (withOverride.length === 0) {
          message.info(t('users.permissionsModal.batchResetNone'));
          return;
        }
        modal.confirm({
          title: t('users.permissionsModal.batchResetConfirmTitle'),
          content: t('users.permissionsModal.batchResetConfirmBody', {
            count: withOverride.length,
          }),
          okText: t('users.permissionsModal.confirmContinue'),
          onOk: async () => {
            try {
              const toDelete = withOverride
                .map((permission) => overridesByPermission.get(permission))
                .filter((o): o is UserPermissionOverrideDto => Boolean(o));
              await Promise.all(toDelete.map((record) => deleteMutation.mutateAsync(record.id)));
              message.success(
                t('users.permissionsModal.batchResetSuccess', { count: toDelete.length })
              );
              setSelectedPermissions(new Set());
            } catch {
              message.error(t('users.permissionsModal.updateError'));
            }
          },
        });
        return;
      }
      modal.confirm({
        title:
          action === 'allow'
            ? t('users.permissionsModal.bulkAllowConfirmTitle')
            : t('users.permissionsModal.bulkDenyConfirmTitle'),
        content: t('users.permissionsModal.bulkConfirmBody', { count: permissions.length }),
        okText: t('users.permissionsModal.confirmContinue'),
        onOk: () => {
          setPendingBulk({ permissions, action });
        },
      });
    },
    [readOnly, modal, t, overridesByPermission, message, deleteMutation]
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

  // Prune selection when filtered visibility changes.
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

  // Keyboard: arrows, Space toggle grant, Ctrl+A / Ctrl+D selection.
  useEffect(() => {
    if (!open || readOnly) return;
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

      const currentIndex = focusedPermission
        ? visiblePermissionKeys.indexOf(focusedPermission)
        : -1;

      if (event.key === 'ArrowDown') {
        event.preventDefault();
        const next = visiblePermissionKeys[Math.min(currentIndex + 1, visiblePermissionKeys.length - 1)]!;
        setFocusedPermission(next);
        return;
      }
      if (event.key === 'ArrowUp') {
        event.preventDefault();
        const next = visiblePermissionKeys[Math.max(currentIndex - 1, 0)]!;
        setFocusedPermission(next);
        return;
      }
      if (event.key === ' ' || event.key === 'Enter') {
        if (!focusedPermission) return;
        event.preventDefault();
        const currentlyAllowed = permissionImplied(focusedPermission, effectivePermissions);
        setPendingToggle({ permission: focusedPermission, isGranted: !currentlyAllowed });
      }
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [open, readOnly, visiblePermissionKeys, focusedPermission, effectivePermissions]);

  const loading = effectiveQuery.isLoading || catalogQuery.isLoading;
  const error = effectiveQuery.isError || catalogQuery.isError;
  const highlightQuery = effectiveSearch;

  const statusOptions = useMemo(
    () => [
      { value: 'all' as const, label: t('users.permissionsModal.filterStatusAll') },
      { value: 'allowed' as const, label: t('users.permissionsModal.filterStatusAllowed') },
      { value: 'denied' as const, label: t('users.permissionsModal.filterStatusDenied') },
      { value: 'individual' as const, label: t('users.permissionsModal.filterStatusIndividual') },
    ],
    [t]
  );

  const handleSearchChange = useCallback((next: string) => {
    setPermissionSearch(next);
    setAppliedSearch(null);
  }, []);

  const handleSearchApply = useCallback((value: string) => {
    setPermissionSearch(value);
    setAppliedSearch(value);
  }, []);

  const handleQuickFilter = useCallback((preset: PermissionQuickFilterPreset) => {
    switch (preset) {
      case 'denied':
        setStatusFilter('denied');
        break;
      case 'allowed':
        setStatusFilter('allowed');
        break;
      case 'individual':
        setStatusFilter('individual');
        break;
      case 'allGroups':
        setGroupFilter('all');
        break;
      case 'reset':
        setPermissionSearch('');
        setAppliedSearch(null);
        setGroupFilter('all');
        setStatusFilter('all');
        break;
    }
  }, []);

  const jumpToPermission = useCallback(
    (key: string) => {
      const item = catalog.find((c) => c.key === key);
      if (!item) return;
      const slug = permissionCatalogGroupToSlug(item.group?.trim() || 'Other');
      setGroupFilter('all');
      setStatusFilter('all');
      setPermissionSearch('');
      setAppliedSearch(null);
      setFocusedPermission(key);
      setActiveKeys((prev) => (prev.includes(slug) ? prev : [...prev, slug]));
    },
    [catalog]
  );

  const jumpToGroup = useCallback((slug: string) => {
    setGroupFilter(slug);
    setActiveKeys([slug]);
  }, []);

  const commandItems = useMemo((): PermissionCommandItem[] => {
    const items: PermissionCommandItem[] = [];

    items.push(
      {
        id: 'action-expand',
        label: t('users.permissionsModal.expandAllGroups'),
        group: 'action',
        keywords: ['expand', 'öffnen', 'alle'],
        run: () => setActiveKeys(Array.from(groupedCatalog.keys())),
      },
      {
        id: 'action-collapse',
        label: t('users.permissionsModal.collapseAllGroups'),
        group: 'action',
        keywords: ['collapse', 'schließen'],
        run: () => setActiveKeys([]),
      },
      {
        id: 'action-denied',
        label: t('users.permissionToolbar.quickFilterDenied'),
        group: 'action',
        keywords: ['denied', 'abgelehnt', 'rot'],
        run: () => handleQuickFilter('denied'),
      },
      {
        id: 'action-allowed',
        label: t('users.permissionToolbar.quickFilterAllowed'),
        group: 'action',
        keywords: ['allowed', 'erlaubt', 'grün'],
        run: () => handleQuickFilter('allowed'),
      },
      {
        id: 'action-individual',
        label: t('users.permissionToolbar.quickFilterIndividual'),
        group: 'action',
        keywords: ['individual', 'override', 'individuell'],
        run: () => handleQuickFilter('individual'),
      },
      {
        id: 'action-reset',
        label: t('users.permissionToolbar.quickFilterReset'),
        group: 'action',
        keywords: ['reset', 'zurücksetzen', 'clear'],
        run: () => handleQuickFilter('reset'),
      }
    );

    for (const [slug] of groupedCatalog.entries()) {
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
  }, [
    t,
    groupedCatalog,
    catalog,
    handleQuickFilter,
    jumpToGroup,
    jumpToPermission,
  ]);

  const catalogDescriptionByKey = useMemo(() => {
    const map = new Map<string, string | null | undefined>();
    for (const item of catalog) {
      map.set(item.key, item.description);
    }
    return map;
  }, [catalog]);

  const collapseItems = filteredGroupedCatalog.map(([slug, itemsInGroup]) => {
    const keys = itemsInGroup.map((i) => i.key);
    const selectedInGroup = keys.filter((k) => selectedPermissions.has(k)).length;
    const allSelected = keys.length > 0 && selectedInGroup === keys.length;
    const someSelected = selectedInGroup > 0 && !allSelected;
    return {
      key: slug,
      label: (
        <span style={{ fontWeight: 500, display: 'inline-flex', alignItems: 'center', gap: 8 }}>
          {!readOnly ? (
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
          <HighlightedText
            text={`${resolvePermissionGroupLabel(slug, t)} (${itemsInGroup.length})`}
            query={highlightQuery}
          />
        </span>
      ),
      extra: readOnly ? null : (
        <Space
          size={4}
          onClick={(e) => e.stopPropagation()}
          onKeyDown={(e) => e.stopPropagation()}
        >
          <Button size="small" type="link" onClick={() => setGroupSelection(keys, true)}>
            {t('users.permissionsModal.selectGroupAll')}
          </Button>
          <Button size="small" type="link" onClick={() => setGroupSelection(keys, false)}>
            {t('users.permissionsModal.selectGroupNone')}
          </Button>
          <Button
            size="small"
            type="link"
            icon={<CheckOutlined />}
            onClick={() => requestBulk(keys, 'allow')}
          >
            {t('users.permissionsModal.bulkAllowGroup')}
          </Button>
          <Button
            size="small"
            type="link"
            danger
            icon={<CloseOutlined />}
            onClick={() => requestBulk(keys, 'deny')}
          >
            {t('users.permissionsModal.bulkDenyGroup')}
          </Button>
        </Space>
      ),
      children: (
        <PermissionGroupList
          permissions={keys}
          rolePermissions={rolePermissions}
          effectivePermissions={effectivePermissions}
          overridesByPermission={overridesByPermission}
          catalogDescriptionByKey={catalogDescriptionByKey}
          readOnly={readOnly}
          searchQuery={highlightQuery}
          focusedPermission={focusedPermission}
          onFocusPermission={setFocusedPermission}
          onToggle={(permission, isGranted) => setPendingToggle({ permission, isGranted })}
          selectionEnabled={!readOnly}
          selectedPermissions={selectedPermissions}
          onSelectedChange={toggleSelectPermission}
        />
      ),
    };
  });

  return (
    <>
      <Modal
        title={t('users.permissionsModal.title', { name: userName })}
        open={open}
        onCancel={onClose}
        width={780}
        footer={[
          <Button key="close" onClick={onClose}>
            {t('users.permissionsModal.close')}
          </Button>,
        ]}
      >
        {targetIsSuperAdmin ? (
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 16 }}
            title={t('users.permissionsModal.superAdminTitle')}
            description={t('users.permissionsModal.superAdminDescription')}
          />
        ) : (
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 16 }}
            title={t('users.permissionsModal.infoTitle')}
            description={t('users.permissionsModal.infoDescription')}
          />
        )}

        <div style={{ marginBottom: 12, display: 'flex', justifyContent: 'flex-end' }}>
          <Button type="link" size="small" onClick={() => setGuidedTourOpen(true)}>
            {t('users.permissionOnboarding.guidedTour')}
          </Button>
        </div>

        {error ? (
          <Alert
            type="error"
            showIcon
            title={t('users.permissionsModal.loadError')}
            action={
              <Button size="small" onClick={() => void effectiveQuery.refetch()}>
                {t('users.list.retry')}
              </Button>
            }
          />
        ) : loading ? (
          <FormSkeleton fields={6} />
        ) : (
          <>
            {!readOnly ? (
              <PermissionHealthCheckAlert
                granted={effectivePermissions}
                catalogSize={catalog.length}
                catalogKeys={catalog.map((c) => c.key)}
                allowPlatformCritical={targetIsSuperAdmin}
              />
            ) : null}
            <PermissionChangesPanel
              before={rolePermissions}
              after={effectivePermissions}
              visible={overrides.length > 0}
            />
            <PermissionCatalogToolbar
              searchValue={permissionSearch}
              onSearchChange={handleSearchChange}
              onSearchApply={handleSearchApply}
              searchPlaceholder={t('users.roleDrawer.permissionSearchPlaceholder')}
              counterLabel={t('users.roleDrawer.permissionSearchCounter', {
                visible: permissionVisibleCount,
                total: catalog.length,
              })}
              visibleCount={permissionVisibleCount}
              totalCount={catalog.length}
              shortcutEnabled={open}
              groupFilter={groupFilter}
              onGroupFilterChange={setGroupFilter}
              groupOptions={groupOptions}
              allGroupsLabel={t('users.permissionsModal.filterGroupAll')}
              statusFilter={statusFilter}
              onStatusFilterChange={setStatusFilter}
              statusOptions={statusOptions}
              onQuickFilter={handleQuickFilter}
              commandItems={commandItems}
              expandAllLabel={t('users.permissionsModal.expandAllGroups')}
              collapseAllLabel={t('users.permissionsModal.collapseAllGroups')}
              onExpandAll={() => setActiveKeys(filteredGroupedCatalog.map(([s]) => s))}
              onCollapseAll={() => setActiveKeys([])}
              style={{ marginBottom: 12 }}
            />

            {!readOnly ? (
              <PermissionBatchToolbar
                selectedCount={selectedVisibleKeys.length}
                disabled={upsertMutation.isPending || deleteMutation.isPending}
                onAllow={() => requestBulk(selectedVisibleKeys, 'allow')}
                onDeny={() => requestBulk(selectedVisibleKeys, 'deny')}
                onResetToRoleDefault={() => requestBulk(selectedVisibleKeys, 'reset')}
                onClearSelection={() => setSelectedPermissions(new Set())}
              />
            ) : null}

            {filteredGroupedCatalog.length === 0 ? (
              <Empty
                image={Empty.PRESENTED_IMAGE_SIMPLE}
                description={t('users.roleDrawer.permissionSearchEmpty')}
              />
            ) : (
              <Collapse
                activeKey={activeKeys}
                onChange={(keys) => setActiveKeys(Array.isArray(keys) ? keys.map(String) : [String(keys)])}
                items={collapseItems}
                style={{ maxHeight: 480, overflow: 'auto' }}
              />
            )}

            {!effectiveSearch.trim() && overrides.length > 0 ? (
              <div style={{ marginTop: 16 }}>
                <Typography.Text strong style={{ display: 'block', marginBottom: 8 }}>
                  {t('users.permissionsModal.tabOverrides')}
                </Typography.Text>
                <OverridesTable
                  overrides={overrides}
                  readOnly={readOnly}
                  loading={deleteMutation.isPending}
                  onDelete={(record) => void handleDeleteOverride(record)}
                  t={t}
                />
              </div>
            ) : null}

            <PermissionAuditHistoryPanel
              userId={userId}
              canRevert={!readOnly}
              onReverted={() => {
                void effectiveQuery.refetch();
              }}
            />
          </>
        )}
      </Modal>

      <PermissionGuidedTour
        open={guidedTourOpen}
        onClose={() => setGuidedTourOpen(false)}
        includeSaveStep={false}
      />

      {pendingToggle ? (
        <PermissionOverrideConfirmModal
          pendingToggle={pendingToggle}
          onCancel={() => setPendingToggle(null)}
          onConfirm={handleOverrideConfirm}
          confirmLoading={upsertMutation.isPending}
          t={t}
        />
      ) : null}

      {pendingBulk && pendingBulk.action !== 'reset' ? (
        <PermissionOverrideConfirmModal
          pendingToggle={{
            permission: t('users.permissionsModal.bulkPermissionSummary', {
              count: pendingBulk.permissions.length,
            }),
            isGranted: pendingBulk.action === 'allow',
          }}
          onCancel={() => setPendingBulk(null)}
          onConfirm={handleBulkConfirm}
          confirmLoading={upsertMutation.isPending}
          t={t}
        />
      ) : null}
    </>
  );
}

type PermissionOverrideConfirmModalProps = {
  pendingToggle: { permission: string; isGranted: boolean };
  onCancel: () => void;
  onConfirm: (values: OverrideFormValues) => Promise<void>;
  confirmLoading?: boolean;
  t: (key: string, options?: Record<string, string | number>) => string;
};

function PermissionOverrideConfirmModal({
  pendingToggle,
  onCancel,
  onConfirm,
  confirmLoading,
  t,
}: PermissionOverrideConfirmModalProps) {
  const [form] = Form.useForm<OverrideFormValues>();

  const handleOk = () => {
    void form
      .validateFields()
      .then((values) => onConfirm(values))
      .catch(() => {
        /* validation shown on form */
      });
  };

  const handleCancel = () => {
    form.resetFields();
    onCancel();
  };

  const displayPermission = pendingToggle.permission.includes('.')
    ? resolvePermissionDisplayLabel(pendingToggle.permission, t)
    : pendingToggle.permission;

  return (
    <Modal
      title={t('users.permissionsModal.confirmTitle')}
      open
      onCancel={handleCancel}
      onOk={handleOk}
      confirmLoading={confirmLoading}
    >
      <Typography.Paragraph type="secondary">
        {t('users.permissionsModal.confirmBody', {
          permission: displayPermission,
          status: pendingToggle.isGranted
            ? t('users.permissionsModal.statusGranted')
            : t('users.permissionsModal.statusDenied'),
        })}
      </Typography.Paragraph>
      <Form form={form} layout="vertical">
        <Form.Item name="reason" label={t('users.permissionsModal.reasonLabel')}>
          <Input.TextArea rows={2} maxLength={500} />
        </Form.Item>
        <Form.Item name="validFrom" label={t('users.permissionsModal.validFromLabel')}>
          <DatePicker className="w-full" format={DAYJS_DATE_FORMAT} showTime />
        </Form.Item>
        <Form.Item name="expiresAt" label={t('users.permissionsModal.expiresLabel')}>
          <DatePicker className="w-full" format={DAYJS_DATE_FORMAT} showTime />
        </Form.Item>
      </Form>
    </Modal>
  );
}

type PermissionGroupListProps = {
  permissions: string[];
  rolePermissions: string[];
  effectivePermissions: string[];
  overridesByPermission: Map<string, UserPermissionOverrideDto>;
  catalogDescriptionByKey: Map<string, string | null | undefined>;
  readOnly: boolean;
  searchQuery?: string;
  focusedPermission?: string | null;
  onFocusPermission?: (permission: string) => void;
  onToggle: (permission: string, isGranted: boolean) => void;
  selectionEnabled?: boolean;
  selectedPermissions?: Set<string>;
  onSelectedChange?: (permission: string, selected: boolean) => void;
};

function PermissionGroupList({
  permissions,
  rolePermissions,
  effectivePermissions,
  overridesByPermission,
  catalogDescriptionByKey,
  readOnly,
  searchQuery = '',
  focusedPermission,
  onFocusPermission,
  onToggle,
  selectionEnabled = false,
  selectedPermissions,
  onSelectedChange,
}: PermissionGroupListProps) {
  return (
    <div role="list" style={{ margin: '0 -4px' }}>
      {permissions.map((permission) => {
        const override = overridesByPermission.get(permission);
        const effectiveAllowed = permissionImplied(permission, effectivePermissions);
        const roleAllowed = permissionImplied(permission, rolePermissions);
        const source = override
          ? ('custom' as const)
          : effectiveAllowed && !roleAllowed
            ? ('implied' as const)
            : roleAllowed
              ? ('role' as const)
              : ('none' as const);

        return (
          <PermissionListRow
            key={permission}
            permission={permission}
            mode="switch"
            checked={effectiveAllowed}
            disabled={readOnly}
            onChange={(checked) => onToggle(permission, checked)}
            searchQuery={searchQuery}
            focused={focusedPermission === permission}
            onFocus={() => onFocusPermission?.(permission)}
            source={source}
            heldPermissions={effectivePermissions}
            catalogDescription={catalogDescriptionByKey.get(permission)}
            selectionEnabled={selectionEnabled}
            selected={selectedPermissions?.has(permission) ?? false}
            onSelectedChange={
              onSelectedChange
                ? (selected) => onSelectedChange(permission, selected)
                : undefined
            }
          />
        );
      })}
    </div>
  );
}

type OverridesTableProps = {
  overrides: UserPermissionOverrideDto[];
  readOnly: boolean;
  loading: boolean;
  onDelete: (record: UserPermissionOverrideDto) => void;
  t: (key: string, options?: Record<string, string | number>) => string;
};

function OverridesTable({ overrides, readOnly, loading, onDelete, t }: OverridesTableProps) {
  const columns: ColumnsType<UserPermissionOverrideDto> = [
    {
      title: t('users.permissionsModal.columnPermission'),
      dataIndex: 'permission',
      render: (perm: string) => resolvePermissionDisplayLabel(perm, t),
    },
    {
      title: t('users.permissionsModal.columnStatus'),
      dataIndex: 'isGranted',
      width: 120,
      render: (granted: boolean) =>
        granted
          ? t('users.permissionsModal.statusGranted')
          : t('users.permissionsModal.statusDenied'),
    },
    {
      title: t('users.permissionsModal.reasonLabel'),
      dataIndex: 'reason',
      ellipsis: true,
      render: (v: string | null | undefined) => v?.trim() || '—',
    },
    {
      title: t('users.permissionsModal.expiresLabel'),
      dataIndex: 'expiresAt',
      width: 180,
      render: (d: string | null | undefined, record) => {
        const status =
          (record.status as PermissionOverrideStatus | undefined) ??
          computePermissionOverrideStatus(record.validFrom, record.expiresAt);
        return (
          <Space size={4} wrap>
            <span>{d ? formatDate(d, '') : '—'}</span>
            <Tag color={permissionOverrideStatusColor(status)}>
              {t(`users.temporaryPermissions.status.${status}`)}
            </Tag>
          </Space>
        );
      },
    },
    {
      key: 'actions',
      width: 56,
      render: (_: unknown, record) =>
        readOnly ? null : (
          <Button
            type="text"
            danger
            icon={<DeleteOutlined />}
            loading={loading}
            aria-label={t('users.permissionsModal.removeOverride')}
            onClick={() => onDelete(record)}
          />
        ),
    },
  ];

  return (
    <Table
      dataSource={overrides}
      columns={columns}
      rowKey="id"
      pagination={false}
      size="small"
      locale={{ emptyText: t('users.permissionsModal.noOverrides') }}
    />
  );
}
