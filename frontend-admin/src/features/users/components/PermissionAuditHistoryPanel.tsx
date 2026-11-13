'use client';

import {
  HistoryOutlined,
  LeftOutlined,
  RightOutlined,
  UndoOutlined,
} from '@ant-design/icons';
import {
  Alert,
  Button,
  Empty,
  List,
  Space,
  Spin,
  Tag,
  Typography,
} from 'antd';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';

import { getApiAuditLog } from '@/api/generated/audit-log/audit-log';
import type { AuditLogEntryDto } from '@/api/generated/model/auditLogEntryDto';
import {
  addPermissionAuditNote,
  revertPermissionAudit,
  type PermissionAuditEntry,
} from '@/features/audit/api/permissionAudit';
import {
  permissionAuditQueryKey,
  usePermissionAudit,
} from '@/features/audit/hooks/usePermissionAudit';
import { useAuditLogUserFilterOptions } from '@/features/audit-logs/hooks/useAuditLogUserFilterOptions';
import { downloadAuditLogExport } from '@/features/audit-logs/utils/exportAuditLogs';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { PermissionAuditDiffModal } from '@/features/users/components/PermissionAuditDiffModal';
import { PermissionAuditFilterBar } from '@/features/users/components/PermissionAuditFilterBar';
import { PermissionAuditReportModal } from '@/features/users/components/PermissionAuditReportModal';
import { PermissionAuditRevertModal } from '@/features/users/components/PermissionAuditRevertModal';
import {
  deleteUserPermissionOverride,
  upsertUserPermissionOverride,
} from '@/features/users/api/userPermissionOverridesApi';
import { updateRolePermissions } from '@/features/users/api/usersGateway';
import { useRolesWithPermissions } from '@/features/users/hooks/useRolesWithPermissions';
import {
  buildPermissionAuditDiff,
  permissionAuditBorderColor,
  permissionAuditTagColor,
  type PermissionAuditColor,
  type PermissionStateKind,
} from '@/features/users/utils/permissionAuditDiff';
import {
  applyClientPermissionAuditFilters,
  type PermissionAuditFilterState,
  type PermissionAuditQuickFilter,
} from '@/features/users/utils/permissionAuditFilters';
import {
  PERMISSION_AUDIT_ACTIONS,
  isPermissionAuditAction,
  resolvePermissionAuditRevert,
  type PermissionAuditAction,
} from '@/features/users/utils/permissionAuditRevert';
import { formatRoleDisplayLabel } from '@/features/users/utils/roleDisplayLabel';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { formatDateTime } from '@/i18n/formatting';
import { usePermissions } from '@/hooks/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';

const PAGE_SIZE = 10;
/** Larger page when client-side search / critical filters need more rows. */
const SEARCH_PAGE_SIZE = 100;

type DedicatedAction = PermissionAuditEntry['action'];

type PermissionAuditHistoryPanelProps = {
  /** Role editor context */
  roleName?: string | null;
  /** Identity role id / roleKey when available */
  roleId?: string | null;
  /** Optional display label for the role (falls back to roleName). */
  subjectLabel?: string | null;
  /** User overrides context (legacy AuditLog path) */
  userId?: string | null;
  /**
   * Tenant-wide permission history (dedicated API without role/user scope).
   * Used by `/admin/access/permission-history`.
   */
  showAllRoles?: boolean;
  /** When true, allow revert actions (requires manage permissions). */
  canRevert?: boolean;
  onReverted?: () => void;
};

function formatTimestampWithOffset(iso: string | null | undefined): string {
  if (!iso) return '—';
  const base = formatDateTime(iso);
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return base || '—';
  const offsetMin = -d.getTimezoneOffset();
  const sign = offsetMin >= 0 ? '+' : '-';
  const abs = Math.abs(offsetMin);
  const hh = String(Math.floor(abs / 60)).padStart(2, '0');
  const mm = String(abs % 60).padStart(2, '0');
  return `${base || '—'} (GMT${sign}${hh}${mm !== '00' ? `:${mm}` : ''})`;
}

function stateLabel(
  state: string | null | undefined,
  t: (key: string, options?: Record<string, string | number>) => string
): string {
  if (!state) return '—';
  const known: PermissionStateKind[] = [
    'allowed',
    'denied',
    'individual',
    'absent',
    'defaults',
  ];
  if ((known as string[]).includes(state)) {
    return t(`users.permissionAudit.state.${state}`);
  }
  return state;
}

function dedicatedActionLabel(
  action: DedicatedAction,
  t: (key: string, options?: Record<string, string | number>) => string
): string {
  const key = `users.permissionAudit.actions.${action}`;
  const labeled = t(key);
  return labeled === key ? action : labeled;
}

