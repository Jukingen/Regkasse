'use client';

import { useMemo, useState, type ReactNode } from 'react';
import {
  Alert,
  Button,
  Card,
  Col,
  Modal,
  Row,
  Space,
  Table,
  Tag,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  CheckCircleOutlined,
  DeleteOutlined,
  DownloadOutlined,
  EyeOutlined,
  SafetyCertificateOutlined,
  ThunderboltOutlined,
} from '@ant-design/icons';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n, formatDate } from '@/i18n';
import { usePermissions } from '@/hooks/usePermissions';
import {
  useConfirmDataRightsDelete,
  useCreateDataRightsRequest,
  useDataRightsRequests,
  useDownloadDataRightsExport,
  useExecuteDataRightsDelete,
  useTenantDataManagementSummary,
} from '@/features/data-management/hooks/useTenantDataManagement';
import type {
  DataRightsRequestType,
  TenantDataRightsRequest,
} from '@/features/data-management/api/tenantDataManagement';

type Props = { tenantId: string };

type RequestTypeCard = {
  key: DataRightsRequestType;
  icon: ReactNode;
  titleKey: string;
  descriptionKey: string;
  danger?: boolean;
};

function typeLabel(type: string, t: (key: string) => string): string {
  switch (type) {
    case 'view':
      return t('dataManagement.view');
    case 'export':
      return t('dataManagement.export');
    case 'delete':
      return t('dataManagement.delete');
    default:
      return type;
  }
}

function statusColor(status: string): string {
  switch (status) {
    case 'completed':
    case 'ready':
      return 'success';
    case 'processing':
    case 'approved':
      return 'processing';
    case 'pending':
    case 'pending_approval':
      return 'warning';
    case 'confirmed':
      return 'orange';
    case 'failed':
    case 'cancelled':
      return 'error';
    default:
      return 'default';
  }
}

const REQUEST_TYPE_CARDS: RequestTypeCard[] = [
  {
    key: 'view',
    icon: <EyeOutlined style={{ fontSize: 28 }} />,
    titleKey: 'dataManagement.view',
    descriptionKey: 'dataManagement.rights.cardDesc.view',
  },
  {
    key: 'export',
    icon: <DownloadOutlined style={{ fontSize: 28 }} />,
    titleKey: 'dataManagement.export',
    descriptionKey: 'dataManagement.rights.cardDesc.export',
  },
  {
    key: 'delete',
    icon: <DeleteOutlined style={{ fontSize: 28 }} />,
    titleKey: 'dataManagement.delete',
    descriptionKey: 'dataManagement.rights.cardDesc.delete',
    danger: true,
  },
];

