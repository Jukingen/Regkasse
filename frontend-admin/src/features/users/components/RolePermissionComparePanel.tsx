'use client';

import { SwapOutlined } from '@ant-design/icons';
import { Alert, Button, Select, Space, Switch, Typography } from 'antd';
import React, { useMemo } from 'react';

import { resolvePermissionDisplayLabel } from '@/features/users/utils/permissionDisplayLabel';
import {
  comparePermissionSets,
  type PermissionRoleDiff,
} from '@/features/users/utils/permissionRoleDiff';
import { useI18n } from '@/i18n';

export type RoleCompareOption = {
  value: string;
  label: string;
};

type RolePermissionComparePanelProps = {
  /** Current role being edited. */
  currentRoleName: string;
  currentRoleLabel: string;
  /** Draft permissions for current role. */
  basePermissions: Iterable<string>;
  compareRoleName: string | null;
  onCompareRoleChange: (roleName: string | null) => void;
  roleOptions: RoleCompareOption[];
  /** Permissions of the selected compare role. */
  comparePermissions: readonly string[] | null;
  visualDiffEnabled: boolean;
  onVisualDiffEnabledChange: (enabled: boolean) => void;
  showOnlyDifferences: boolean;
  onShowOnlyDifferencesChange: (enabled: boolean) => void;
  canApply: boolean;
  onApplyFromRole: () => void;
  applyLoading?: boolean;
};

const SUMMARY_MAX = 12;

/**
 * Compare current draft with another role: summary, filters, apply.
 */
export function RolePermissionComparePanel({
  currentRoleName,
  currentRoleLabel,
  basePermissions,
  compareRoleName,
  onCompareRoleChange,
  roleOptions,
  comparePermissions,
  visualDiffEnabled,
  onVisualDiffEnabledChange,
  showOnlyDifferences,
  onShowOnlyDifferencesChange,
  canApply,
  onApplyFromRole,
  applyLoading = false,
}: RolePermissionComparePanelProps) {
  const { t } = useI18n();

  const diff: PermissionRoleDiff | null = useMemo(() => {
    if (!compareRoleName || !comparePermissions) return null;
    return comparePermissionSets(basePermissions, comparePermissions);
  }, [basePermissions, compareRoleName, comparePermissions]);

  const selectableOptions = useMemo(
    () => roleOptions.filter((o) => o.value !== currentRoleName),
    [roleOptions, currentRoleName]
  );

  return (
    <div
      style={{
        marginBottom: 12,
        padding: 12,
        borderRadius: 8,
        border: '1px solid #f0f0f0',
        background: '#fafafa',
      }}
    >
      <Space wrap size={8} style={{ width: '100%', marginBottom: 8 }}>
        <Typography.Text strong style={{ whiteSpace: 'nowrap' }}>
          <SwapOutlined style={{ marginRight: 6 }} />
          {t('users.roleDrawer.compareWithRole')}
        </Typography.Text>
        <Select
          allowClear
          placeholder={t('users.roleDrawer.compareRolePlaceholder')}
          style={{ minWidth: 200 }}
          value={compareRoleName}
          options={selectableOptions}
          onChange={(value: string | null) => onCompareRoleChange(value)}
          showSearch
          optionFilterProp="label"
        />
        {compareRoleName && canApply ? (
          <Button type="primary" loading={applyLoading} onClick={onApplyFromRole}>
            {t('users.roleDrawer.applyFromRole')}
          </Button>
        ) : null}
      </Space>

      {diff ? (
        <>
          <Space wrap size={16} style={{ marginBottom: 8 }}>
            <Typography.Text style={{ fontSize: 12 }}>
              <span style={{ color: '#cf1322' }}>●</span>{' '}
              {t('users.roleDrawer.compareDiffCount', { count: diff.differenceCount })}
            </Typography.Text>
            <Typography.Text style={{ fontSize: 12 }}>
              <span style={{ color: '#389e0d' }}>●</span>{' '}
              {t('users.roleDrawer.compareSameCount', { count: diff.same.length })}
            </Typography.Text>
            <Typography.Text style={{ fontSize: 12 }}>
              <span style={{ color: '#0958d9' }}>●</span>{' '}
              {t('users.roleDrawer.compareExclusiveCount', {
                count: diff.onlyBase.length + diff.onlyCompare.length,
              })}
            </Typography.Text>
          </Space>

          <Typography.Text type="secondary" style={{ fontSize: 11, display: 'block', marginBottom: 8 }}>
            {t('users.roleDrawer.compareLegend')}
          </Typography.Text>

          <Space wrap size={16} style={{ marginBottom: 8 }}>
            <Space size={6}>
              <Switch
                size="small"
                checked={visualDiffEnabled}
                onChange={onVisualDiffEnabledChange}
              />
              <Typography.Text style={{ fontSize: 12 }}>
                {t('users.roleDrawer.compareVisualDiff')}
              </Typography.Text>
            </Space>
            <Space size={6}>
              <Switch
                size="small"
                checked={showOnlyDifferences}
                onChange={onShowOnlyDifferencesChange}
              />
              <Typography.Text style={{ fontSize: 12 }}>
                {t('users.roleDrawer.compareOnlyDifferences')}
              </Typography.Text>
            </Space>
          </Space>

          {diff.differenceCount === 0 ? (
            <Alert
              type="success"
              showIcon
              title={t('users.roleDrawer.compareIdentical', {
                role: selectableOptions.find((o) => o.value === compareRoleName)?.label ?? compareRoleName,
              })}
            />
          ) : (
            <div
              style={{
                fontFamily: 'ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace',
                fontSize: 12,
                lineHeight: 1.6,
                maxHeight: 140,
                overflow: 'auto',
                padding: 8,
                background: '#fff',
                borderRadius: 6,
                border: '1px solid #f0f0f0',
              }}
            >
              <div style={{ fontWeight: 600, marginBottom: 4 }}>
                {t('users.roleDrawer.compareDiffHeader', {
                  role:
                    selectableOptions.find((o) => o.value === compareRoleName)?.label ??
                    compareRoleName,
                })}
              </div>
              {diff.onlyBase.slice(0, SUMMARY_MAX).map((key) => (
                <div key={`+${key}`} style={{ color: '#389e0d' }}>
                  + {key} ({currentRoleLabel})
                  <Typography.Text type="secondary" style={{ marginLeft: 6, fontSize: 11 }}>
                    {resolvePermissionDisplayLabel(key, t)}
                  </Typography.Text>
                </div>
              ))}
              {diff.onlyCompare.slice(0, SUMMARY_MAX).map((key) => (
                <div key={`-${key}`} style={{ color: '#cf1322' }}>
                  − {key} (
                  {selectableOptions.find((o) => o.value === compareRoleName)?.label ??
                    compareRoleName}
                  )
                  <Typography.Text type="secondary" style={{ marginLeft: 6, fontSize: 11 }}>
                    {resolvePermissionDisplayLabel(key, t)}
                  </Typography.Text>
                </div>
              ))}
              {diff.differenceCount > SUMMARY_MAX * 2 ? (
                <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                  {t('users.roleDrawer.compareDiffMore', {
                    count: Math.max(0, diff.differenceCount - SUMMARY_MAX * 2),
                  })}
                </Typography.Text>
              ) : null}
            </div>
          )}
        </>
      ) : (
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
          {t('users.roleDrawer.compareHint')}
        </Typography.Text>
      )}
    </div>
  );
}
