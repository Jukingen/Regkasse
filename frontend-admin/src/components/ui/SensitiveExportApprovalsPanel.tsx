'use client';

import { Button, Card, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useCallback, useEffect, useState } from 'react';

import { dateColumnRender } from '@/components/DateColumn';
import { useAntdApp } from '@/hooks/useAntdApp';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import {
  approveSensitiveExportApproval,
  listPendingSensitiveExportApprovals,
  rejectSensitiveExportApproval,
  type SensitiveExportApprovalDto,
} from '@/lib/download/sensitiveExportSecurity';

/**
 * Super Admin inbox for pending sensitive-export download approvals.
 */
export function SensitiveExportApprovalsPanel() {
  const { t } = useI18n();
  const { message } = useAntdApp();
  const { isSuperAdmin } = usePermissions();
  const [rows, setRows] = useState<SensitiveExportApprovalDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [busyId, setBusyId] = useState<string | null>(null);

  const reload = useCallback(async () => {
    if (!isSuperAdmin) return;
    setLoading(true);
    try {
      setRows(await listPendingSensitiveExportApprovals());
    } catch {
      message.error(t('common.sensitiveExport.approvalsLoadFailed'));
    } finally {
      setLoading(false);
    }
  }, [isSuperAdmin, message, t]);

  useEffect(() => {
    void reload();
  }, [reload]);

  if (!isSuperAdmin) return null;

  const columns: ColumnsType<SensitiveExportApprovalDto> = [
    {
      title: t('common.sensitiveExport.approvalsKind'),
      dataIndex: 'exportKind',
      key: 'exportKind',
      render: (v: string) => <Tag>{v}</Tag>,
    },
    {
      title: t('common.sensitiveExport.approvalsRequester'),
      dataIndex: 'requesterUserId',
      key: 'requester',
      ellipsis: true,
    },
    {
      title: t('common.sensitiveExport.approvalsResource'),
      dataIndex: 'resourceId',
      key: 'resource',
      ellipsis: true,
      render: (v?: string | null) => v || '—',
    },
    {
      title: t('common.sensitiveExport.approvalsRequestedAt'),
      dataIndex: 'requestedAt',
      key: 'requestedAt',
      render: dateColumnRender('datetimeSeconds'),
    },
    {
      title: t('common.sensitiveExport.approvalsActions'),
      key: 'actions',
      render: (_: unknown, row) => (
        <Space>
          <Button
            type="primary"
            size="small"
            loading={busyId === row.id}
            onClick={() => {
              setBusyId(row.id);
              void approveSensitiveExportApproval(row.id)
                .then(() => {
                  message.success(t('common.sensitiveExport.approvalsApproved'));
                  return reload();
                })
                .catch(() => message.error(t('common.sensitiveExport.approvalsActionFailed')))
                .finally(() => setBusyId(null));
            }}
          >
            {t('common.sensitiveExport.approvalsApprove')}
          </Button>
          <Button
            danger
            size="small"
            loading={busyId === row.id}
            onClick={() => {
              setBusyId(row.id);
              void rejectSensitiveExportApproval(row.id)
                .then(() => {
                  message.success(t('common.sensitiveExport.approvalsRejected'));
                  return reload();
                })
                .catch(() => message.error(t('common.sensitiveExport.approvalsActionFailed')))
                .finally(() => setBusyId(null));
            }}
          >
            {t('common.sensitiveExport.approvalsReject')}
          </Button>
        </Space>
      ),
    },
  ];

  return (
    <Card
      title={t('common.sensitiveExport.approvalsTitle')}
      extra={
        <Button size="small" onClick={() => void reload()} loading={loading}>
          {t('common.sensitiveExport.approvalsRefresh')}
        </Button>
      }
    >
      <Typography.Paragraph type="secondary">
        {t('common.sensitiveExport.approvalsHint')}
      </Typography.Paragraph>
      <Table
        rowKey="id"
        size="middle"
        loading={loading}
        columns={columns}
        dataSource={rows}
        pagination={false}
        locale={{ emptyText: t('common.sensitiveExport.approvalsEmpty') }}
      />
    </Card>
  );
}
