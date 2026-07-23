'use client';

/**
 * Super Admin TSE fleet management dashboard.
 * Process health comes from existing ITseHealthMonitor (no duplicate probe worker).
 * DR: encrypted device + signature-chain snapshots (no vendor private keys).
 */
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  Button,
  Card,
  Col,
  Descriptions,
  Form,
  Modal,
  Progress,
  Row,
  Select,
  Space,
  Statistic,
  Table,
  Tag,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useMemo, useState } from 'react';

import { dateColumnRender, DateColumn } from '@/components/DateColumn';
import {
  createTseBackup,
  getTseCertificate,
  getTseFleetOverview,
  listTseBackups,
  previewTseBackupRestore,
  provisionTse,
  renewTseCertificate,
  restoreTseBackup,
  revokeTse,
} from '@/features/tse-management/api/tseManagement';
import { TseSimulationToolsCard } from '@/features/tse-management/components/TseSimulationToolsCard';
import type {
  TseBackupListItem,
  TseCertificateInfo,
  TseDeviceFleetItem,
} from '@/features/tse-management/types';
import { listAdminTenants } from '@/features/super-admin/api/adminTenants';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';

const { Countdown } = Statistic;

const STATUS_COLOR: Record<string, string> = {
  Active: 'green',
  Degraded: 'orange',
  Inactive: 'default',
  Expired: 'red',
};

function certStatusColor(status: string): string {
  switch (status) {
    case 'Valid':
      return 'green';
    case 'ExpiringSoon':
      return 'orange';
    case 'Expired':
    case 'Revoked':
    case 'Invalid':
      return 'red';
    default:
      return 'default';
  }
}

