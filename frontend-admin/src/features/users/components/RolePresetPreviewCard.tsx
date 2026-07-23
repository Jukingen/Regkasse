'use client';

import { Card, Space, Tag, Typography } from 'antd';
import React, { useMemo } from 'react';

import {
  getRolePresetPreview,
  type RolePreset,
} from '@/features/users/constants/rolePresets';
import { resolvePermissionDisplayLabel, resolvePermissionGroupLabel } from '@/features/users/utils/permissionDisplayLabel';
import { useI18n } from '@/i18n';

type RolePresetPreviewCardProps = {
  preset: RolePreset;
  /** When provided, counts only keys present in the live catalog. */
  catalogKeys?: Set<string> | string[];
  compact?: boolean;
};

/** Preview: permission count, key permissions, group distribution. */
export function RolePresetPreviewCard({
  preset,
  catalogKeys,
  compact = false,
}: RolePresetPreviewCardProps) {
  const { t } = useI18n();
  const preview = useMemo(
    () => getRolePresetPreview(preset, catalogKeys),
    [preset, catalogKeys]
  );

  return (
    <Card size="small" style={{ marginTop: compact ? 8 : 12, background: '#fafafa' }}>
      <Typography.Text strong style={{ display: 'block', marginBottom: 4 }}>
        {preset.label}
      </Typography.Text>
      <Typography.Paragraph type="secondary" style={{ fontSize: 12, marginBottom: 8 }}>
        {preset.description}
      </Typography.Paragraph>
      <Space orientation="vertical" size={8} style={{ width: '100%' }}>
        <Typography.Text style={{ fontSize: 12 }}>
          {t('users.roleDrawer.presetPreviewCount', { count: preview.permissionCount })}
        </Typography.Text>
        {preview.highlightKeys.length > 0 ? (
          <div>
            <Typography.Text type="secondary" style={{ fontSize: 11, display: 'block', marginBottom: 4 }}>
              {t('users.roleDrawer.presetPreviewKeyPermissions')}
            </Typography.Text>
            <Space size={[4, 4]} wrap>
              {preview.highlightKeys.map((key) => (
                <Tag key={key} style={{ fontSize: 11 }}>
                  {resolvePermissionDisplayLabel(key, t)}
                </Tag>
              ))}
            </Space>
          </div>
        ) : null}
        {preview.groupDistribution.length > 0 ? (
          <div>
            <Typography.Text type="secondary" style={{ fontSize: 11, display: 'block', marginBottom: 4 }}>
              {t('users.roleDrawer.presetPreviewGroups')}
            </Typography.Text>
            <Space size={[4, 4]} wrap>
              {preview.groupDistribution.map(({ slug, count }) => (
                <Tag key={slug} color="blue" style={{ fontSize: 11 }}>
                  {resolvePermissionGroupLabel(slug, t)} · {count}
                </Tag>
              ))}
            </Space>
          </div>
        ) : null}
      </Space>
    </Card>
  );
}
