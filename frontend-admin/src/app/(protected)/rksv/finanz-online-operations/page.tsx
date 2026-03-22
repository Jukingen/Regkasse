'use client';

import React, { useMemo, useState } from 'react';
import {
  Alert,
  Button,
  Card,
  Col,
  Descriptions,
  Input,
  Row,
  Space,
  Spin,
  Statistic,
  Table,
  Tag,
  Typography,
  message,
} from 'antd';
import { CheckCircleOutlined, CloseCircleOutlined, ReloadOutlined } from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import dayjs from 'dayjs';
import Link from 'next/link';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import {
  extractApiErrorMessage,
} from '@/api/admin-rksv/client';
import {
  getApiFinanzOnlineConfig,
  getApiFinanzOnlineErrors,
  getApiFinanzOnlineHistoryInvoiceId,
  getApiFinanzOnlineStatus,
  postApiFinanzOnlineTestConnection,
} from '@/api/generated/finanz-online/finanz-online';
import { rksvAdminQueryKeys } from '@/api/admin-rksv/query-keys';
import { OPERATOR_FO_OPERATIONS_PAGE_COPY } from '@/shared/operatorTruthCopy';

function statusColor(isConnected: boolean | undefined): string {
  if (isConnected === true) return 'green';
  if (isConnected === false) return 'red';
  return 'default';
}

