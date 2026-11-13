'use client';

import { Collapse, Tag, Typography } from 'antd';
import React, { useMemo } from 'react';

import { resolvePermissionDisplayLabel } from '@/features/users/utils/permissionDisplayLabel';
import { summarizePermissionChanges } from '@/features/users/utils/permissionChangesSummary';
import { useI18n } from '@/i18n';

export type PermissionChangesPanelProps = {
  before: Iterable<string>;
  after: Iterable<string>;
  /** When false, panel collapses to empty (no dirty state). */
  visible?: boolean;
};

/**
 * "What's changed?" — before/after permission keys + affected menus.
 */
export function PermissionChangesPanel({
  before,
  after,
  visible = true,
}: PermissionChangesPanelProps) {
  const { t } = useI18n();

  const summary = useMemo(() => summarizePermissionChanges(before, after), [before, after]);

  if (!visible) return null;
  if (summary.added.length === 0 && summary.removed.length === 0) return null;

  return (
    <Collapse
      size="small"
      style={{ marginBottom: 12 }}
      defaultActiveKey={['changes']}
      items={[
        {
          key: 'changes',
          label: t('users.permissionOnboarding.whatsChangedTitle', {
            added: summary.added.length,
            removed: summary.removed.length,
          }),
          children: (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
              {summary.added.length > 0 ? (
                <div>
                  <Typography.Text strong style={{ fontSize: 12, display: 'block', marginBottom: 4 }}>
                    {t('users.permissionOnboarding.whatsChangedAdded')}
                  </Typography.Text>
                  <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4 }}>
                    {summary.added.slice(0, 24).map((key) => (
                      <Tag key={key} color="success" style={{ margin: 0 }}>
                        {resolvePermissionDisplayLabel(key, t)}
                      </Tag>
                    ))}
                    {summary.added.length > 24 ? (
                      <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                        +{summary.added.length - 24}
                      </Typography.Text>
                    ) : null}
                  </div>
                </div>
              ) : null}

              {summary.removed.length > 0 ? (
                <div>
                  <Typography.Text strong style={{ fontSize: 12, display: 'block', marginBottom: 4 }}>
                    {t('users.permissionOnboarding.whatsChangedRemoved')}
                  </Typography.Text>
                  <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4 }}>
                    {summary.removed.slice(0, 24).map((key) => (
                      <Tag key={key} color="error" style={{ margin: 0 }}>
                        {resolvePermissionDisplayLabel(key, t)}
                      </Tag>
                    ))}
                    {summary.removed.length > 24 ? (
                      <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                        +{summary.removed.length - 24}
                      </Typography.Text>
                    ) : null}
                  </div>
                </div>
              ) : null}

              {summary.menusGained.length > 0 || summary.menusLost.length > 0 ? (
                <div>
                  <Typography.Text strong style={{ fontSize: 12, display: 'block', marginBottom: 4 }}>
                    {t('users.permissionOnboarding.whatsChangedMenus')}
                  </Typography.Text>
                  {summary.menusGained.length > 0 ? (
                    <Typography.Text style={{ fontSize: 12, display: 'block' }}>
                      {t('users.permissionOnboarding.whatsChangedMenusGained')}:{' '}
                      {summary.menusGained
                        .slice(0, 8)
                        .map((m) => t(m.labelKey))
                        .join(', ')}
                      {summary.menusGained.length > 8
                        ? ` (+${summary.menusGained.length - 8})`
                        : ''}
                    </Typography.Text>
                  ) : null}
                  {summary.menusLost.length > 0 ? (
                    <Typography.Text style={{ fontSize: 12, display: 'block' }}>
                      {t('users.permissionOnboarding.whatsChangedMenusLost')}:{' '}
                      {summary.menusLost
                        .slice(0, 8)
                        .map((m) => t(m.labelKey))
                        .join(', ')}
                      {summary.menusLost.length > 8
                        ? ` (+${summary.menusLost.length - 8})`
                        : ''}
                    </Typography.Text>
                  ) : null}
                </div>
              ) : (
                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                  {t('users.permissionOnboarding.whatsChangedNoMenuImpact')}
                </Typography.Text>
              )}
            </div>
          ),
        },
      ]}
    />
  );
}
