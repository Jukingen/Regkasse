'use client';

import { EyeOutlined, UndoOutlined } from '@ant-design/icons';
import { Button, Modal, Space, Spin, Table, Tag, Tooltip, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useState } from 'react';

import type { OperationLogDetail, OperationLogListItem } from '@/features/audit/api/operationLogs';
import { useI18n } from '@/i18n';

export type OperationLogViewerProps = {
  logs: OperationLogListItem[];
  loading?: boolean;
  onUndo: (id: string) => void | Promise<void>;
  onViewDetails?: (id: string) => Promise<OperationLogDetail | null>;
  page?: number;
  pageSize?: number;
  total?: number;
  onPageChange?: (page: number, pageSize: number) => void;
};

export function OperationLogViewer({
  logs,
  loading,
  onUndo,
  onViewDetails,
  page = 1,
  pageSize = 50,
  total = 0,
  onPageChange,
}: OperationLogViewerProps) {
  const { t } = useI18n();
  const [detail, setDetail] = useState<OperationLogDetail | null>(null);
  const [detailOpen, setDetailOpen] = useState(false);
  const [detailLoading, setDetailLoading] = useState(false);

  const openDetails = async (id: string) => {
    if (!onViewDetails) return;
    setDetailLoading(true);
    setDetailOpen(true);
    try {
      const row = await onViewDetails(id);
      setDetail(row);
    } finally {
      setDetailLoading(false);
    }
  };

  const columns: ColumnsType<OperationLogListItem> = [
    {
      title: t('activity.operationLog.table.time'),
      dataIndex: 'createdAt',
      key: 'createdAt',
      width: 180,
      render: (date: string) => new Date(date).toLocaleString(),
    },
    {
      title: t('activity.operationLog.table.user'),
      key: 'user',
      width: 200,
      render: (_, record) =>
        record.userDisplayName || record.userEmail || record.userId || t('activity.operationLog.unknownUser'),
    },
    {
      title: t('activity.operationLog.table.operation'),
      dataIndex: 'operationType',
      key: 'operationType',
      width: 160,
      render: (type: string) => <Tag color="blue">{type}</Tag>,
    },
    {
      title: t('activity.operationLog.table.entity'),
      key: 'entity',
      ellipsis: true,
      render: (_, record) => (
        <span>
          {record.entityType} ({record.entityId})
        </span>
      ),
    },
    {
      title: t('activity.operationLog.table.status'),
      key: 'status',
      width: 120,
      render: (_, record) => (
        <Tag color={record.isUndone ? 'red' : 'green'}>
          {record.isUndone
            ? t('activity.operationLog.status.undone')
            : t('activity.operationLog.status.active')}
        </Tag>
      ),
    },
    {
      title: t('activity.operationLog.table.actions'),
      key: 'actions',
      width: 120,
      render: (_, record) => (
        <Space>
          <Tooltip title={t('activity.operationLog.actions.view')}>
            <Button
              icon={<EyeOutlined />}
              size="small"
              onClick={() => void openDetails(record.id)}
              disabled={!onViewDetails}
            />
          </Tooltip>
          {record.canUndo && !record.isUndone && (
            <Tooltip title={t('activity.operationLog.actions.undo')}>
              <Button
                icon={<UndoOutlined />}
                size="small"
                onClick={() => void onUndo(record.id)}
              />
            </Tooltip>
          )}
        </Space>
      ),
    },
  ];

  return (
    <>
      <Table
        dataSource={logs}
        rowKey="id"
        columns={columns}
        loading={loading}
        pagination={{
          current: page,
          pageSize,
          total,
          showSizeChanger: true,
          onChange: onPageChange,
        }}
      />

      <Modal
        title={t('activity.operationLog.detailTitle')}
        open={detailOpen}
        onCancel={() => setDetailOpen(false)}
        footer={null}
        width={720}
        destroyOnHidden
      >
        <Spin spinning={detailLoading}>
          {detail && (
          <Space direction="vertical" size="middle" style={{ width: '100%' }}>
            <Typography.Text>
              <strong>{t('activity.operationLog.table.operation')}:</strong> {detail.operationType}
            </Typography.Text>
            <Typography.Text>
              <strong>{t('activity.operationLog.table.entity')}:</strong>{' '}
              {detail.entityType} ({detail.entityId})
            </Typography.Text>
            <div>
              <Typography.Text strong>{t('activity.operationLog.beforeState')}</Typography.Text>
              <Typography.Paragraph>
                <pre style={{ whiteSpace: 'pre-wrap', margin: 0 }}>
                  {formatJson(detail.beforeState)}
                </pre>
              </Typography.Paragraph>
            </div>
            <div>
              <Typography.Text strong>{t('activity.operationLog.afterState')}</Typography.Text>
              <Typography.Paragraph>
                <pre style={{ whiteSpace: 'pre-wrap', margin: 0 }}>
                  {formatJson(detail.afterState)}
                </pre>
              </Typography.Paragraph>
            </div>
          </Space>
          )}
        </Spin>
      </Modal>
    </>
  );
}

function formatJson(raw?: string | null): string {
  if (!raw) return '—';
  try {
    return JSON.stringify(JSON.parse(raw), null, 2);
  } catch {
    return raw;
  }
}
