'use client';

/**
 * Role management drawer: left = role list (three IA groups), right = grouped permission checklist.
 * Groups: System (Canonical) — readonly; Custom — editable per backend; Legacy/Deprecated — same edit rules as custom but flagged for migration.
 * State/effects unchanged; grouping is informational only except legacy warning banner.
 */
import React, { useMemo, useState, useEffect, useCallback, useRef } from 'react';
import {
  Drawer,
  List,
  Button,
  Space,
  Spin,
  Alert,
  Empty,
  Checkbox,
  Tooltip,
  Typography,
  Modal,
  Select,
  Tag,
} from 'antd';
import { PlusOutlined, DeleteOutlined, SaveOutlined } from '@ant-design/icons';
import type { RoleWithPermissionsDto } from '../api/usersGateway';
import type { PermissionCatalogItemDto } from '../api/usersGateway';
import { usersCopy } from '../constants/copy';
import { ROLE_PRESETS, getPresetKeysInCatalog, type RolePreset } from '../constants/rolePresets';
import { validateCatalogAlignment } from '@/shared/auth/validateCatalogAlignment';

/** Stable empty refs so default props do not create new [] each render (avoids effect dependency churn). */
const EMPTY_ROLES: RoleWithPermissionsDto[] = [];
const EMPTY_CATALOG: PermissionCatalogItemDto[] = [];

/**
 * Legacy/deprecated role names still present in AspNetRoles until migrated.
 * Align with backend ReservedRoleNames / RoleLegacyMapping where applicable; extend as needed.
 */
const LEGACY_ROLE_NAMES = new Set(
  ['Demo', 'BranchManager', 'Auditor', 'Admin', 'Administrator', 'Kellner'].map((n) => n.toLowerCase())
);

function isLegacyRoleName(roleName: string): boolean {
  if (!roleName) return false;
  return LEGACY_ROLE_NAMES.has(roleName.trim().toLowerCase());
}

function setsEqual(a: Set<string>, b: Set<string>): boolean {
  if (a.size !== b.size) return false;
  let equal = true;
  a.forEach((x) => {
    if (!b.has(x)) equal = false;
  });
  return equal;
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
};