function isDedicatedAction(
  action: string
): action is DedicatedAction {
  return (
    action === 'created' ||
    action === 'updated' ||
    action === 'deleted' ||
    action === 'reverted'
  );
}

function legacyActionLabel(
  entry: AuditLogEntryDto,
  t: (key: string, options?: Record<string, string | number>) => string
): string {
  const action = (entry.action ?? '').trim();
  if (!action) return t('users.permissionAudit.unknownAction');
  const key = `users.permissionAudit.actions.${action}`;
  const labeled = t(key);
  return labeled === key ? action : labeled;
}

function colorForDedicatedEntry(entry: PermissionAuditEntry): PermissionAuditColor {
  if (entry.action === 'created' || entry.action === 'deleted') return 'blue';
  if (entry.action === 'reverted') return 'yellow';
  const oldV = (entry.oldValue ?? '').toLowerCase();
  const newV = (entry.newValue ?? '').toLowerCase();
  const added =
    (oldV === 'absent' || oldV === '' || oldV === 'denied') &&
    (newV === 'allowed' || newV === 'individual');
  const removed =
    (oldV === 'allowed' || oldV === 'individual') &&
    (newV === 'absent' || newV === 'denied' || newV === '');
  if (added && !removed) return 'green';
  if (removed && !added) return 'red';
  return 'yellow';
}

