'use client';

import { Alert, Button, Modal, Space, Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useEffect, useState } from 'react';

import {
  type RolePermissionSimulateResultDto,
  type RolePermissionSimulateUserImpactDto,
  simulateRolePermissions,
} from '@/features/users/api/rolePermissionSimulateApi';
import { PermissionChangesPanel } from '@/features/users/components/PermissionChangesPanel';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';

type RoleImpactAnalysisModalProps = {
  open: boolean;
  roleName: string;
  draftPermissions: Set<string>;
  savedPermissions: Set<string>;
  onCancel: () => void;
  onConfirmSave: () => Promise<void>;
  saveLoading?: boolean;
};

export function RoleImpactAnalysisModal({
  open,
  roleName,
  draftPermissions,
  savedPermissions,
  onCancel,
  onConfirmSave,
  saveLoading,
}: RoleImpactAnalysisModalProps) {
  const { t } = useI18n();
  const { message } = useAntdApp();
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<RolePermissionSimulateResultDto | null>(null);

  useEffect(() => {
    if (!open || !roleName) return;
    let cancelled = false;
    setLoading(true);
    setResult(null);
    void simulateRolePermissions(roleName, {
      proposedPermissions: Array.from(draftPermissions),
      page: 1,
      pageSize: 50,
    })
      .then((res) => {
        if (!cancelled) setResult(res);
      })
      .catch(() => {
        if (!cancelled) message.error(t('users.roleDrawer.impactAnalysisError'));
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [open, roleName, draftPermissions, message, t]);

  const columns: ColumnsType<RolePermissionSimulateUserImpactDto> = [
    {
      title: t('users.roleDrawer.impactAnalysisUser'),
      dataIndex: 'userName',
    },
    {
      title: t('users.roleDrawer.impactAnalysisGained'),
      dataIndex: 'permissionsGained',
      width: 100,
    },
    {
      title: t('users.roleDrawer.impactAnalysisLost'),
      dataIndex: 'permissionsLost',
      width: 100,
    },
  ];

  return (
    <Modal
      title={t('users.roleDrawer.impactAnalysisTitle')}
      open={open}
      onCancel={onCancel}
      width={720}
      footer={[
        <Button key="cancel" onClick={onCancel}>
          {t('common.buttons.cancel')}
        </Button>,
        <Button
          key="save"
          type="primary"
          loading={saveLoading}
          onClick={() => void onConfirmSave()}
        >
          {t('users.roleDrawer.impactAnalysisConfirmSave')}
        </Button>,
      ]}
      destroyOnHidden
    >
      <PermissionChangesPanel before={savedPermissions} after={draftPermissions} visible />
      {loading ? <Typography.Text type="secondary">{t('common.status.pending')}…</Typography.Text> : null}
      {result ? (
        <Space orientation="vertical" style={{ width: '100%', marginTop: 12 }}>
          <Alert
            type="info"
            showIcon
            title={t('users.roleDrawer.impactAnalysisAffectedUsers', {
              count: result.affectedUserCount,
            })}
          />
          <Table
            rowKey="userId"
            size="small"
            pagination={false}
            dataSource={result.users}
            columns={columns}
          />
        </Space>
      ) : null}
    </Modal>
  );
}
