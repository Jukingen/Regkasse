'use client';

import {
  CheckCircleOutlined,
  CloseCircleOutlined,
  ExclamationCircleOutlined,
} from '@ant-design/icons';
import { Alert, Button, Space, Typography } from 'antd';
import React, { useMemo, useSyncExternalStore } from 'react';
import { useRouter } from 'next/navigation';

import {
  buildRoleMenuPreview,
  summarizeMenuPreview,
  type MenuPreviewNode,
  type MenuPreviewVisibility,
} from '@/features/users/utils/roleMenuPreview';
import {
  startRoleMenuPreview,
  stopRoleMenuPreview,
  getRoleMenuPreviewSession,
  subscribeRoleMenuPreview,
} from '@/features/users/utils/roleMenuPreviewSession';
import { useI18n } from '@/i18n';

export type RoleMenuPreviewPanelProps = {
  roleName: string;
  roleLabel: string;
  permissions: ReadonlySet<string> | readonly string[];
};

function VisibilityIcon({ visibility }: { visibility: MenuPreviewVisibility }) {
  if (visibility === 'visible') {
    return <CheckCircleOutlined style={{ color: '#52c41a' }} aria-hidden />;
  }
  if (visibility === 'partial') {
    return <ExclamationCircleOutlined style={{ color: '#faad14' }} aria-hidden />;
  }
  return <CloseCircleOutlined style={{ color: '#ff4d4f' }} aria-hidden />;
}

function visibilityHintKey(visibility: MenuPreviewVisibility): string {
  if (visibility === 'visible') return 'users.roleDrawer.menuPreview.visibleHint';
  if (visibility === 'partial') return 'users.roleDrawer.menuPreview.partialHint';
  return 'users.roleDrawer.menuPreview.hiddenHint';
}

function PreviewTree({
  nodes,
  depth = 0,
  t,
}: {
  nodes: MenuPreviewNode[];
  depth?: number;
  t: (key: string) => string;
}) {
  return (
    <ul style={{ listStyle: 'none', margin: 0, paddingLeft: depth === 0 ? 0 : 16 }}>
      {nodes.map((node) => (
        <li key={node.key} style={{ marginBottom: 4 }}>
          <span
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: 8,
              opacity: node.visibility === 'hidden' ? 0.55 : 1,
              fontSize: 13,
            }}
          >
            <VisibilityIcon visibility={node.visibility} />
            <span>{node.label}</span>
            <Typography.Text type="secondary" style={{ fontSize: 11 }}>
              ← {t(visibilityHintKey(node.visibility))}
            </Typography.Text>
          </span>
          {node.children?.length ? (
            <PreviewTree nodes={node.children} depth={depth + 1} t={t} />
          ) : null}
        </li>
      ))}
    </ul>
  );
}

/**
 * Sidebar visibility preview for a role's effective/draft permissions.
 */
export function RoleMenuPreviewPanel({
  roleName,
  roleLabel,
  permissions,
}: RoleMenuPreviewPanelProps) {
  const { t } = useI18n();
  const router = useRouter();
  const permList = useMemo(
    () => (permissions instanceof Set ? [...permissions] : [...permissions]),
    [permissions]
  );

  const tree = useMemo(
    () => buildRoleMenuPreview(roleName, permList, t),
    [roleName, permList, t]
  );
  const stats = useMemo(() => summarizeMenuPreview(tree), [tree]);
  const activePreview = useSyncExternalStore(
    subscribeRoleMenuPreview,
    getRoleMenuPreviewSession,
    () => null
  );

  return (
    <div data-testid="role-menu-preview">
      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 12 }}
        message={t('users.roleDrawer.menuPreview.title', { role: roleLabel })}
        description={t('users.roleDrawer.menuPreview.legend')}
      />
      <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 8, fontSize: 12 }}>
        {t('users.roleDrawer.menuPreview.stats', {
          visible: stats.visible,
          partial: stats.partial,
          hidden: stats.hidden,
        })}
      </Typography.Text>
      <div
        style={{
          border: '1px solid rgba(0,0,0,0.06)',
          borderRadius: 8,
          padding: 12,
          maxHeight: 420,
          overflow: 'auto',
          background: '#fafafa',
        }}
      >
        <PreviewTree nodes={tree} t={t} />
      </div>
      <Space wrap style={{ marginTop: 12 }}>
        <Button
          type="primary"
          onClick={() => {
            // In-panel refresh is automatic via draft; this scrolls to top of preview.
            document
              .querySelector('[data-testid="role-menu-preview"]')
              ?.scrollIntoView({ behavior: 'smooth', block: 'start' });
          }}
        >
          {t('users.roleDrawer.menuPreview.testRole')}
        </Button>
        <Button
          onClick={() => {
            startRoleMenuPreview(roleName, permList);
            router.push('/dashboard');
          }}
        >
          {t('users.roleDrawer.menuPreview.testAsUser')}
        </Button>
        {activePreview ? (
          <Button danger type="link" onClick={() => stopRoleMenuPreview()}>
            {t('users.roleDrawer.menuPreview.stopPreview')}
          </Button>
        ) : null}
      </Space>
    </div>
  );
}
