'use client';

/**
 * Role management drawer: left = role list, right = grouped permission checklist.
 * Top actions: new role, delete role, save. Dirty state and confirm on close/switch.
 */
import React, { useMemo, useState, useEffect } from 'react';
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
  roles = [],
  catalog = [],
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
  const [selectedRoleName, setSelectedRoleName] = useState<string | null>(null);
  const [draftPermissions, setDraftPermissions] = useState<Set<string>>(new Set());
  const [presetSelectValue, setPresetSelectValue] = useState<string | null>(null);

  const selectedRole = useMemo(
    () => roles.find((r) => r.roleName === selectedRoleName),
    [roles, selectedRoleName]
  );
  const isSystemRole = selectedRole?.isSystemRole ?? false;
  const selectedRoleCanDelete = selectedRole?.canDelete ?? (!isSystemRole && (selectedRole?.userCount ?? 0) === 0);
  const canEditRole = selectedRole?.canEditPermissions ?? !isSystemRole;
  const savedPermissionsSet = useMemo(
    () => new Set(selectedRole?.permissions ?? []),
    [selectedRole]
  );
  const dirty = useMemo(() => {
    if (!selectedRoleName || draftPermissions.size !== savedPermissionsSet.size) return true;
    const draftArr = Array.from(draftPermissions);
    const savedArr = Array.from(savedPermissionsSet);
    return !draftArr.every((p) => savedPermissionsSet.has(p)) || !savedArr.every((p) => draftPermissions.has(p));
  }, [selectedRoleName, draftPermissions, savedPermissionsSet]);

  // Stable key for "selected role's permissions": only changes when role or its permissions content change.
  // Used as effect dependency to avoid re-running when `roles` array reference changes (e.g. React Query).
  const selectedRolePermissionsKey = useMemo(() => {
    const r = roles.find((rr) => rr.roleName === selectedRoleName);
    return r ? [...(r.permissions ?? [])].sort().join(',') : '';
  }, [roles, selectedRoleName]);

  // Initialize selected to first role when data loads; sync draft from selected role
  useEffect(() => {
    if (!open || sortedRoles.length === 0) {
      setSelectedRoleName(null);
      setDraftPermissions(new Set());
      return;
    }
    if (!selectedRoleName || !sortedRoles.some((r) => r.roleName === selectedRoleName)) {
      setSelectedRoleName(sortedRoles[0]!.roleName);
    }
  }, [open, sortedRoles, selectedRoleName]);

  // Sync draft from server when selected role (by name) or its permissions change. Use primitive key
  // instead of selectedRole object so we don't re-run on every new roles array reference (avoids loop).
  useEffect(() => {
    if (!selectedRoleName || !selectedRolePermissionsKey) return;
    const role = roles.find((r) => r.roleName === selectedRoleName);
    if (!role) return;
    setDraftPermissions(new Set(role.permissions ?? []));
  }, [selectedRoleName, selectedRolePermissionsKey]);

  const groupedCatalog = useMemo(() => groupCatalogByGroup(catalog), [catalog]);
  const catalogKeySet = useMemo(() => new Set(catalog.map((c) => c.key)), [catalog]);

  // Warn when menu/route permission keys are missing from catalog (contract alignment)
  useEffect(() => {
    if (!open || catalog.length === 0) return;
    validateCatalogAlignment(Array.from(catalogKeySet), { warnUnknown: true });
  }, [open, catalog.length, catalogKeySet]);

  const handleApplyPreset = (preset: RolePreset) => {
    if (isSystemRole) return;
    const keysInCatalog = getPresetKeysInCatalog(preset, catalogKeySet);
    setDraftPermissions(new Set(keysInCatalog));
    setPresetSelectValue(null);
  };

  const handleSelectRole = (roleName: string) => {
    if (roleName === selectedRoleName) return;
    if (dirty) {
      Modal.confirm({
        title: usersCopy.confirmCloseWithDirty,
        okText: 'Verwerfen',
        cancelText: 'Dranbleiben',
        onOk: () => {
          setSelectedRoleName(roleName);
          const next = roles.find((r) => r.roleName === roleName);
          setDraftPermissions(new Set(next?.permissions ?? []));
        },
      });
      return;
    }
    setSelectedRoleName(roleName);
    const next = roles.find((r) => r.roleName === roleName);
    setDraftPermissions(new Set(next?.permissions ?? []));
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
    if (isSystemRole) return;
    setDraftPermissions((prev) => {
      const next = new Set(prev);
      if (checked) next.add(key);
      else next.delete(key);
      return next;
    });
  };

  const handleSave = async () => {
    if (!selectedRoleName || isSystemRole || !dirty) return;
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
        setSelectedRoleName(remaining[0]?.roleName ?? null);
        setDraftPermissions(new Set(remaining[0]?.permissions ?? []));
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
      destroyOnClose
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
          action={<Button size="small" onClick={onRetry}>{usersCopy.retry}</Button>}
          style={{ marginBottom: 16 }}
        />
      )}

      {loading ? (
        <div style={{ display: 'flex', justifyContent: 'center', padding: 48 }}>
          <Spin tip="Laden…" />
        </div>
      ) : (
        <div style={{ display: 'flex', gap: 24, minHeight: 400 }}>
          {/* Left: role list */}
          <div style={{ width: 220, flexShrink: 0, borderRight: '1px solid #f0f0f0', paddingRight: 16 }}>
            <Typography.Text strong>{usersCopy.role}</Typography.Text>
            {sortedRoles.length === 0 ? (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={usersCopy.noRoleSelected} style={{ marginTop: 8 }} />
            ) : (
              <List
                size="small"
                dataSource={sortedRoles}
                style={{ marginTop: 8 }}
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
                        <Tag color={r.isSystemRole ? 'blue' : 'default'} style={{ margin: 0, fontSize: 11 }}>
                          {r.isSystemRole ? usersCopy.badgeSystemRole : usersCopy.badgeCustomRole}
                        </Tag>
                      </div>
                      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                        {usersCopy.userCount(r.userCount)}
                      </Typography.Text>
                    </div>
                  </List.Item>
                )}
              />
            )}
          </div>

          {/* Right: grouped permissions */}
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 8, marginBottom: 8 }}>
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
                  options={ROLE_PRESETS.map((p) => ({ value: p.id, label: p.label }))}
                  allowClear
                />
              )}
            </div>
            {!selectedRoleName ? (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={usersCopy.noRoleSelected} style={{ marginTop: 24 }} />
            ) : (
              <>
                {selectedRole?.isSystemRole && (
                  <Alert
                    type="info"
                    message={usersCopy.badgeSystemRole}
                    description={usersCopy.systemRoleProtectedNoDelete}
                    showIcon
                    style={{ marginBottom: 12 }}
                  />
                )}
                {selectedRole && !selectedRole.isSystemRole && selectedRole.userCount > 0 && (
                  <Alert
                    type="info"
                    message={usersCopy.roleHasUsers}
                    description={usersCopy.roleDeleteBlockedReassignFirst}
                    showIcon
                    style={{ marginBottom: 12 }}
                  />
                )}
                <div style={{ marginTop: 8, maxHeight: 420, overflow: 'auto' }}>
                {Array.from(groupedCatalog.entries()).sort(([a], [b]) => a.localeCompare(b)).map(([groupName, items]) => (
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