export default function FinanzOnlineOperationsPage() {
  const queryClient = useQueryClient();
  const [invoiceId, setInvoiceId] = useState('');
  const [appliedInvoiceId, setAppliedInvoiceId] = useState('');

  const statusQuery = useQuery({
    queryKey: rksvAdminQueryKeys.finanzOnlineOps.status,
    queryFn: getApiFinanzOnlineStatus,
    staleTime: 15_000,
  });
  const configQuery = useQuery({
    queryKey: rksvAdminQueryKeys.finanzOnlineOps.config,
    queryFn: getApiFinanzOnlineConfig,
    staleTime: 60_000,
  });
  const errorsQuery = useQuery({
    queryKey: rksvAdminQueryKeys.finanzOnlineOps.errors,
    queryFn: getApiFinanzOnlineErrors,
    staleTime: 20_000,
  });
  const historyQuery = useQuery({
    queryKey: rksvAdminQueryKeys.finanzOnlineOps.history(appliedInvoiceId),
    queryFn: () => getApiFinanzOnlineHistoryInvoiceId(appliedInvoiceId),
    enabled: appliedInvoiceId.length > 0,
  });

  const testMutation = useMutation({
    mutationFn: () => postApiFinanzOnlineTestConnection(),
    onSuccess: (result) => {
      if (result.success) message.success(result.message || 'Verbindungstest erfolgreich.');
      else message.warning(result.message || 'Verbindungstest fehlgeschlagen.');
      queryClient.invalidateQueries({ queryKey: rksvAdminQueryKeys.finanzOnlineOps.base });
    },
    onError: (err) => message.error(extractApiErrorMessage(err, 'Verbindungstest fehlgeschlagen.')),
  });

  const errors = errorsQuery.data ?? [];
  const history = historyQuery.data ?? [];
  const testResult = testMutation.data;

  const canRunHistory = useMemo(() => appliedInvoiceId.trim().length > 0, [appliedInvoiceId]);

  return (
    <>
      <AdminPageHeader
        title={OPERATOR_FO_OPERATIONS_PAGE_COPY.pageTitle}
        breadcrumbs={[
          { title: 'Dashboard', href: '/dashboard' },
          { title: 'RKSV', href: '/rksv' },
          { title: OPERATOR_FO_OPERATIONS_PAGE_COPY.breadcrumbTitle },
        ]}
        actions={
          <Button
            icon={<ReloadOutlined />}
            onClick={() => queryClient.invalidateQueries({ queryKey: rksvAdminQueryKeys.finanzOnlineOps.base })}
          >
            Aktualisieren
          </Button>
        }
      />

      <Typography.Paragraph type="secondary" style={{ marginBottom: 16 }}>
        {OPERATOR_FO_OPERATIONS_PAGE_COPY.introLead}{' '}
        <Link href="/rksv/finanz-online-queue">{OPERATOR_FO_OPERATIONS_PAGE_COPY.introAbgleichLinkLabel}</Link>.
      </Typography.Paragraph>

      <Row gutter={[16, 16]}>
        <Col xs={24} lg={12}>
          <Card title="Detaillierter Status" size="small">
            {statusQuery.isLoading ? (
              <Spin />
            ) : statusQuery.isError ? (
              <Alert type="error" message={extractApiErrorMessage(statusQuery.error, 'Status konnte nicht geladen werden.')} />
            ) : (
              <Descriptions bordered size="small" column={1}>
                <Descriptions.Item label="Verbindung">
                  <Tag
                    color={statusColor(statusQuery.data?.isConnected)}
                    icon={statusQuery.data?.isConnected ? <CheckCircleOutlined /> : <CloseCircleOutlined />}
                  >
                    {statusQuery.data?.isConnected ? 'Connected' : 'Disconnected'}
                  </Tag>
                </Descriptions.Item>
                <Descriptions.Item label="API Version">{statusQuery.data?.apiVersion || '—'}</Descriptions.Item>
                <Descriptions.Item label="Last Sync">{statusQuery.data?.lastSync || '—'}</Descriptions.Item>
                <Descriptions.Item label="Pending Invoices">{statusQuery.data?.pendingInvoices ?? 0}</Descriptions.Item>
                <Descriptions.Item label="Pending Reports">{statusQuery.data?.pendingReports ?? 0}</Descriptions.Item>
                <Descriptions.Item label="Fehlermeldung">{statusQuery.data?.errorMessage || '—'}</Descriptions.Item>
              </Descriptions>
            )}
          </Card>
        </Col>

        <Col xs={24} lg={12}>
          <Card
            title="Konfiguration (nur Anzeige) & Verbindungstest (API-Aufruf)"
            size="small"
            extra={
              <Button loading={testMutation.isPending} onClick={() => testMutation.mutate()} type="primary">
                Test Connection
              </Button>
            }
          >
            {configQuery.isLoading ? (
              <Spin />
            ) : configQuery.isError ? (
              <Alert type="error" message={extractApiErrorMessage(configQuery.error, 'Konfiguration konnte nicht geladen werden.')} />
            ) : (
              <Space direction="vertical" style={{ width: '100%' }} size="middle">
                <Row gutter={[12, 12]}>
                  <Col span={12}>
                    <Statistic title="Enabled" value={configQuery.data?.isEnabled ? 'Ja' : 'Nein'} />
                  </Col>
                  <Col span={12}>
                    <Statistic title="Auto Submit" value={configQuery.data?.autoSubmit ? 'Ja' : 'Nein'} />
                  </Col>
                  <Col span={12}>
                    <Statistic title="Submit Interval (min)" value={configQuery.data?.submitInterval ?? 0} />
                  </Col>
                  <Col span={12}>
                    <Statistic title="Retry Attempts" value={configQuery.data?.retryAttempts ?? 0} />
                  </Col>
                </Row>
                <Descriptions bordered size="small" column={1}>
                  <Descriptions.Item label="API URL">{configQuery.data?.apiUrl || '—'}</Descriptions.Item>
                  <Descriptions.Item label="Username">{configQuery.data?.username || '—'}</Descriptions.Item>
                  <Descriptions.Item label="Validation">{configQuery.data?.enableValidation ? 'Enabled' : 'Disabled'}</Descriptions.Item>
                </Descriptions>
                {testResult && (
                  <Alert
                    type={testResult.success ? 'success' : 'warning'}
                    message={testResult.message}
                    description={`Response time: ${testResult.responseTime} ms · ${testResult.timestamp}`}
                    showIcon
                  />
                )}
              </Space>
            )}
          </Card>
        </Col>
      </Row>

      <Card title={`Recent Errors (${errors.length})`} size="small" style={{ marginTop: 16 }}>
        {errorsQuery.isLoading ? (
          <Spin />
        ) : errorsQuery.isError ? (
          <Alert type="error" message={extractApiErrorMessage(errorsQuery.error, 'Fehlerliste konnte nicht geladen werden.')} />
        ) : errors.length === 0 ? (
          <Alert type="info" message="Keine Fehler gefunden." />
        ) : (
          <Table
            size="small"
            pagination={{ pageSize: 5 }}
            rowKey={(r) => `${r.code}-${r.timestamp}-${r.invoiceNumber}`}
            dataSource={errors}
            columns={[
              { title: 'Code', dataIndex: 'code', key: 'code', width: 120 },
              { title: 'Message', dataIndex: 'message', key: 'message' },
              { title: 'Timestamp', dataIndex: 'timestamp', key: 'timestamp', width: 180 },
              { title: 'Invoice', dataIndex: 'invoiceNumber', key: 'invoiceNumber', width: 180, render: (v: string) => v || '—' },
              { title: 'Retry', dataIndex: 'retryCount', key: 'retryCount', width: 90 },
            ]}
          />
        )}
      </Card>

      <Card
        title="Submission History by Invoice ID"
        size="small"
        style={{ marginTop: 16 }}
        extra={
          <Space>
            <Input
              placeholder="Invoice ID (GUID)"
              style={{ width: 280 }}
              value={invoiceId}
              onChange={(e) => setInvoiceId(e.target.value)}
            />
            <Button type="primary" onClick={() => setAppliedInvoiceId(invoiceId.trim())}>
              Verlauf laden
            </Button>
          </Space>
        }
      >
        {!canRunHistory ? (
          <Alert type="info" message="Bitte eine Invoice ID eingeben, um Verlauf zu laden." />
        ) : historyQuery.isLoading ? (
          <Spin />
        ) : historyQuery.isError ? (
          <Alert type="error" message={extractApiErrorMessage(historyQuery.error, 'Verlauf konnte nicht geladen werden.')} />
        ) : history.length === 0 ? (
          <Alert type="info" message="Kein Submission-Verlauf fuer diese Invoice ID gefunden." />
        ) : (
          <Table
            size="small"
            dataSource={history}
            rowKey={(r) => `${r.id}-${r.submittedAt}`}
            pagination={{ pageSize: 10 }}
            columns={[
              {
                title: 'Submitted At',
                dataIndex: 'submittedAt',
                key: 'submittedAt',
                width: 180,
                render: (v: string) => (v ? dayjs(v).format('DD.MM.YYYY HH:mm:ss') : '—'),
              },
              {
                title: 'Success',
                dataIndex: 'success',
                key: 'success',
                width: 90,
                render: (v: boolean) => <Tag color={v ? 'green' : 'red'}>{v ? 'Yes' : 'No'}</Tag>,
              },
              { title: 'HTTP', dataIndex: 'responseStatusCode', key: 'responseStatusCode', width: 80 },
              { title: 'Error', dataIndex: 'errorMessage', key: 'errorMessage', ellipsis: true },
            ]}
          />
        )}
      </Card>
    </>
  );
}