function downloadClientCsv(entries: PermissionAuditEntry[], filename: string): void {
  const header = [
    'id',
    'timestamp',
    'actorUserId',
    'actorName',
    'actorEmail',
    'action',
    'roleId',
    'roleName',
    'permissionKey',
    'oldValue',
    'newValue',
    'reason',
    'ipAddress',
  ];
  const escape = (v: string | null | undefined) => {
    const s = v ?? '';
    if (/[",\n]/.test(s)) return `"${s.replace(/"/g, '""')}"`;
    return s;
  };
  const lines = [
    header.join(','),
    ...entries.map((e) =>
      [
        e.id,
        e.timestamp,
        e.actorUserId,
        e.actorName,
        e.actorEmail,
        e.action,
        e.roleId,
        e.roleName,
        e.permissionKey,
        e.oldValue,
        e.newValue,
        e.reason,
        e.ipAddress,
      ]
        .map(escape)
        .join(',')
    ),
  ];
  const blob = new Blob([lines.join('\n')], { type: 'text/csv;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}

/**
 * Änderungshistorie for role permissions / user overrides with filters, diffs, export, revert.
 * Role context uses GET /api/admin/audit/permissions via usePermissionAudit.
 * User-override context keeps the legacy AuditLog list until the dedicated API supports targetUserId.
 */
export function PermissionAuditHistoryPanel({
  roleName,
  roleId,
  subjectLabel,
  userId,
  showAllRoles = false,
  canRevert = false,
  onReverted,
}: PermissionAuditHistoryPanelProps) {
  const { t } = useI18n();
  const { message, modal } = useAntdApp();
  const queryClient = useQueryClient();
  const { user } = useAuth();
  const { hasPermission } = usePermissions();
  const canViewAudit = hasPermission(PERMISSIONS.AUDIT_VIEW);
  const canExport = hasPermission(PERMISSIONS.AUDIT_EXPORT);
  const { options: actorOptions, isLoading: actorsLoading } = useAuditLogUserFilterOptions();
  const rolesQuery = useRolesWithPermissions({ enabled: canViewAudit });

  const useDedicatedApi = Boolean(roleName || roleId || showAllRoles);
  const useLegacyUserApi = Boolean(userId) && !useDedicatedApi;

  const [range, setRange] = useState<[Dayjs | null, Dayjs | null] | null>(null);
  const [actorUserId, setActorUserId] = useState<string | undefined>();
  const [permissionKeyFilter, setPermissionKeyFilter] = useState('');
  const [roleNameFilter, setRoleNameFilter] = useState<string | undefined>(
    roleName ?? undefined
  );
  const [search, setSearch] = useState('');
  const [quickFilters, setQuickFilters] = useState<PermissionAuditQuickFilter[]>([]);
  const [actionFilter, setActionFilter] = useState<DedicatedAction | PermissionAuditAction | 'all'>(
    'all'
  );
  const [revertingId, setRevertingId] = useState<string | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [exporting, setExporting] = useState(false);
  const [reportOpen, setReportOpen] = useState(false);
  const [diffEntry, setDiffEntry] = useState<PermissionAuditEntry | null>(null);
  const [revertEntry, setRevertEntry] = useState<PermissionAuditEntry | null>(null);
  const [newerWarning, setNewerWarning] = useState<string | null>(null);
  const [revertLoading, setRevertLoading] = useState(false);

  useEffect(() => {
    if (roleName) setRoleNameFilter(roleName);
  }, [roleName]);

  const titleSubject = useMemo(() => {
    if (subjectLabel?.trim()) return subjectLabel.trim();
    if (roleName) return formatRoleDisplayLabel(t, roleName);
    return null;
  }, [subjectLabel, roleName, t]);

  const roleOptions = useMemo(
    () =>
      (rolesQuery.data ?? [])
        .filter((r) => Boolean(r.roleName))
        .map((r) => ({
          value: r.roleName!,
          label: formatRoleDisplayLabel(t, r.roleName!),
        })),
    [rolesQuery.data, t]
  );

  const needsClientHeavyFilter =
    Boolean(search.trim()) ||
    quickFilters.includes('onlyCritical') ||
    (actionFilter !== 'all' && useDedicatedApi);

  const effectivePageSize = needsClientHeavyFilter ? SEARCH_PAGE_SIZE : PAGE_SIZE;

  const effectiveRoleName = roleNameFilter || roleName || undefined;
  const effectiveRoleId =
    roleNameFilter && roleNameFilter !== roleName ? undefined : roleId || undefined;

  const dedicatedParams = useMemo(
    () => ({
      roleId: effectiveRoleId || undefined,
      roleName: effectiveRoleName || undefined,
      permissionKey: permissionKeyFilter.trim() || undefined,
      actorUserId: actorUserId || undefined,
      fromDate: range?.[0]?.toISOString(),
      toDate: range?.[1]?.toISOString(),
      page: needsClientHeavyFilter ? 1 : page,
      pageSize: effectivePageSize,
    }),
    [
      effectiveRoleId,
      effectiveRoleName,
      permissionKeyFilter,
      actorUserId,
      range,
      page,
      needsClientHeavyFilter,
      effectivePageSize,
    ]
  );

  const dedicatedQuery = usePermissionAudit(dedicatedParams, {
    enabled: canViewAudit && useDedicatedApi,
  });

  const legacyQueryParams = useMemo(() => {
    return {
      page: 1,
      pageSize: SEARCH_PAGE_SIZE,
      startDate: range?.[0]?.toISOString(),
      endDate: range?.[1]?.toISOString(),
      userId: actorUserId || undefined,
      targetUserId: userId || undefined,
      action:
        actionFilter === 'all'
          ? 'USER_PERMISSION_OVERRIDES_CHANGED'
          : String(actionFilter),
    };
  }, [range, actorUserId, actionFilter, userId]);

  const legacyQuery = useQuery({
    queryKey: ['permission-audit-history-legacy', legacyQueryParams],
    queryFn: () => getApiAuditLog(legacyQueryParams),
    enabled: canViewAudit && useLegacyUserApi,
    staleTime: 30_000,
  });

  const sinceIso = useMemo(() => {
    if (!quickFilters.includes('last24h')) return undefined;
    return dayjs().subtract(24, 'hour').toISOString();
  }, [quickFilters]);

  const dedicatedEntries = useMemo(() => {
    const raw = dedicatedQuery.data?.items ?? [];
    return applyClientPermissionAuditFilters(raw, {
      search,
      action: actionFilter === 'all' || !isDedicatedAction(actionFilter) ? 'all' : actionFilter,
      onlyCritical: quickFilters.includes('onlyCritical'),
      onlyActorUserId: quickFilters.includes('onlyMine') ? user?.id ?? undefined : undefined,
      sinceIso,
    });
  }, [
    dedicatedQuery.data?.items,
    search,
    actionFilter,
    quickFilters,
    user?.id,
    sinceIso,
  ]);

  const legacyEntries = useMemo(() => {
    const raw = legacyQuery.data?.auditLogs ?? [];
    let list = raw.filter((e) => isPermissionAuditAction(e.action));
    const permQ = permissionKeyFilter.trim().toLowerCase();
    const freeQ = search.trim().toLowerCase();
    if (permQ) {
      list = list.filter((e) => {
        const hay = `${e.oldValues ?? ''} ${e.newValues ?? ''} ${e.description ?? ''}`.toLowerCase();
        return hay.includes(permQ);
      });
    }
    if (freeQ) {
      list = list.filter((e) => {
        const hay =
          `${e.description ?? ''} ${e.oldValues ?? ''} ${e.newValues ?? ''} ${e.notes ?? ''} ${e.actorDisplayName ?? ''} ${e.userId ?? ''}`.toLowerCase();
        return hay.includes(freeQ);
      });
    }
    if (sinceIso) {
      const since = new Date(sinceIso).getTime();
      list = list.filter((e) => {
        const ts = e.timestamp ? new Date(e.timestamp).getTime() : NaN;
        return !Number.isNaN(ts) && ts >= since;
      });
    }
    if (quickFilters.includes('onlyMine') && user?.id) {
      list = list.filter((e) => e.userId === user.id);
    }
    return list;
  }, [
    legacyQuery.data,
    permissionKeyFilter,
    search,
    sinceIso,
    quickFilters,
    user?.id,
  ]);

  useEffect(() => {
    setPage(1);
    setSelectedId(null);
  }, [
    roleId,
    roleName,
    roleNameFilter,
    userId,
    actorUserId,
    permissionKeyFilter,
    actionFilter,
    range,
    search,
    quickFilters,
  ]);

  const isLoading = useDedicatedApi ? dedicatedQuery.isLoading : legacyQuery.isLoading;
  const isError = useDedicatedApi ? dedicatedQuery.isError : legacyQuery.isError;

  const clientPagedDedicated = useMemo(() => {
    if (!needsClientHeavyFilter) return dedicatedEntries;
    const start = (page - 1) * PAGE_SIZE;
    return dedicatedEntries.slice(start, start + PAGE_SIZE);
  }, [dedicatedEntries, needsClientHeavyFilter, page]);

  const total = useDedicatedApi
    ? needsClientHeavyFilter
      ? dedicatedEntries.length
      : (dedicatedQuery.data?.totalCount ?? dedicatedEntries.length)
    : legacyEntries.length;
  const totalPages = useDedicatedApi
    ? needsClientHeavyFilter
      ? Math.max(1, Math.ceil(dedicatedEntries.length / PAGE_SIZE) || 1)
      : Math.max(1, dedicatedQuery.data?.totalPages ?? 1)
    : Math.max(1, Math.ceil(legacyEntries.length / PAGE_SIZE) || 1);
  const safePage = Math.min(page, totalPages);

  const legacyPageSlice = useMemo(() => {
    const start = (safePage - 1) * PAGE_SIZE;
    return legacyEntries.slice(start, start + PAGE_SIZE);
  }, [legacyEntries, safePage]);

  const dedicatedPageSlice = needsClientHeavyFilter
    ? clientPagedDedicated
    : dedicatedEntries;

  const rangeFrom = total === 0 ? 0 : (safePage - 1) * PAGE_SIZE + 1;
  const rangeTo = Math.min(safePage * PAGE_SIZE, total);

  const selectedDedicated = useMemo(
    () => dedicatedEntries.find((e) => e.id === selectedId) ?? null,
    [dedicatedEntries, selectedId]
  );
  const selectedLegacy = useMemo(
    () => legacyEntries.find((e) => e.id === selectedId) ?? null,
    [legacyEntries, selectedId]
  );

  const buildFilterSnapshot = useCallback((): PermissionAuditFilterState => {
    return {
      fromDate: range?.[0]?.toISOString(),
      toDate: range?.[1]?.toISOString(),
      actorUserId,
      permissionKey: permissionKeyFilter.trim() || undefined,
      roleName: roleNameFilter,
      roleId: effectiveRoleId || undefined,
      action: isDedicatedAction(actionFilter) || actionFilter === 'all' ? actionFilter : 'all',
      search: search.trim() || undefined,
      quickFilters: [...quickFilters],
    };
  }, [
    range,
    actorUserId,
    permissionKeyFilter,
    roleNameFilter,
    effectiveRoleId,
    actionFilter,
    search,
    quickFilters,
  ]);

  const applySavedFilters = useCallback(
    (filters: PermissionAuditFilterState) => {
      setRange(
        filters.fromDate || filters.toDate
          ? [filters.fromDate ? dayjs(filters.fromDate) : null, filters.toDate ? dayjs(filters.toDate) : null]
          : null
      );
      setActorUserId(filters.actorUserId);
      setPermissionKeyFilter(filters.permissionKey ?? '');
      if (!roleName) setRoleNameFilter(filters.roleName);
      setSearch(filters.search ?? '');
      setQuickFilters(filters.quickFilters ?? []);
      setActionFilter(filters.action ?? 'all');
      setPage(1);
    },
    [roleName]
  );
  const invalidateAudit = useCallback(async () => {
    await queryClient.invalidateQueries({ queryKey: ['permission-audit'] });
    await queryClient.invalidateQueries({ queryKey: ['permission-audit-history-legacy'] });
  }, [queryClient]);

  const openRevertDedicated = useCallback(
    (entry: PermissionAuditEntry) => {
      if (entry.action === 'created' || entry.action === 'deleted') {
        message.info(t('users.permissionAudit.revertUnsupported.roleLifecycleManual'));
        return;
      }
      setNewerWarning(null);
      setRevertEntry(entry);
      setDiffEntry(null);
    },
    [message, t]
  );

  const confirmRevertDedicated = useCallback(
    async (reason: string, force: boolean) => {
      if (!revertEntry) return;
      setRevertLoading(true);
      setRevertingId(revertEntry.id);
      try {
        const result = await revertPermissionAudit(revertEntry.id, {
          reason: reason || undefined,
          force,
        });
        if (!result.success && result.warningNewerChanges) {
          setNewerWarning(
            t('users.permissionAudit.revertDialog.newerWarning', {
              count: result.newerChangesCount ?? 0,
            })
          );
          return;
        }
        if (!result.success) {
          message.error(result.message || t('users.permissionAudit.revertError'));
          return;
        }
        message.success(t('users.permissionAudit.revertSuccess'));
        setRevertEntry(null);
        setNewerWarning(null);
        await invalidateAudit();
        await queryClient.invalidateQueries({ queryKey: permissionAuditQueryKey(dedicatedParams) });
        onReverted?.();
      } catch (err: unknown) {
        const ax = err as {
          response?: { status?: number; data?: { warningNewerChanges?: boolean; newerChangesCount?: number; message?: string } };
        };
        if (ax.response?.status === 409 && ax.response.data?.warningNewerChanges) {
          setNewerWarning(
            t('users.permissionAudit.revertDialog.newerWarning', {
              count: ax.response.data.newerChangesCount ?? 0,
            })
          );
          return;
        }
        message.error(t('users.permissionAudit.revertError'));
      } finally {
        setRevertLoading(false);
        setRevertingId(null);
      }
    },
    [revertEntry, t, message, invalidateAudit, queryClient, dedicatedParams, onReverted]
  );

  const handleAddNote = useCallback(
    async (entry: PermissionAuditEntry, note: string) => {
      await addPermissionAuditNote(entry.id, { note });
      await invalidateAudit();
    },
    [invalidateAudit]
  );

  const runRevertLegacy = useCallback(
    (entry: AuditLogEntryDto) => {
      const capability = resolvePermissionAuditRevert(entry, { roleName, userId });
      if (capability.kind === 'unsupported') {
        message.info(t(`users.permissionAudit.revertUnsupported.${capability.reason}`));
        return;
      }

      modal.confirm({
        title: t('users.permissionAudit.revertConfirmTitle'),
        content: t('users.permissionAudit.revertConfirmBody'),
        okText: t('users.permissionAudit.revert'),
        onOk: async () => {
          setRevertingId(entry.id ?? null);
          try {
            if (capability.kind === 'rolePermissions') {
              await updateRolePermissions(capability.roleName, capability.permissions);
            } else if (capability.kind === 'overrideUpsert') {
              await upsertUserPermissionOverride(capability.userId, {
                permission: capability.permission,
                isGranted: capability.isGranted,
                tenantId: capability.tenantId,
                expiresAt: capability.expiresAt,
                reason: t('users.permissionAudit.revertReason'),
              });
            } else if (capability.kind === 'overrideDelete') {
              await deleteUserPermissionOverride(capability.userId, capability.overrideId);
            }
            message.success(t('users.permissionAudit.revertSuccess'));
            await invalidateAudit();
            onReverted?.();
          } catch {
            message.error(t('users.permissionAudit.revertError'));
          } finally {
            setRevertingId(null);
          }
        },
      });
    },
    [roleName, userId, message, modal, t, invalidateAudit, onReverted]
  );

  const handleExport = useCallback(async () => {
    if (!canExport) {
      message.info(t('users.permissionAudit.noAuditPermission'));
      return;
    }
    setExporting(true);
    try {
      if (useDedicatedApi) {
        downloadClientCsv(
          dedicatedEntries,
          `permission_audit_${new Date().toISOString().slice(0, 10)}.csv`
        );
        message.success(t('users.permissionAudit.exportSuccess'));
        return;
      }
      const exportQuery: Record<string, string> = {};
      if (legacyQueryParams.startDate) exportQuery.startDate = legacyQueryParams.startDate;
      if (legacyQueryParams.endDate) exportQuery.endDate = legacyQueryParams.endDate;
      if (legacyQueryParams.userId) exportQuery.userId = legacyQueryParams.userId;
      if (legacyQueryParams.action) exportQuery.action = legacyQueryParams.action;
      if (legacyQueryParams.targetUserId) {
        exportQuery.targetUserId = legacyQueryParams.targetUserId;
      }
      await downloadAuditLogExport('csv', exportQuery, {
        exportFailedMessage: t('users.permissionAudit.exportError'),
      });
      message.success(t('users.permissionAudit.exportSuccess'));
    } catch {
      message.error(t('users.permissionAudit.exportError'));
    } finally {
      setExporting(false);
    }
  }, [
    canExport,
    useDedicatedApi,
    dedicatedEntries,
    legacyQueryParams,
    message,
    t,
  ]);

  if (!canViewAudit) {
    return (
      <Alert
        type="info"
        showIcon
        style={{ marginTop: 8 }}
        title={t('users.permissionAudit.noAuditPermission')}
      />
    );
  }

  if (!useDedicatedApi && !useLegacyUserApi) {
    return (
      <Empty
        image={Empty.PRESENTED_IMAGE_SIMPLE}
        description={t('users.permissionAudit.empty')}
      />
    );
  }

  const canRevertSelectedDedicated =
    canRevert &&
    selectedDedicated != null &&
    selectedDedicated.action !== 'created' &&
    selectedDedicated.action !== 'deleted';
  const selectedLegacyRevert =
    selectedLegacy != null
      ? resolvePermissionAuditRevert(selectedLegacy, { roleName, userId })
      : null;
  const canRevertSelectedLegacy =
    canRevert && selectedLegacyRevert != null && selectedLegacyRevert.kind !== 'unsupported';

  return (
    <div style={{ marginTop: 8 }}>
      <Typography.Title level={5} style={{ marginTop: 0, marginBottom: 12 }}>
        <HistoryOutlined style={{ marginRight: 8 }} />
        {titleSubject
          ? t('users.permissionAudit.titleWithSubject', { subject: titleSubject })
          : t('users.permissionAudit.title')}
      </Typography.Title>

      <PermissionAuditFilterBar
        range={range}
        onRangeChange={setRange}
        actorUserId={actorUserId}
        onActorChange={setActorUserId}
        actorOptions={actorOptions}
        actorsLoading={actorsLoading}
        permissionKey={permissionKeyFilter}
        onPermissionKeyChange={setPermissionKeyFilter}
        roleNameFilter={roleNameFilter}
        onRoleNameChange={setRoleNameFilter}
        roleOptions={roleOptions}
        roleFilterLocked={Boolean(roleName)}
        actionFilter={
          useDedicatedApi
            ? isDedicatedAction(String(actionFilter)) || actionFilter === 'all'
              ? (actionFilter as DedicatedAction | 'all')
              : 'all'
            : actionFilter
        }
        onActionChange={(v) => setActionFilter(v as DedicatedAction | PermissionAuditAction | 'all')}
        actionMode={useDedicatedApi ? 'dedicated' : 'legacy'}
        search={search}
        onSearchChange={setSearch}
        quickFilters={quickFilters}
        onQuickFiltersChange={setQuickFilters}
        currentUserId={user?.id}
        tenantId={user?.tenantId}
        currentUserName={
          user?.userName ||
          [user?.firstName, user?.lastName].filter(Boolean).join(' ') ||
          user?.email
        }
        buildFilterSnapshot={buildFilterSnapshot}
        onApplySavedFilters={applySavedFilters}
      />

      {isLoading ? (
        <Spin />
      ) : isError ? (
        <Alert type="error" showIcon title={t('users.permissionAudit.loadError')} />
      ) : useDedicatedApi ? (
        dedicatedEntries.length === 0 ? (
          <Empty
            image={Empty.PRESENTED_IMAGE_SIMPLE}
            description={t('users.permissionAudit.empty')}
          />
        ) : (
          <List
            size="small"
            bordered
            dataSource={dedicatedPageSlice}
            style={{ maxHeight: 420, overflow: 'auto', background: '#fff' }}
            renderItem={(entry) => {
              const color = colorForDedicatedEntry(entry);
              const border = permissionAuditBorderColor(color);
              const isSelected = entry.id === selectedId;
              const canRevertEntry =
                canRevert && entry.action !== 'created' && entry.action !== 'deleted';
              const actorSub = entry.actorEmail?.trim() || entry.actorUserId?.trim() || null;

              return (
                <List.Item
                  onClick={() => {
                    setSelectedId(entry.id);
                    setDiffEntry(entry);
                  }}
                  style={{
                    cursor: 'pointer',
                    borderLeft: `4px solid ${border}`,
                    background: isSelected ? 'rgba(22, 119, 255, 0.06)' : undefined,
                    alignItems: 'flex-start',
                  }}
                  actions={
                    canRevertEntry
                      ? [
                          <Button
                            key="diff"
                            type="link"
                            size="small"
                            onClick={(e) => {
                              e.stopPropagation();
                              setDiffEntry(entry);
                            }}
                          >
                            {t('users.permissionAudit.diff.open')}
                          </Button>,
                          <Button
                            key="revert"
                            type="link"
                            size="small"
                            icon={<UndoOutlined />}
                            loading={revertingId === entry.id}
                            onClick={(e) => {
                              e.stopPropagation();
                              openRevertDedicated(entry);
                            }}
                          >
                            {t('users.permissionAudit.revert')}
                          </Button>,
                        ]
                      : [
                          <Button
                            key="diff"
                            type="link"
                            size="small"
                            onClick={(e) => {
                              e.stopPropagation();
                              setDiffEntry(entry);
                            }}
                          >
                            {t('users.permissionAudit.diff.open')}
                          </Button>,
                        ]
                  }
                >
                  <div style={{ width: '100%' }}>
                    <Space wrap size={[8, 4]} style={{ marginBottom: 6 }}>
                      <Typography.Text style={{ fontSize: 12 }}>
                        🕐 {formatTimestampWithOffset(entry.timestamp)}
                      </Typography.Text>
                      <Typography.Text strong style={{ fontSize: 12 }}>
                        👤 {entry.actorName?.trim() || t('users.permissionAudit.systemActor')}
                      </Typography.Text>
                      {actorSub ? (
                        <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                          ({actorSub})
                        </Typography.Text>
                      ) : null}
                      <Tag color={permissionAuditTagColor(color)} style={{ marginInlineEnd: 0 }}>
                        {dedicatedActionLabel(entry.action, t)}
                      </Tag>
                    </Space>

                    {entry.permissionKey ? (
                      <Typography.Text code style={{ fontSize: 12 }}>
                        {entry.permissionKey}
                      </Typography.Text>
                    ) : (
                      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                        {entry.roleName || t('users.permissionAudit.noDiffDetails')}
                      </Typography.Text>
                    )}
                    <div style={{ fontSize: 12, marginTop: 2 }}>
                      {stateLabel(entry.oldValue, t)}
                      {' → '}
                      {stateLabel(entry.newValue, t)}
                    </div>
                    {entry.reason?.trim() ? (
                      <Typography.Paragraph
                        type="secondary"
                        style={{ fontSize: 11, marginBottom: 0, marginTop: 6 }}
                      >
                        {t('users.permissionAudit.reason', { reason: entry.reason.trim() })}
                      </Typography.Paragraph>
                    ) : null}
                  </div>
                </List.Item>
              );
            }}
          />
        )
      ) : legacyEntries.length === 0 ? (
        <Empty
          image={Empty.PRESENTED_IMAGE_SIMPLE}
          description={t('users.permissionAudit.empty')}
        />
      ) : (
        <List
          size="small"
          bordered
          dataSource={legacyPageSlice}
          style={{ maxHeight: 420, overflow: 'auto', background: '#fff' }}
          renderItem={(entry) => {
            const actorName =
              entry.actorDisplayName?.trim() ||
              entry.userRole?.trim() ||
              t('users.permissionAudit.systemActor');
            const actorSub = entry.userId?.trim() || null;
            const diff = buildPermissionAuditDiff(entry);
            const border = permissionAuditBorderColor(diff.color);
            const isSelected = entry.id != null && entry.id === selectedId;
            const entryRevert = resolvePermissionAuditRevert(entry, { roleName, userId });
            const canRevertEntry = canRevert && entryRevert.kind !== 'unsupported';
            const reason = (entry.notes ?? '').trim();

            return (
              <List.Item
                onClick={() => setSelectedId(entry.id ?? null)}
                style={{
                  cursor: 'pointer',
                  borderLeft: `4px solid ${border}`,
                  background: isSelected ? 'rgba(22, 119, 255, 0.06)' : undefined,
                  alignItems: 'flex-start',
                }}
                actions={
                  canRevertEntry
                    ? [
                        <Button
                          key="revert"
                          type="link"
                          size="small"
                          icon={<UndoOutlined />}
                          loading={revertingId === entry.id}
                          onClick={(e) => {
                            e.stopPropagation();
                            runRevertLegacy(entry);
                          }}
                        >
                          {t('users.permissionAudit.revert')}
                        </Button>,
                      ]
                    : undefined
                }
              >
                <div style={{ width: '100%' }}>
                  <Space wrap size={[8, 4]} style={{ marginBottom: 6 }}>
                    <Typography.Text style={{ fontSize: 12 }}>
                      🕐 {formatTimestampWithOffset(entry.timestamp)}
                    </Typography.Text>
                    <Typography.Text strong style={{ fontSize: 12 }}>
                      👤 {actorName}
                    </Typography.Text>
                    {actorSub ? (
                      <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                        ({actorSub})
                      </Typography.Text>
                    ) : null}
                    <Tag
                      color={permissionAuditTagColor(diff.color)}
                      style={{ marginInlineEnd: 0 }}
                    >
                      {legacyActionLabel(entry, t)}
                    </Tag>
                  </Space>
                  {diff.lines.length === 0 ? (
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                      {t('users.permissionAudit.noDiffDetails')}
                    </Typography.Text>
                  ) : (
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                      {diff.lines.map((line, idx) => (
                        <div key={`${entry.id ?? 'e'}-${line.permissionKey ?? 'x'}-${idx}`}>
                          {line.permissionKey ? (
                            <Typography.Text code style={{ fontSize: 12 }}>
                              {line.permissionKey}
                            </Typography.Text>
                          ) : null}
                          <div style={{ fontSize: 12, marginTop: 2 }}>
                            {stateLabel(line.oldState, t)}
                            {' → '}
                            {stateLabel(line.newState, t)}
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                  {reason ? (
                    <Typography.Paragraph
                      type="secondary"
                      style={{ fontSize: 11, marginBottom: 0, marginTop: 6 }}
                    >
                      {t('users.permissionAudit.reason', { reason })}
                    </Typography.Paragraph>
                  ) : null}
                </div>
              </List.Item>
            );
          }}
        />
      )}

      <Space
        wrap
        size={8}
        style={{ marginTop: 12, width: '100%', justifyContent: 'space-between' }}
      >
        <Space wrap size={8}>
          <Button
            icon={<UndoOutlined />}
            disabled={
              useDedicatedApi
                ? !canRevertSelectedDedicated || !selectedDedicated
                : !canRevertSelectedLegacy || !selectedLegacy
            }
            loading={
              revertingId != null &&
              (revertingId === selectedDedicated?.id || revertingId === selectedLegacy?.id)
            }
            onClick={() => {
              if (useDedicatedApi && selectedDedicated) openRevertDedicated(selectedDedicated);
              else if (selectedLegacy) runRevertLegacy(selectedLegacy);
            }}
          >
            {t('users.permissionAudit.revert')}
          </Button>
          <Button loading={exporting} disabled={!canExport} onClick={() => void handleExport()}>
            {t('users.permissionAudit.export')}
          </Button>
          <Button
            disabled={!canViewAudit}
            onClick={() => setReportOpen(true)}
          >
            {t('users.permissionAudit.report.button')}
          </Button>
        </Space>
        <Space size={8}>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {t('users.permissionAudit.pagination', {
              from: rangeFrom,
              to: rangeTo,
              total,
            })}
          </Typography.Text>
          <Button
            size="small"
            icon={<LeftOutlined />}
            disabled={safePage <= 1}
            onClick={() => setPage((p) => Math.max(1, p - 1))}
          />
          <Button
            size="small"
            icon={<RightOutlined />}
            disabled={safePage >= totalPages || total === 0}
            onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
          />
        </Space>
      </Space>

      <PermissionAuditDiffModal
        open={diffEntry != null}
        entry={diffEntry}
        onClose={() => setDiffEntry(null)}
        canRevert={
          canRevert &&
          diffEntry != null &&
          diffEntry.action !== 'created' &&
          diffEntry.action !== 'deleted'
        }
        onRevert={(entry) => openRevertDedicated(entry)}
        onAddNote={handleAddNote}
      />

      <PermissionAuditRevertModal
        open={revertEntry != null}
        entry={revertEntry}
        confirmLoading={revertLoading}
        newerChangesWarning={newerWarning}
        onCancel={() => {
          setRevertEntry(null);
          setNewerWarning(null);
        }}
        onConfirm={(reason, force) => void confirmRevertDedicated(reason, force)}
      />

      <PermissionAuditReportModal
        open={reportOpen}
        onClose={() => setReportOpen(false)}
        canExport={canExport}
        filters={{
          roleId: roleId ?? undefined,
          roleName: roleNameFilter ?? roleName ?? undefined,
          permissionKey: permissionKeyFilter.trim() || undefined,
          actorUserId: actorUserId,
          fromDate: range?.[0]?.toISOString(),
          toDate: range?.[1]?.toISOString(),
        }}
      />
    </div>
  );
}
