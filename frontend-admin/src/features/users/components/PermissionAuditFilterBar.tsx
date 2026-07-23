'use client';

import {
  FilterOutlined,
  SaveOutlined,
  ShareAltOutlined,
  DeleteOutlined,
} from '@ant-design/icons';
import {
  Button,
  DatePicker,
  Dropdown,
  Input,
  Modal,
  Select,
  Space,
  Tag,
  Typography,
} from 'antd';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import React, { useCallback, useMemo, useState } from 'react';

import type { PermissionAuditEntry } from '@/features/audit/api/permissionAudit';
import type { PermissionAuditQuickFilter } from '@/features/users/utils/permissionAuditFilters';
import {
  createSavedPermissionAuditFilterId,
  decodePermissionAuditFilterShare,
  deletePermissionAuditFilter,
  encodePermissionAuditFilterShare,
  loadPersonalPermissionAuditFilters,
  loadSharedPermissionAuditFilters,
  savePermissionAuditFilter,
  type SavedPermissionAuditFilter,
} from '@/features/users/utils/permissionAuditSavedFilters';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';

const { RangePicker } = DatePicker;

export type PermissionAuditFilterBarProps = {
  range: [Dayjs | null, Dayjs | null] | null;
  onRangeChange: (v: [Dayjs | null, Dayjs | null] | null) => void;
  actorUserId?: string;
  onActorChange: (userId: string | undefined) => void;
  actorOptions: { value: string; label: string }[];
  actorsLoading?: boolean;
  permissionKey: string;
  onPermissionKeyChange: (v: string) => void;
  roleNameFilter?: string;
  onRoleNameChange: (v: string | undefined) => void;
  roleOptions: { value: string; label: string }[];
  roleFilterLocked?: boolean;
  actionFilter: PermissionAuditEntry['action'] | 'all' | string;
  onActionChange: (v: PermissionAuditEntry['action'] | 'all' | string) => void;
  /** Dedicated permission API vs legacy override audit actions. */
  actionMode?: 'dedicated' | 'legacy';
  search: string;
  onSearchChange: (v: string) => void;
  quickFilters: PermissionAuditQuickFilter[];
  onQuickFiltersChange: (next: PermissionAuditQuickFilter[]) => void;
  currentUserId?: string | null;
  tenantId?: string | null;
  currentUserName?: string | null;
  /** Snapshot used when saving a filter */
  buildFilterSnapshot: () => SavedPermissionAuditFilter['filters'];
  onApplySavedFilters: (filters: SavedPermissionAuditFilter['filters']) => void;
};

function toggleQuick(
  current: PermissionAuditQuickFilter[],
  key: PermissionAuditQuickFilter
): PermissionAuditQuickFilter[] {
  return current.includes(key) ? current.filter((k) => k !== key) : [...current, key];
}