export function DataRightsRequestPanel({ tenantId }: Props) {
  const { t, formatLocale } = useI18n();
  const { message, modal } = useAntdApp();
  const { isSuperAdmin } = usePermissions();
  const summaryQuery = useTenantDataManagementSummary(tenantId);
  const requestsQuery = useDataRightsRequests(tenantId);
  const createMutation = useCreateDataRightsRequest(tenantId);
  const downloadMutation = useDownloadDataRightsExport(tenantId);
  const confirmMutation = useConfirmDataRightsDelete(tenantId);
  const executeMutation = useExecuteDataRightsDelete(tenantId);
  const [modalOpen, setModalOpen] = useState(false);
  const [selectedType, setSelectedType] = useState<DataRightsRequestType | null>(null);

  const downloadRow = async (row: TenantDataRightsRequest) => {
    try {
      if (row.downloadLink && !row.canDownload) {
        window.open(row.downloadLink, '_blank', 'noopener,noreferrer');
        return;
      }
      const blob = await downloadMutation.mutateAsync(row.id);
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = row.artifactFileName ?? `tenant_${tenantId}_export.zip`;
      a.click();
      URL.revokeObjectURL(url);
      message.success(t('dataManagement.exportSuccess'));
    } catch {
      if (row.downloadLink) {
        window.open(row.downloadLink, '_blank', 'noopener,noreferrer');
        return;
      }
      message.error(t('dataManagement.exportFailed'));
    }
  };

  const confirmRow = (requestId: string) => {
    modal.confirm({
      title: t('dataManagement.confirmTitle'),
      content: t('dataManagement.confirmBody'),
      okText: t('dataManagement.confirmOk'),
      onOk: async () => {
        try {
          await confirmMutation.mutateAsync(requestId);
          message.success(t('dataManagement.confirmSuccess'));
        } catch {
          message.error(t('dataManagement.confirmFailed'));
        }
      },
    });
  };

  const executeRow = (requestId: string) => {
    modal.confirm({
      title: t('dataManagement.executeTitle'),
      content: t('dataManagement.executeBody'),
      okText: t('dataManagement.executeOk'),
      okButtonProps: { danger: true },
      onOk: async () => {
        try {
          const result = await executeMutation.mutateAsync(requestId);
          if (result.succeeded) {
            message.success(t('dataManagement.executeSuccess'));
          } else {
            message.error(result.error ?? t('dataManagement.executeFailed'));
          }
        } catch {
          message.error(t('dataManagement.executeFailed'));
        }
      },
    });
  };

  const requestColumns: ColumnsType<TenantDataRightsRequest> = useMemo(
    () => [
      {
        title: t('dataManagement.rights.colRequested'),
        dataIndex: 'requestedAtUtc',
        key: 'requestedAtUtc',
        render: (value: string) => formatDate(value, formatLocale),
      },
      {
        title: t('dataManagement.rights.colType'),
        dataIndex: 'requestType',
        key: 'requestType',
        render: (value: string) => typeLabel(value, t),
      },
      {
        title: t('dataManagement.rights.colStatus'),
        dataIndex: 'status',
        key: 'status',
        render: (value: string) => <Tag color={statusColor(value)}>{value}</Tag>,
      },
      {
        title: t('dataManagement.rights.colActions'),
        key: 'actions',
        render: (_, row) => (
          <Space wrap size="small">
            {row.canDownload || row.downloadLink ? (
              <Button
                size="small"
                icon={<DownloadOutlined />}
                loading={downloadMutation.isPending}
                onClick={() => void downloadRow(row)}
              >
                {t('dataManagement.download')}
              </Button>
            ) : null}
            {row.canConfirm ? (
              <Button
                size="small"
                icon={<CheckCircleOutlined />}
                loading={confirmMutation.isPending}
                onClick={() => confirmRow(row.id)}
              >
                {t('dataManagement.confirmDeletion')}
              </Button>
            ) : null}
            {isSuperAdmin && row.canExecute ? (
              <Button
                size="small"
                danger
                icon={<ThunderboltOutlined />}
                loading={executeMutation.isPending}
                onClick={() => executeRow(row.id)}
              >
                {t('dataManagement.executePurge')}
              </Button>
            ) : null}
          </Space>
        ),
      },
    ],
    // Handlers close over latest mutation state; columns rebuilt each render intentionally.
    [
      t,
      formatLocale,
      isSuperAdmin,
      downloadMutation.isPending,
      confirmMutation.isPending,
      executeMutation.isPending,
    ],
  );

  const isTypeDisabled = (type: DataRightsRequestType): boolean => {
    if (type === 'export') return summaryQuery.data?.canExport === false;
    if (type === 'delete') return !summaryQuery.data?.canRequestDeletion;
    return false;
  };

  const openRequestModal = (type: DataRightsRequestType) => {
    if (isTypeDisabled(type)) {
      if (type === 'delete') {
        message.warning(t('dataManagement.deleteWarning'));
      }
      return;
    }
    setSelectedType(type);
    setModalOpen(true);
  };

  const submitRequest = async (type: DataRightsRequestType) => {
    try {
      const row = await createMutation.mutateAsync({ type });
      setModalOpen(false);
      setSelectedType(null);
      message.success(t('dataManagement.requestSent'));

      if (type === 'view') {
        message.success(t('dataManagement.rights.viewSuccess'));
        void summaryQuery.refetch();
      } else if (type === 'export') {
        if (row.canDownload) {
          await downloadRow(row);
        } else if (row.downloadLink) {
          message.success(t('dataManagement.ready'));
        } else {
          message.info(t('dataManagement.processing'));
        }
      } else {
        message.success(t('dataManagement.deleteRequested'));
      }
    } catch {
      message.error(
        type === 'delete'
          ? t('dataManagement.deleteFailed')
          : t('dataManagement.rights.requestFailed'),
      );
    }
  };

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Alert
        type="info"
        showIcon
        icon={<SafetyCertificateOutlined />}
        title={t('dataManagement.ownership')}
        description={t('dataManagement.ownershipDesc')}
      />

      <Row gutter={[16, 16]}>
        {REQUEST_TYPE_CARDS.map((card) => {
          const disabled = isTypeDisabled(card.key);
          return (
            <Col key={card.key} xs={24} sm={8}>
              <Card
                hoverable={!disabled}
                loading={summaryQuery.isLoading}
                onClick={() => openRequestModal(card.key)}
                styles={{
                  body: {
                    textAlign: 'center',
                    opacity: disabled ? 0.55 : 1,
                    cursor: disabled ? 'not-allowed' : 'pointer',
                    minHeight: 140,
                  },
                }}
              >
                <div style={{ color: card.danger ? 'var(--ant-color-error)' : undefined }}>
                  {card.icon}
                </div>
                <Typography.Title level={5} style={{ marginTop: 12, marginBottom: 4 }}>
                  {t(card.titleKey)}
                </Typography.Title>
                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                  {t(card.descriptionKey)}
                </Typography.Text>
              </Card>
            </Col>
          );
        })}
      </Row>

      <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
        {t('dataManagement.deleteWarning')}
      </Typography.Paragraph>

      <Card title={t('dataManagement.history')} loading={requestsQuery.isLoading}>
        <Table
          rowKey="id"
          size="middle"
          pagination={{ pageSize: 8 }}
          columns={requestColumns}
          dataSource={requestsQuery.data ?? []}
          locale={{ emptyText: t('dataManagement.rights.emptyHistory') }}
        />
      </Card>

      <Modal
        title={t('dataManagement.rights.modalTitle')}
        open={modalOpen}
        onCancel={() => {
          setModalOpen(false);
          setSelectedType(null);
        }}
        destroyOnHidden
        footer={null}
      >
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: 16 }}
          title={t('dataManagement.rights.modalInfoTitle')}
          description={t('dataManagement.rights.modalInfoBody')}
        />
        <Space wrap>
          {REQUEST_TYPE_CARDS.map((card) => (
            <Button
              key={card.key}
              type={selectedType === card.key ? (card.danger ? 'primary' : 'primary') : 'default'}
              danger={card.danger}
              icon={card.icon}
              loading={createMutation.isPending && selectedType === card.key}
              disabled={isTypeDisabled(card.key) || createMutation.isPending}
              onClick={() => void submitRequest(card.key)}
            >
              {t(card.titleKey)}
            </Button>
          ))}
        </Space>
      </Modal>
    </Space>
  );
}