function groupCatalogByGroup(catalog: PermissionCatalogItemDto[]): Map<string, PermissionCatalogItemDto[]> {
  const map = new Map<string, PermissionCatalogItemDto[]>();
  for (const item of catalog) {
    const g = item.group || 'Sonstige';
    if (!map.has(g)) map.set(g, []);
    map.get(g)!.push(item);
  }
  return map;
}

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
}: Props) {
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

  // Three-way IA grouping; selection/effects still keyed by roleName only. System = canonical; legacy = name in LEGACY_ROLE_NAMES; custom = rest.
  const systemRolesList = useMemo(
    () => sortedRoles.filter((r) => r.isSystemRole || r.isImmutable),
    [sortedRoles]
  );
  const legacyRolesList = useMemo(
    () =>
      sortedRoles.filter(
        (r) =>
          !(r.isSystemRole || r.isImmutable) && isLegacyRoleName(r.roleName)
      ),
    [sortedRoles]
  );
  const customRolesList = useMemo(
    () =>
      sortedRoles.filter(
        (r) =>
          !(r.isSystemRole || r.isImmutable) && !isLegacyRoleName(r.roleName)
      ),
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

  // Legacy section + warning only; edit/delete still follow backend DTO (same as custom if not system).
  const selectedIsLegacy =
    !!selectedRole &&
    !selectedRole.isSystemRole &&
    !selectedRole.isImmutable &&
    isLegacyRoleName(selectedRole.roleName);

  const savedPermissionsSet = useMemo(
    () => new Set(selectedRole?.permissions ?? []),
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
    if (!open) return;
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
  }, [open, roleNamesKey, selectedRoleName]);

  // --- Effect 2: sync draft from server. Deps exclude `roles` array — selectedRolePermissionsKey already encodes permission content; prevents loop when React Query returns new roles reference with same data. ---
  useEffect(() => {
    if (!open || !selectedRoleName) return;
    const role = rolesRef.current.find((r) => r.roleName === selectedRoleName);
    if (!role) return;
    const next = new Set(role.permissions ?? []);
    setDraftPermissions((prev) => (setsEqual(prev, next) ? prev : next));
  }, [open, selectedRoleName, selectedRolePermissionsKey]);

  // Primitive fingerprint for catalog keys so effect does not re-run every render (Set reference unstable).
  const catalogKeysFingerprint = useMemo(() => {
    if (catalog.length === 0) return '';
    return [...catalog.map((c) => c.key)].sort().join(',');
  }, [catalog]);

  useEffect(() => {
    if (!open || catalogKeysFingerprint.length === 0) return;
    const keys = catalogKeysFingerprint.split(',');
    validateCatalogAlignment(keys, { warnUnknown: true });
  }, [open, catalogKeysFingerprint]);

  const groupedCatalog = useMemo(() => groupCatalogByGroup(catalog), [catalog]);
  // Set from fingerprint avoids depending on catalog array reference (React Query refetch).
  const catalogKeySet = useMemo(() => {
    if (!catalogKeysFingerprint) return new Set<string>();
    return new Set(catalogKeysFingerprint.split(','));
  }, [catalogKeysFingerprint]);

  const groupedCatalogEntries = useMemo(() => {
    return Array.from(groupedCatalog.entries()).sort(([a], [b]) => a.localeCompare(b, 'de'));
  }, [groupedCatalog]);

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
      Modal.confirm({
        title: usersCopy.confirmCloseWithDirty,
        okText: 'Verwerfen',
        cancelText: 'Dranbleiben',
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
      Modal.confirm({
        title: usersCopy.confirmCloseWithDirty,
        okText: 'Verwerfen',
        cancelText: 'Dranbleiben',
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
      Modal.warning({
        title: usersCopy.roleHasUsers,
        content: usersCopy.roleDeleteBlockedReassignFirst,
      });
      return;
    }
    Modal.confirm({
      title: usersCopy.deleteRole,
      content: `„${selectedRoleName}" wirklich löschen?`,
      okText: usersCopy.deleteRole,
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

  const deleteButtonTooltip =
    !selectedRoleName
      ? undefined
      : isSystemRole
        ? usersCopy.systemRoleProtectedNoDelete
        : (selectedRole?.userCount ?? 0) > 0
          ? usersCopy.roleDeleteBlockedReassignFirst
          : undefined;

  const loading = rolesLoading || catalogLoading;
  const error = rolesError || catalogError;

  return (
    <Drawer
      title={usersCopy.manageRoles}
      placement="right"
      width={720}
      open={open}
      onClose={handleClose}
      destroyOnHidden
      footer={
        <Space>
          {canCreateRole && (
            <Button icon={<PlusOutlined />} onClick={onCreateRole}>
              {usersCopy.newRole}
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
                  {usersCopy.deleteRole}
                </Button>
              </span>
            </Tooltip>
          )}
          <span style={{ flex: 1 }} />
          {canEditRolePermissions && (
            <Button
              type="primary"
              icon={<SaveOutlined />}
              onClick={handleSave}
              disabled={!dirty || !canEditRole || !selectedRoleName}
              loading={saveLoading}
            >
              {usersCopy.savePermissions}
            </Button>
          )}
        </Space>
      }
    >
      <Typography.Paragraph type="secondary" style={{ marginBottom: 16 }}>
        {usersCopy.manageRolesDescription}
      </Typography.Paragraph>

      {error && (
        <Alert
          type="error"
          message={usersCopy.errorLoad}
          action={
            <Button size="small" onClick={onRetry}>
              {usersCopy.retry}
            </Button>
          }
          style={{ marginBottom: 16 }}
        />
      )}

      {loading ? (
        <div style={{ display: 'flex', justifyContent: 'center', padding: 48 }}>
          <Spin spinning tip="Laden…">
            <div style={{ minHeight: 80 }} />
          </Spin>
        </div>
      ) : (
        <div style={{ display: 'flex', gap: 24, minHeight: 400 }}>
          <div style={{ width: 220, flexShrink: 0, borderRight: '1px solid #f0f0f0', paddingRight: 16 }}>
            <Typography.Text strong>{usersCopy.role}</Typography.Text>
            {sortedRoles.length === 0 ? (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={usersCopy.noRoleSelected} style={{ marginTop: 8 }} />
            ) : (
              <div style={{ marginTop: 8 }}>
                {systemRolesList.length > 0 && (
                  <>
                    <Typography.Text type="secondary" style={{ fontSize: 11, display: 'block', marginBottom: 4 }}>
                      {usersCopy.systemRolesSection}
                    </Typography.Text>
                    <Typography.Text type="secondary" style={{ fontSize: 10, display: 'block', marginBottom: 6, opacity: 0.85 }}>
                      {usersCopy.systemRolesSectionHint}
                    </Typography.Text>
                    <List
                      size="small"
                      dataSource={systemRolesList}
                      renderItem={(r) => (
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
                            <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
                              <span>{usersCopy.roleDisplayName(r.roleName)}</span>
                              <Tag color="blue" style={{ margin: 0, fontSize: 11 }}>
                                {usersCopy.badgeSystemRole}
                              </Tag>
                            </div>
                            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                              {usersCopy.userCount(r.userCount)}
                            </Typography.Text>
                          </div>
                        </List.Item>
                      )}
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
                        marginTop: systemRolesList.length > 0 || legacyRolesList.length > 0 ? 14 : 0,
                        marginBottom: 4,
                      }}
                    >
                      {usersCopy.customRolesSection}
                    </Typography.Text>
                    <Typography.Text type="secondary" style={{ fontSize: 10, display: 'block', marginBottom: 6, opacity: 0.85 }}>
                      {usersCopy.customRolesSectionHint}
                    </Typography.Text>
                    <List
                      size="small"
                      dataSource={customRolesList}
                      renderItem={(r) => (
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
                            <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
                              <span>{usersCopy.roleDisplayName(r.roleName)}</span>
                              <Tag color="default" style={{ margin: 0, fontSize: 11 }}>
                                {usersCopy.badgeCustomRole}
                              </Tag>
                            </div>
                            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                              {usersCopy.userCount(r.userCount)}
                            </Typography.Text>
                          </div>
                        </List.Item>
                      )}
                    />
                  </>
                )}
                {legacyRolesList.length > 0 && (
                  <>
                    <Typography.Text
                      type="secondary"
                      style={{
                        fontSize: 11,
                        display: 'block',
                        marginTop: systemRolesList.length > 0 || customRolesList.length > 0 ? 14 : 0,
                        marginBottom: 4,
                      }}
                    >
                      {usersCopy.legacyRolesSection}
                    </Typography.Text>
                    <Typography.Text type="secondary" style={{ fontSize: 10, display: 'block', marginBottom: 6, opacity: 0.85 }}>
                      {usersCopy.legacyRolesSectionHint}
                    </Typography.Text>
                    <List
                      size="small"
                      dataSource={legacyRolesList}
                      renderItem={(r) => (
                        <List.Item
                          key={r.roleName}
                          style={{
                            cursor: 'pointer',
                            background: r.roleName === selectedRoleName ? '#fff7e6' : undefined,
                            borderRadius: 4,
                            padding: '4px 8px',
                          }}
                          onClick={() => handleSelectRole(r.roleName)}
                        >
                          <div style={{ width: '100%' }}>
                            <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
                              <span>{usersCopy.roleDisplayName(r.roleName)}</span>
                              <Tag color="orange" style={{ margin: 0, fontSize: 11 }}>
                                {usersCopy.badgeLegacyRole}
                              </Tag>
                            </div>
                            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                              {usersCopy.userCount(r.userCount)}
                            </Typography.Text>
                          </div>
                        </List.Item>
                      )}
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
              <Typography.Text strong>{usersCopy.permissionsByGroup}</Typography.Text>
              {canEditRolePermissions && selectedRoleName && canEditRole && (
                <Select
                  placeholder={usersCopy.presetPlaceholder}
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
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={usersCopy.noRoleSelected} style={{ marginTop: 24 }} />
            ) : (
              <>
                {(isSystemRole || selectedRole?.isImmutable) && (
                  <Alert
                    type="info"
                    message={usersCopy.badgeSystemRole}
                    description={usersCopy.systemRoleImmutableInfo}
                    showIcon
                    style={{ marginBottom: 12 }}
                  />
                )}
                {selectedIsLegacy && (
                  <Alert
                    type="warning"
                    message={usersCopy.legacyRoleWarningMessage}
                    description={usersCopy.legacyRoleWarningDescription}
                    showIcon
                    style={{ marginBottom: 12 }}
                  />
                )}
                {selectedRole && !selectedRole.isSystemRole && !selectedRole.isImmutable && selectedRole.userCount > 0 && (
                  <Alert
                    type="info"
                    message={usersCopy.roleHasUsers}
                    description={usersCopy.roleDeleteBlockedReassignFirst}
                    showIcon
                    style={{ marginBottom: 12 }}
                  />
                )}
                <div style={{ marginTop: 8, maxHeight: 420, overflow: 'auto' }}>
                  {groupedCatalogEntries.map(([groupName, items]) => (
                    <div key={groupName} style={{ marginBottom: 16 }}>
                      <Typography.Text type="secondary" style={{ fontSize: 12, display: 'block', marginBottom: 6 }}>
                        {groupName}
                      </Typography.Text>
                      <Space direction="vertical" size={2} style={{ width: '100%' }}>
                        {items.map((item) => (
                          <Checkbox
                            key={item.key}
                            checked={draftPermissions.has(item.key)}
                            onChange={(e) => handleTogglePermission(item.key, e.target.checked)}
                            disabled={!canEditRole}
                          >
                            <Typography.Text style={{ fontSize: 13 }}>{item.key}</Typography.Text>
                          </Checkbox>
                        ))}
                      </Space>
                    </div>
                  ))}
                </div>
              </>
            )}
          </div>
        </div>
      )}
    </Drawer>
  );
}