export function PermissionAuditFilterBar({
  range,
  onRangeChange,
  actorUserId,
  onActorChange,
  actorOptions,
  actorsLoading,
  permissionKey,
  onPermissionKeyChange,
  roleNameFilter,
  onRoleNameChange,
  roleOptions,
  roleFilterLocked,
  actionFilter,
  onActionChange,
  actionMode = 'dedicated',
  search,
  onSearchChange,
  quickFilters,
  onQuickFiltersChange,
  currentUserId,
  tenantId,
  currentUserName,
  buildFilterSnapshot,
  onApplySavedFilters,
}: PermissionAuditFilterBarProps) {
  const { t } = useI18n();
  const { message, modal } = useAntdApp();
  const [saveOpen, setSaveOpen] = useState(false);
  const [saveName, setSaveName] = useState('');
  const [saveShared, setSaveShared] = useState(false);
  const [importOpen, setImportOpen] = useState(false);
  const [importToken, setImportToken] = useState('');
  const [savedTick, setSavedTick] = useState(0);

  const personal = useMemo(() => {
    void savedTick;
    return loadPersonalPermissionAuditFilters(currentUserId ?? '');
  }, [currentUserId, savedTick]);

  const shared = useMemo(() => {
    void savedTick;
    return loadSharedPermissionAuditFilters(tenantId ?? 'default');
  }, [tenantId, savedTick]);

  const actionOptions = useMemo(() => {
    if (actionMode === 'legacy') {
      return [
        { value: 'all', label: t('users.permissionAudit.filterActionAll') },
        {
          value: 'USER_PERMISSION_OVERRIDES_CHANGED',
          label: t('users.permissionAudit.actions.USER_PERMISSION_OVERRIDES_CHANGED'),
        },
      ];
    }
    return [
      { value: 'all' as const, label: t('users.permissionAudit.filterActionAll') },
      ...(['created', 'updated', 'deleted', 'reverted'] as const).map((a) => ({
        value: a,
        label: t(`users.permissionAudit.actions.${a}`),
      })),
    ];
  }, [t, actionMode]);

  const refreshSaved = useCallback(() => setSavedTick((n) => n + 1), []);

  const handleSave = useCallback(() => {
    const name = saveName.trim();
    if (!name) {
      message.warning(t('users.permissionAudit.savedFilters.nameRequired'));
      return;
    }
    if (!currentUserId) {
      message.error(t('users.permissionAudit.savedFilters.saveError'));
      return;
    }
    const record: SavedPermissionAuditFilter = {
      id: createSavedPermissionAuditFilterId(),
      name,
      shared: saveShared,
      filters: buildFilterSnapshot(),
      createdAt: new Date().toISOString(),
      createdByUserId: currentUserId,
      createdByName: currentUserName ?? null,
    };
    savePermissionAuditFilter(record, {
      userId: currentUserId,
      tenantId: tenantId ?? 'default',
    });
    message.success(
      saveShared
        ? t('users.permissionAudit.savedFilters.sharedSuccess')
        : t('users.permissionAudit.savedFilters.saveSuccess')
    );
    setSaveOpen(false);
    setSaveName('');
    setSaveShared(false);
    refreshSaved();
  }, [
    saveName,
    saveShared,
    currentUserId,
    currentUserName,
    tenantId,
    buildFilterSnapshot,
    message,
    t,
    refreshSaved,
  ]);

  const handleShareClipboard = useCallback(
    async (filter: SavedPermissionAuditFilter) => {
      try {
        const token = encodePermissionAuditFilterShare(filter);
        await navigator.clipboard.writeText(token);
        message.success(t('users.permissionAudit.savedFilters.shareCopied'));
      } catch {
        message.error(t('users.permissionAudit.savedFilters.shareError'));
      }
    },
    [message, t]
  );

  const handleImport = useCallback(() => {
    const decoded = decodePermissionAuditFilterShare(importToken);
    if (!decoded || !currentUserId) {
      message.error(t('users.permissionAudit.savedFilters.importError'));
      return;
    }
    const imported: SavedPermissionAuditFilter = {
      ...decoded,
      id: createSavedPermissionAuditFilterId(),
      shared: false,
      createdAt: new Date().toISOString(),
      createdByUserId: currentUserId,
      createdByName: currentUserName ?? null,
    };
    savePermissionAuditFilter(imported, {
      userId: currentUserId,
      tenantId: tenantId ?? 'default',
    });
    onApplySavedFilters(imported.filters);
    message.success(t('users.permissionAudit.savedFilters.importSuccess'));
    setImportOpen(false);
    setImportToken('');
    refreshSaved();
  }, [
    importToken,
    currentUserId,
    currentUserName,
    tenantId,
    onApplySavedFilters,
    message,
    t,
    refreshSaved,
  ]);

  const savedMenuItems = useMemo(() => {
    const applyItem = (f: SavedPermissionAuditFilter) => ({
      key: f.id,
      label: (
        <Space style={{ width: '100%', justifyContent: 'space-between' }}>
          <span>
            {f.name}
            {f.shared ? (
              <Tag style={{ marginLeft: 6 }} color="blue">
                {t('users.permissionAudit.savedFilters.sharedBadge')}
              </Tag>
            ) : null}
          </span>
          <Space size={4} onClick={(e) => e.stopPropagation()}>
            <Button
              type="text"
              size="small"
              icon={<ShareAltOutlined />}
              onClick={() => void handleShareClipboard(f)}
            />
            <Button
              type="text"
              size="small"
              danger
              icon={<DeleteOutlined />}
              onClick={() => {
                modal.confirm({
                  title: t('users.permissionAudit.savedFilters.deleteConfirm'),
                  onOk: () => {
                    if (!currentUserId) return;
                    deletePermissionAuditFilter(f.id, {
                      userId: currentUserId,
                      tenantId: tenantId ?? 'default',
                      shared: f.shared,
                    });
                    refreshSaved();
                    message.success(t('users.permissionAudit.savedFilters.deleteSuccess'));
                  },
                });
              }}
            />
          </Space>
        </Space>
      ),
      onClick: () => onApplySavedFilters(f.filters),
    });

    const items: {
      key: string;
      type?: 'group';
      label?: React.ReactNode;
      children?: ReturnType<typeof applyItem>[];
      onClick?: () => void;
    }[] = [];

    if (personal.length) {
      items.push({
        key: 'personal-group',
        type: 'group',
        label: t('users.permissionAudit.savedFilters.personalGroup'),
        children: personal.map(applyItem),
      });
    }
    if (shared.length) {
      items.push({
        key: 'shared-group',
        type: 'group',
        label: t('users.permissionAudit.savedFilters.sharedGroup'),
        children: shared.map(applyItem),
      });
    }
    if (!personal.length && !shared.length) {
      items.push({
        key: 'empty',
        label: t('users.permissionAudit.savedFilters.empty'),
      });
    }
    items.push({
      key: 'save',
      label: t('users.permissionAudit.savedFilters.saveCurrent'),
      onClick: () => setSaveOpen(true),
    });
    items.push({
      key: 'import',
      label: t('users.permissionAudit.savedFilters.import'),
      onClick: () => setImportOpen(true),
    });
    return items;
  }, [
    personal,
    shared,
    t,
    handleShareClipboard,
    onApplySavedFilters,
    modal,
    currentUserId,
    tenantId,
    refreshSaved,
    message,
  ]);

  const quickDefs: { key: PermissionAuditQuickFilter; label: string }[] = [
    { key: 'last24h', label: t('users.permissionAudit.quick.last24h') },
    { key: 'onlyMine', label: t('users.permissionAudit.quick.onlyMine') },
    { key: 'onlyCritical', label: t('users.permissionAudit.quick.onlyCritical') },
  ];

  return (
    <div style={{ marginBottom: 12 }}>
      <Space wrap size={8} style={{ width: '100%', marginBottom: 8 }}>
        <RangePicker
          value={range}
          onChange={(v) => onRangeChange(v)}
          allowClear
          showTime={{ format: 'HH:mm' }}
          format="DD.MM.YYYY HH:mm"
          placeholder={[t('users.permissionAudit.filterDate'), t('users.permissionAudit.filterDate')]}
        />
        <Select
          allowClear
          showSearch
          optionFilterProp="label"
          loading={actorsLoading}
          placeholder={t('users.permissionAudit.filterActorPlaceholder')}
          style={{ minWidth: 200 }}
          value={actorUserId}
          options={actorOptions}
          onChange={(v) => onActorChange(v || undefined)}
        />
        <Select
          allowClear={!roleFilterLocked}
          showSearch
          optionFilterProp="label"
          disabled={roleFilterLocked}
          placeholder={t('users.permissionAudit.filterRolePlaceholder')}
          style={{ minWidth: 180 }}
          value={roleNameFilter}
          options={roleOptions}
          onChange={(v) => onRoleNameChange(v || undefined)}
        />
        <Select
          style={{ minWidth: 200 }}
          value={actionFilter}
          options={actionOptions}
          onChange={(v) => onActionChange(v)}
          placeholder={t('users.permissionAudit.filterAction')}
        />
        <Input
          allowClear
          placeholder={t('users.permissionAudit.filterPermissionKeyPlaceholder')}
          style={{ width: 180 }}
          value={permissionKey}
          onChange={(e) => onPermissionKeyChange(e.target.value)}
        />
        <Input.Search
          allowClear
          placeholder={t('users.permissionAudit.filterSearchPlaceholder')}
          style={{ width: 240 }}
          value={search}
          onChange={(e) => onSearchChange(e.target.value)}
        />
        <Dropdown menu={{ items: savedMenuItems }} trigger={['click']}>
          <Button icon={<FilterOutlined />}>
            {t('users.permissionAudit.savedFilters.menu')}
          </Button>
        </Dropdown>
      </Space>

      <Space wrap size={8}>
        {quickDefs.map((q) => {
          const active = quickFilters.includes(q.key);
          return (
            <Tag.CheckableTag
              key={q.key}
              checked={active}
              onChange={() => {
                const next = toggleQuick(quickFilters, q.key);
                onQuickFiltersChange(next);
                if (q.key === 'last24h' && !active) {
                  onRangeChange([dayjs().subtract(24, 'hour'), dayjs()]);
                }
                if (q.key === 'last24h' && active) {
                  onRangeChange(null);
                }
                if (q.key === 'onlyMine' && !active && currentUserId) {
                  onActorChange(currentUserId);
                }
                if (q.key === 'onlyMine' && active) {
                  onActorChange(undefined);
                }
              }}
            >
              {q.label}
            </Tag.CheckableTag>
          );
        })}
      </Space>

      <Modal
        title={t('users.permissionAudit.savedFilters.saveTitle')}
        open={saveOpen}
        onCancel={() => setSaveOpen(false)}
        onOk={handleSave}
        okText={t('users.permissionAudit.savedFilters.save')}
        destroyOnHidden
      >
        <Space direction="vertical" style={{ width: '100%' }}>
          <Input
            placeholder={t('users.permissionAudit.savedFilters.namePlaceholder')}
            value={saveName}
            onChange={(e) => setSaveName(e.target.value)}
            prefix={<SaveOutlined />}
          />
          <label>
            <input
              type="checkbox"
              checked={saveShared}
              onChange={(e) => setSaveShared(e.target.checked)}
              style={{ marginRight: 8 }}
            />
            {t('users.permissionAudit.savedFilters.shareWithAdmins')}
          </label>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {t('users.permissionAudit.savedFilters.shareHint')}
          </Typography.Text>
        </Space>
      </Modal>

      <Modal
        title={t('users.permissionAudit.savedFilters.importTitle')}
        open={importOpen}
        onCancel={() => setImportOpen(false)}
        onOk={handleImport}
        okText={t('users.permissionAudit.savedFilters.importApply')}
        destroyOnHidden
      >
        <Input.TextArea
          rows={4}
          value={importToken}
          onChange={(e) => setImportToken(e.target.value)}
          placeholder={t('users.permissionAudit.savedFilters.importPlaceholder')}
        />
      </Modal>
    </div>
  );
}
