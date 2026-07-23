'use client';

import { CheckOutlined, CloseOutlined } from '@ant-design/icons';
import {
  Alert,
  Button,
  Card,
  Col,
  Form,
  Input,
  Modal,
  Row,
  Space,
  Statistic,
  Table,
  Tag,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  type PermissionRequestDto,
  approvePermissionRequest,
  fetchPendingPermissionRequests,
  fetchPermissionRequestStats,
  rejectPermissionRequest,
} from '@/features/users/api/permissionRequestsApi';
import { resolvePermissionDisplayLabel } from '@/features/users/utils/permissionDisplayLabel';
import { dateColumnRender } from '@/components/DateColumn';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';

type ResolveMode = 'approve' | 'reject';

export function PermissionRequestsPanel() {
  const { t } = useI18n();
  const { message } = useAntdApp();
  const queryClient = useQueryClient();
  const [resolveTarget, setResolveTarget] = useState<{
    row: PermissionRequestDto;
    mode: ResolveMode;
  } | null>(null);
  const [note, setNote] = useState('');

  const pendingQuery = useQuery({
    queryKey: ['permission-requests', 'pending'],
    queryFn: fetchPendingPermissionRequests,
  });
  const statsQuery = useQuery({
    queryKey: ['permission-requests', 'stats'],
    queryFn: fetchPermissionRequestStats,
  });

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: ['permission-requests'] });
  };

  const resolveMutation = useMutation({
    mutationFn: async () => {
      if (!resolveTarget) throw new Error('No target');
      const body = { note: note.trim() || null };
      if (resolveTarget.mode === 'approve') {
        return approvePermissionRequest(resolveTarget.row.id, body);
      }
      return rejectPermissionRequest(resolveTarget.row.id, body);
    },
    onSuccess: () => {
      message.success(
        resolveTarget?.mode === 'approve'
          ? t('access.permissionRequests.approveSuccess')
          : t('access.permissionRequests.rejectSuccess')
      );
      setResolveTarget(null);
      setNote('');
      invalidate();
    },
    onError: () => message.error(t('access.permissionRequests.resolveError')),
  });

  const stats = statsQuery.data;
  const rows = pendingQuery.data ?? [];

  const columns: ColumnsType<PermissionRequestDto> = useMemo(
    () => [
      {
        title: t('access.permissionRequests.columnRequester'),
        key: 'requester',
        render: (_, row) => row.requesterUserName || row.requesterUserId || '—',
      },
      {
        title: t('access.permissionRequests.columnPermission'),
        dataIndex: 'permission',
        render: (perm: string) => resolvePermissionDisplayLabel(perm, t),
      },
      {
        title: t('access.permissionRequests.columnDuration'),
        dataIndex: 'requestedDuration',
        width: 90,
      },
      {
        title: t('access.permissionRequests.columnReason'),
        dataIndex: 'reason',
        ellipsis: true,
      },
      {
        title: t('access.permissionRequests.columnRequestedAt'),
        dataIndex: 'requestedAt',
        width: 160,
        render: dateColumnRender('datetime'),
      },
      {
        title: t('access.permissionRequests.columnStatus'),
        dataIndex: 'status',
        width: 110,
        render: (status: string) => <Tag color="processing">{status}</Tag>,
      },
      {
        key: 'actions',
        width: 200,
        render: (_: unknown, row) => (
          <Space>
            <Button
              type="primary"
              size="small"
              icon={<CheckOutlined />}
              onClick={() => {
                setNote('');
                setResolveTarget({ row, mode: 'approve' });
              }}
            >
              {t('access.permissionRequests.approve')}
            </Button>
            <Button
              danger
              size="small"
              icon={<CloseOutlined />}
              onClick={() => {
                setNote('');
                setResolveTarget({ row, mode: 'reject' });
              }}
            >
              {t('access.permissionRequests.reject')}
            </Button>
          </Space>
        ),
      },
    ],
    [t]
  );

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <Row gutter={[16, 16]}>
        <Col xs={12} md={6}>
          <Card size="small">
            <Statistic title={t('access.permissionRequests.statsPending')} value={stats?.pending ?? 0} />
          </Card>
        </Col>
        <Col xs={12} md={6}>
          <Card size="small">
            <Statistic title={t('access.permissionRequests.statsApproved')} value={stats?.approved ?? 0} />
          </Card>
        </Col>
        <Col xs={12} md={6}>
          <Card size="small">
            <Statistic title={t('access.permissionRequests.statsRejected')} value={stats?.rejected ?? 0} />
          </Card>
        </Col>
        <Col xs={12} md={6}>
          <Card size="small">
            <Statistic title={t('access.permissionRequests.statsTotal')} value={stats?.total ?? 0} />
          </Card>
        </Col>
      </Row>

      {pendingQuery.isError ? (
        <Alert type="error" showIcon title={t('access.permissionRequests.loadError')} />
      ) : null}

      <Table
        rowKey="id"
        loading={pendingQuery.isLoading}
        dataSource={rows}
        columns={columns}
        pagination={{ pageSize: 20 }}
        locale={{ emptyText: t('access.permissionRequests.empty') }}
      />

      <Modal
        title={
          resolveTarget?.mode === 'approve'
            ? t('access.permissionRequests.approveTitle')
            : t('access.permissionRequests.rejectTitle')
        }
        open={Boolean(resolveTarget)}
        onCancel={() => {
          setResolveTarget(null);
          setNote('');
        }}
        onOk={() => resolveMutation.mutate()}
        confirmLoading={resolveMutation.isPending}
        destroyOnHidden
      >
        <Typography.Paragraph type="secondary">
          {resolveTarget
            ? resolvePermissionDisplayLabel(resolveTarget.row.permission, t)
            : null}
        </Typography.Paragraph>
        <Form layout="vertical">
          <Form.Item label={t('access.permissionRequests.noteLabel')}>
            <Input.TextArea
              rows={3}
              value={note}
              onChange={(e) => setNote(e.target.value)}
              maxLength={500}
            />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