export default function TseManagementPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const { modal } = useAntdApp();
  const queryClient = useQueryClient();
  const [provisionOpen, setProvisionOpen] = useState(false);
  const [backupOpen, setBackupOpen] = useState(false);
  const [details, setDetails] = useState<TseDeviceFleetItem | null>(null);
  const [drTenantId, setDrTenantId] = useState<string | undefined>();
  const [form] = Form.useForm<{ tenantId: string }>();
  const [backupForm] = Form.useForm<{ tenantId: string }>();

  const fleetQuery = useQuery({
    queryKey: ['admin', 'tse-management'],
    queryFn: ({ signal }) => getTseFleetOverview(signal),
    refetchInterval: 60_000,
  });

  const certQuery = useQuery({
    queryKey: ['admin', 'tse-management', 'certificate', details?.id],
    queryFn: ({ signal }) => getTseCertificate(details!.id, signal),
    enabled: !!details?.id,
  });

  const tenantsQuery = useQuery({
    queryKey: ['admin', 'tenants', 'tse-provision'],
    queryFn: () => listAdminTenants(false),
    enabled: provisionOpen || backupOpen,
    staleTime: 60_000,
  });

  const backupsQuery = useQuery({
    queryKey: ['admin', 'tse-management', 'backups', drTenantId ?? 'all'],
    queryFn: ({ signal }) => listTseBackups(drTenantId, signal),
    refetchInterval: 60_000,
  });

  const invalidate = async () => {
    await queryClient.invalidateQueries({ queryKey: ['admin', 'tse-management'] });
  };

  const provisionMutation = useMutation({
    mutationFn: (tenantId: string) => provisionTse({ tenantId }),
    onSuccess: async (res) => {
      if (res.outcome === 'Skipped') {
        notify.success(t('tseManagement.provisionSkipped'));
      } else {
        notify.success(t('tseManagement.provisionSuccess'));
      }
      setProvisionOpen(false);
      form.resetFields();
      await invalidate();
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseManagement.provision',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const revokeMutation = useMutation({
    mutationFn: (deviceId: string) => revokeTse(deviceId),
    onSuccess: async () => {
      notify.success(t('tseManagement.revokeSuccess'));
      setDetails(null);
      await invalidate();
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseManagement.revoke',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const renewCertMutation = useMutation({
    mutationFn: (deviceId: string) => renewTseCertificate(deviceId),
    onSuccess: async (res) => {
      if (res.success) {
        notify.success(res.message || t('tseManagement.certRenewSuccess'));
      } else {
        notify.error(res.message || t('tseManagement.certRenewPending'));
      }
      await queryClient.invalidateQueries({
        queryKey: ['admin', 'tse-management', 'certificate', details?.id],
      });
      await invalidate();
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseManagement.renewCertificate',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const createBackupMutation = useMutation({
    mutationFn: (tenantId: string) => createTseBackup({ tenantId }),
    onSuccess: async () => {
      notify.success(t('tseManagement.backupSuccess'));
      setBackupOpen(false);
      backupForm.resetFields();
      await invalidate();
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseManagement.createBackup',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const confirmRestore = async (row: TseBackupListItem) => {
    try {
      const preview = await previewTseBackupRestore(row.id);
      const needsForce = preview.wouldRequireForceDowngrade;
      modal.confirm({
        title: t('tseManagement.restoreConfirmTitle'),
        width: 560,
        content: (
          <Space direction="vertical" size="small" style={{ width: '100%' }}>
            <Typography.Paragraph style={{ marginBottom: 0 }}>
              {t('tseManagement.restoreConfirmContent')}
            </Typography.Paragraph>
            <Alert
              type="warning"
              showIcon
              message={preview.cryptoMaterialNote}
              description={
                <ul style={{ margin: 0, paddingLeft: 18 }}>
                  {preview.warnings.slice(0, 5).map((w) => (
                    <li key={w}>{w}</li>
                  ))}
                </ul>
              }
            />
            {needsForce ? (
              <Alert type="error" showIcon message={t('tseManagement.restoreForceDowngrade')} />
            ) : null}
          </Space>
        ),
        okButtonProps: { danger: true },
        okText: t('tseManagement.actionRestore'),
        onOk: () =>
          restoreTseBackup(row.id, {
            confirmToken: 'RESTORE',
            forceChainDowngrade: needsForce,
          }).then(async (res) => {
            if (!res.success) {
              throw new Error(res.error || t('common.errorGeneric'));
            }
            notify.success(t('tseManagement.restoreSuccess'));
            await invalidate();
          }),
      });
    } catch (err) {
      notify.apiError(err, {
        logContext: 'TseManagement.previewRestore',
        fallbackKey: 'common.errorGeneric',
      });
    }
  };

  const overview = fleetQuery.data;
  const devices = overview?.devices ?? [];
  const backups = backupsQuery.data ?? [];

  const columns: ColumnsType<TseDeviceFleetItem> = useMemo(
    () => [
      {
        title: t('tseManagement.colTenant'),
        key: 'tenant',
        width: 160,
        render: (_, row) => row.tenantName || row.tenantSlug || t('tseManagement.unknownTenant'),
      },
      {
        title: t('tseManagement.colRegister'),
        dataIndex: 'cashRegisterNumber',
        width: 120,
        render: (v: string | null | undefined) => v || '—',
      },
      {
        title: t('tseManagement.colSerial'),
        dataIndex: 'serialNumber',
        ellipsis: true,
        render: (serial: string) => (
          <Typography.Text code style={{ fontSize: 12 }}>
            {serial}
          </Typography.Text>
        ),
      },
      {
        title: t('tseManagement.colType'),
        dataIndex: 'deviceType',
        width: 100,
      },
      {
        title: t('tseManagement.colStatus'),
        dataIndex: 'status',
        width: 120,
        render: (status: string) => (
          <Tag color={STATUS_COLOR[status] ?? 'default'}>
            {t(`tseManagement.status.${status}` as 'tseManagement.status.Active')}
          </Tag>
        ),
      },
      {
        title: t('tseManagement.colCertificate'),
        dataIndex: 'certificateStatus',
        width: 110,
      },
      {
        title: t('tseManagement.colHealth'),
        dataIndex: 'healthScore',
        width: 140,
        render: (score: number) => (
          <Progress
            percent={score}
            size="small"
            status={score > 80 ? 'success' : score >= 40 ? 'active' : 'exception'}
          />
        ),
      },
      {
        title: t('tseManagement.colCreated'),
        dataIndex: 'createdAt',
        width: 150,
        render: dateColumnRender('datetime'),
      },
      {
        title: t('tseManagement.colActions'),
        key: 'actions',
        width: 180,
        fixed: 'right',
        render: (_, row) => (
          <Space size="small">
            <Button size="small" onClick={() => setDetails(row)}>
              {t('tseManagement.actionDetails')}
            </Button>
            <Button
              size="small"
              danger
              disabled={!row.isActive}
              loading={revokeMutation.isPending}
              onClick={() => {
                modal.confirm({
                  title: t('tseManagement.revokeConfirmTitle'),
                  content: t('tseManagement.revokeConfirmContent'),
                  okButtonProps: { danger: true },
                  onOk: () => revokeMutation.mutateAsync(row.id),
                });
              }}
            >
              {t('tseManagement.actionRevoke')}
            </Button>
          </Space>
        ),
      },
    ],
    [t, modal, revokeMutation]
  );

  const backupColumns: ColumnsType<TseBackupListItem> = useMemo(
    () => [
      {
        title: t('tseManagement.colTenant'),
        key: 'tenant',
        render: (_, row) => row.tenantName || row.tenantSlug || row.tenantId,
      },
      {
        title: t('tseManagement.colCreated'),
        dataIndex: 'createdAt',
        width: 160,
        render: dateColumnRender('datetime'),
      },
      {
        title: t('tseManagement.backupDevices'),
        dataIndex: 'deviceCount',
        width: 90,
      },
      {
        title: t('tseManagement.backupChains'),
        dataIndex: 'chainCount',
        width: 90,
      },
      {
        title: t('tseManagement.backupEncryption'),
        dataIndex: 'encryptionKind',
        width: 140,
      },
      {
        title: t('tseManagement.colActions'),
        key: 'actions',
        width: 160,
        render: (_, row) => (
          <Button
            size="small"
            type="primary"
            danger
            onClick={() => void confirmRestore(row)}
          >
            {t('tseManagement.actionRestore')}
          </Button>
        ),
      },
    ],
    [t, modal, notify]
  );

  const healthScore = overview?.processHealthScore ?? 0;
  const tenantOptions = (tenantsQuery.data ?? []).map((tenant) => ({
    value: tenant.id,
    label: `${tenant.name} (${tenant.slug})`,
  }));

  return (
    <div style={{ padding: 24 }}>
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'flex-start',
          marginBottom: 16,
          gap: 16,
          flexWrap: 'wrap',
        }}
      >
        <div>
          <Typography.Title level={3} style={{ margin: 0 }}>
            {t('tseManagement.title')}
          </Typography.Title>
          <Typography.Text type="secondary">{t('tseManagement.subtitle')}</Typography.Text>
        </div>
        <Space wrap>
          <Button type="primary" onClick={() => setBackupOpen(true)}>
            {t('tseManagement.backupButton')}
          </Button>
          <Button onClick={() => setProvisionOpen(true)}>
            {t('tseManagement.provisionButton')}
          </Button>
        </Space>
      </div>

      <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic title={t('tseManagement.statTotal')} value={overview?.totalDevices ?? 0} />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title={t('tseManagement.statActive')}
              value={overview?.activeDevices ?? 0}
              valueStyle={{ color: '#52c41a' }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title={t('tseManagement.statDegraded')}
              value={(overview?.degradedDevices ?? 0) + (overview?.expiredCertificateDevices ?? 0)}
              valueStyle={{ color: '#faad14' }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title={t('tseManagement.statHealth')}
              value={healthScore}
              suffix="%"
              valueStyle={{ color: healthScore > 80 ? '#52c41a' : '#faad14' }}
            />
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {overview?.processHealthStatus ?? '—'} · {t('tseManagement.modeLabel')}:{' '}
              {overview?.tseMode ?? '—'} / {overview?.signingMode ?? '—'}
            </Typography.Text>
          </Card>
        </Col>
      </Row>

      {overview?.processLastErrorSafe ? (
        <Card size="small" style={{ marginBottom: 16 }}>
          <Typography.Text type="danger">
            {t('tseManagement.processError')}: {overview.processLastErrorSafe}
          </Typography.Text>
        </Card>
      ) : null}

      <Card title={t('tseManagement.tableTitle')} style={{ marginBottom: 16 }}>
        <Table
          rowKey="id"
          loading={fleetQuery.isLoading}
          dataSource={devices}
          columns={columns}
          scroll={{ x: 1100 }}
          pagination={{ pageSize: 20, showSizeChanger: true }}
          locale={{ emptyText: t('tseManagement.empty') }}
        />
      </Card>

      <TseSimulationToolsCard devices={devices} onApplied={invalidate} />

      <Card
        title={t('tseManagement.backupTableTitle')}
        extra={
          <Select
            allowClear
            placeholder={t('tseManagement.backupFilterTenant')}
            style={{ minWidth: 240 }}
            options={tenantOptions}
            value={drTenantId}
            onChange={(v) => setDrTenantId(v)}
            showSearch
            optionFilterProp="label"
          />
        }
      >
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: 12 }}
          message={t('tseManagement.backupCryptoNote')}
        />
        <Table
          rowKey="id"
          loading={backupsQuery.isLoading}
          dataSource={backups}
          columns={backupColumns}
          pagination={{ pageSize: 10 }}
          locale={{ emptyText: t('tseManagement.backupEmpty') }}
        />
      </Card>

      <Modal
        title={t('tseManagement.provisionModalTitle')}
        open={provisionOpen}
        onCancel={() => {
          setProvisionOpen(false);
          form.resetFields();
        }}
        onOk={() => form.submit()}
        confirmLoading={provisionMutation.isPending}
        destroyOnHidden
      >
        <Typography.Paragraph type="secondary">{t('tseManagement.provisionModalHint')}</Typography.Paragraph>
        <Form form={form} layout="vertical" onFinish={(values) => provisionMutation.mutate(values.tenantId)}>
          <Form.Item
            name="tenantId"
            label={t('tseManagement.provisionTenantLabel')}
            rules={[{ required: true, message: t('tseManagement.provisionTenantRequired') }]}
          >
            <Select
              showSearch
              optionFilterProp="label"
              loading={tenantsQuery.isLoading}
              options={tenantOptions}
            />
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        title={t('tseManagement.backupModalTitle')}
        open={backupOpen}
        onCancel={() => {
          setBackupOpen(false);
          backupForm.resetFields();
        }}
        onOk={() => backupForm.submit()}
        confirmLoading={createBackupMutation.isPending}
        destroyOnHidden
      >
        <Typography.Paragraph type="secondary">{t('tseManagement.backupModalHint')}</Typography.Paragraph>
        <Form
          form={backupForm}
          layout="vertical"
          onFinish={(values) => createBackupMutation.mutate(values.tenantId)}
        >
          <Form.Item
            name="tenantId"
            label={t('tseManagement.provisionTenantLabel')}
            rules={[{ required: true, message: t('tseManagement.provisionTenantRequired') }]}
          >
            <Select
              showSearch
              optionFilterProp="label"
              loading={tenantsQuery.isLoading}
              options={tenantOptions}
            />
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        title={t('tseManagement.detailsTitle')}
        open={details != null}
        onCancel={() => setDetails(null)}
        width={640}
        footer={
          <Space wrap>
            <Button onClick={() => setDetails(null)}>{t('tseManagement.certClose')}</Button>
            {details ? (
              <Button
                type="primary"
                loading={renewCertMutation.isPending}
                onClick={() => renewCertMutation.mutate(details.id)}
              >
                {t('tseManagement.certRenew')}
              </Button>
            ) : null}
          </Space>
        }
        destroyOnHidden
      >
        {details ? (
          <Space direction="vertical" size="middle" style={{ width: '100%' }}>
            <Descriptions column={1} size="small" bordered>
              <Descriptions.Item label={t('tseManagement.colTenant')}>
                {details.tenantName || details.tenantSlug || t('tseManagement.unknownTenant')}
              </Descriptions.Item>
              <Descriptions.Item label={t('tseManagement.colRegister')}>
                {details.cashRegisterNumber || details.cashRegisterId}
              </Descriptions.Item>
              <Descriptions.Item label={t('tseManagement.colSerial')}>{details.serialNumber}</Descriptions.Item>
              <Descriptions.Item label={t('tseManagement.colType')}>{details.deviceType}</Descriptions.Item>
              <Descriptions.Item label={t('tseManagement.colStatus')}>{details.status}</Descriptions.Item>
              <Descriptions.Item label={t('tseManagement.colHealth')}>{details.healthScore}%</Descriptions.Item>
              <Descriptions.Item label={t('tseManagement.colLastSignature')}>
                {details.lastSignatureTime}
              </Descriptions.Item>
              {details.errorMessage ? (
                <Descriptions.Item label={t('tseManagement.processError')}>
                  {details.errorMessage}
                </Descriptions.Item>
              ) : null}
            </Descriptions>

            <Typography.Title level={5} style={{ margin: 0 }}>
              {t('tseManagement.certSectionTitle')}
            </Typography.Title>
            {certQuery.isLoading ? (
              <Typography.Text type="secondary">{t('tseManagement.certLoading')}</Typography.Text>
            ) : certQuery.data ? (
              <CertificateDetailsPanel cert={certQuery.data} />
            ) : (
              <Alert type="warning" showIcon message={t('tseManagement.certLoadFailed')} />
            )}
          </Space>
        ) : null}
      </Modal>
    </div>
  );
}

function CertificateDetailsPanel({ cert }: { cert: TseCertificateInfo }) {
  const { t } = useI18n();
  const expiresMs = cert.expiresAt ? new Date(cert.expiresAt).getTime() : null;

  return (
    <Space direction="vertical" size="small" style={{ width: '100%' }}>
      <Descriptions column={1} size="small" bordered>
        <Descriptions.Item label={t('tseManagement.certDeviceId')}>
          <code>{cert.vendorDeviceId || cert.deviceRowId}</code>
        </Descriptions.Item>
        <Descriptions.Item label={t('tseManagement.certStatus')}>
          <Tag color={certStatusColor(cert.status)}>
            {t(`tseManagement.certLifecycle.${cert.status}` as 'tseManagement.certLifecycle.Valid')}
          </Tag>
        </Descriptions.Item>
        <Descriptions.Item label={t('tseManagement.certIssuer')}>
          {cert.issuer || '—'}
        </Descriptions.Item>
        <Descriptions.Item label={t('tseManagement.certSubject')}>
          {cert.subject || '—'}
        </Descriptions.Item>
        <Descriptions.Item label={t('tseManagement.certSerial')}>
          {cert.certificateSerialNumber || '—'}
        </Descriptions.Item>
        <Descriptions.Item label={t('tseManagement.certIssuedAt')}>
          {cert.issuedAt ? <DateColumn date={cert.issuedAt} format="short" utc /> : '—'}
        </Descriptions.Item>
        <Descriptions.Item label={t('tseManagement.certExpiresAt')}>
          {cert.expiresAt ? <DateColumn date={cert.expiresAt} format="short" utc /> : '—'}
        </Descriptions.Item>
        {expiresMs && !cert.isExpired && !cert.isRevoked ? (
          <Descriptions.Item label={t('tseManagement.certCountdown')} span={2}>
            <Countdown value={expiresMs} format="D [d] HH:mm:ss" />
          </Descriptions.Item>
        ) : null}
        {cert.scheduledRenewalAt ? (
          <Descriptions.Item label={t('tseManagement.certScheduledRenewal')}>
            <DateColumn date={cert.scheduledRenewalAt} format="datetime" utc />
          </Descriptions.Item>
        ) : null}
        <Descriptions.Item label={t('tseManagement.certSource')}>{cert.source || '—'}</Descriptions.Item>
      </Descriptions>
      {(cert.warnings?.length ?? 0) > 0 ? (
        <Space direction="vertical" size={4} style={{ width: '100%' }}>
          {cert.warnings.map((w) => (
            <Alert
              key={`${w.code}-${w.message}`}
              type={w.severity === 'Critical' ? 'error' : w.severity === 'Warning' ? 'warning' : 'info'}
              showIcon
              message={w.message}
            />
          ))}
        </Space>
      ) : null}
    </Space>
  );
}
