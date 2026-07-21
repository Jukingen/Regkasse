'use client';

import { DeleteOutlined, PlusOutlined, SaveOutlined } from '@ant-design/icons';
import {
  Alert,
  Button,
  Card,
  Checkbox,
  Col,
  Drawer,
  Empty,
  Row,
  Select,
  Space,
  Spin,
  Tag,
  Tooltip,
  Typography,
} from 'antd';
/**
 * Role management drawer: left = role list (System + Custom), right = grouped permission checklist.
 * System roles are readonly; custom roles are editable. No legacy/deprecated role category.
 */
import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';

import { SimpleList as List } from '@/components/ui/SimpleList';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n/I18nProvider';
import { validateCatalogAlignment } from '@/shared/auth/validateCatalogAlignment';

import type { PermissionCatalogItemDto, RoleWithPermissionsDto } from '../api/usersGateway';
import { ROLE_PRESETS, type RolePreset, getPresetKeysInCatalog } from '../constants/rolePresets';
import { permissionCatalogGroupToSlug } from '../utils/permissionCatalogGroup';
import { resolvePermissionDisplayLabel } from '../utils/permissionDisplayLabel';

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
  { key: 'reports', summaryKey: 'reports' as const, groupKeys: ['audit_report'] },
  { key: 'cash', summaryKey: 'cashShift' as const, groupKeys: ['cash_shift'] },
  { key: 'customer', summaryKey: 'customer' as const, groupKeys: ['customer'] },
  { key: 'catalog', summaryKey: 'catalog' as const, groupKeys: ['product', 'order_sale'] },
  { key: 'settings', summaryKey: 'settingsAdmin' as const, groupKeys: ['settings', 'user_role'] },
] as const;

function groupCatalogByGroup(
  catalog: PermissionCatalogItemDto[]
): Map<string, PermissionCatalogItemDto[]> {
  const map = new Map<string, PermissionCatalogItemDto[]>();
  for (const item of catalog) {
    const rawGroup = item.group?.trim() || 'Other';
    const slug = permissionCatalogGroupToSlug(rawGroup);
    if (!map.has(slug)) map.set(slug, []);
    map.get(slug)!.push(item);
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
}: Props) {
  const { modal } = useAntdApp();

  const { t } = useI18n();
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
    (groupKey: string) => t(`users.roleDrawer.groups.${groupKey}`),
    [t]
  );

  const getPermissionDisplayLabel = useCallback(
    (code: string) => resolvePermissionDisplayLabel(code, t),
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
      if (pos && has('cash_shift')) return t('users.roleDrawer.capabilityHintPosCash');
      if (pos) return t('users.roleDrawer.capabilityHintPosOnly');
      if (admin && has('audit_report')) return t('users.roleDrawer.capabilityHintAdminReports');
      if (admin && (has('user_role') || has('settings') || has('system')))
        return t('users.roleDrawer.capabilityHintAdminFull');
      if (admin) return t('users.roleDrawer.capabilityHintAdminCatalog');
      if (has('audit_report')) return t('users.roleDrawer.summary.reports');
      if (has('cash_shift')) return t('users.roleDrawer.summary.cashShift');
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
      const labelA = t(`users.roleDrawer.groups.${slugA}`);
      const labelB = t(`users.roleDrawer.groups.${slugB}`);
      return labelA.localeCompare(labelB, undefined, { sensitivity: 'base' });
    });
  }, [groupedCatalog, t]);

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
        },
      });
      return;
    }
    setSelectedRoleName(roleName);
    syncDraftToRole(roleName);
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

  const handleSave = async () => {
    if (!selectedRoleName || !canEditRole || !dirty) return;
    await onSavePermissions(selectedRoleName, Array.from(draftPermissions));
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
      {canEditRolePermissions && (
        <Button
          type="primary"
          icon={<SaveOutlined />}
          onClick={handleSave}
          disabled={!dirty || !canEditRole || !selectedRoleName}
          loading={saveLoading}
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
              {canEditRolePermissions && selectedRoleName && canEditRole && (
                <Select
                  placeholder={t('users.roleDrawer.presetPlaceholder')}
                  style={{ minWidth: 180 }}
                  value={presetSelectValue}
                  onChange={(presetId: string) => {
                    const preset = ROLE_PRESETS.find((p) => p.id === presetId);
                    if (preset) handleApplyPreset(preset);
                  }}
                  options={presetOptions}
                  allowClear
                />
              )}
            </div>
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
                <div style={{ marginTop: 8, maxHeight: 360, overflow: 'auto' }}>
                  {groupedCatalogEntries.length === 0 ? (
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                      {t('users.roleDrawer.noPermissionsInGroup')}
                    </Typography.Text>
                  ) : (
                    groupedCatalogEntries.map(([groupSlug, items]) => (
                      <div
                        key={groupSlug}
                        style={{
                          marginBottom: 16,
                          padding: '10px 12px',
                          background: '#fafafa',
                          borderRadius: 6,
                          border: '1px solid #f0f0f0',
                        }}
                      >
                        <Typography.Text
                          type="secondary"
                          style={{
                            fontSize: 12,
                            display: 'block',
                            marginBottom: 6,
                            fontWeight: 500,
                          }}
                        >
                          {t(`users.roleDrawer.groups.${groupSlug}`)}
                        </Typography.Text>
                        <Space orientation="vertical" size={2} style={{ width: '100%' }}>
                          {items.map((item) => (
                            <Checkbox
                              key={item.key}
                              checked={draftPermissions.has(item.key)}
                              onChange={(e) => handleTogglePermission(item.key, e.target.checked)}
                              disabled={!canEditRole}
                            >
                              <Tooltip title={item.key}>
                                <Typography.Text style={{ fontSize: 13 }}>
                                  {getPermissionDisplayLabel(item.key)}
                                </Typography.Text>
                              </Tooltip>
                            </Checkbox>
                          ))}
                        </Space>
                      </div>
                    ))
                  )}
                </div>
              </>
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
    </Drawer>
  );
}
