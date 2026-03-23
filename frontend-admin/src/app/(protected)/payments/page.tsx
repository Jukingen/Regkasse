'use client';

import React, { useEffect, useMemo, useState } from 'react';
import {
  Table,
  Card,
  Typography,
  Tag,
  Space,
  Button,
  DatePicker,
  Select,
  Drawer,
  Descriptions,
  Alert,
  Statistic,
  Row,
  Col,
  Input,
  InputNumber,
  Modal,
  message,
  Collapse,
  Empty,
} from 'antd';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell, AdminPageScopeSummary } from '@/components/admin-layout/AdminPageShell';
import { ADMIN_NAV_LABELS, ADMIN_OVERVIEW_CRUMB } from '@/shared/adminShellLabels';
import { CreditCardOutlined, InfoCircleOutlined, ReloadOutlined } from '@ant-design/icons';
import { OPERATOR_LINK_LABELS, OPERATOR_SHARED_COPY } from '@/shared/operatorTruthCopy';
import {
  postApiAdminPaymentsIdCancel,
  postApiAdminPaymentsIdRefund,
  useGetApiAdminPayments,
  useGetApiAdminPaymentsId,
  useGetApiAdminPaymentsStatistics,
} from '@/api/generated/admin/admin';
import type {
  AdminPaymentDetailDto,
  AdminPaymentListItemDto,
} from '@/api/generated/model';
import dayjs from 'dayjs';
import { useMutation } from '@tanstack/react-query';
import { useRouter, useSearchParams } from 'next/navigation';
import Link from 'next/link';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { getReceiptByPaymentForensics } from '@/features/receipts/api/forensics-client';

const { RangePicker } = DatePicker;

const DEFAULT_DATE_RANGE = { startDate: dayjs().subtract(30, 'day').format('YYYY-MM-DD'), endDate: dayjs().format('YYYY-MM-DD') };

interface PaymentStatisticsShape {
  totalPayments?: number;
  totalAmount?: number;
  averageAmount?: number;
  tseSignedPayments?: number;
  finanzOnlineSentPayments?: number;
  finanzOnlineSentAmount?: number;
}

function fmtDetail(value: unknown): string {
  if (value === null || value === undefined) return '—';
  if (typeof value === 'boolean') return value ? 'Ja' : 'Nein';
  if (typeof value === 'number') return Number.isFinite(value) ? String(value) : '—';
  if (typeof value === 'string') return value.trim() === '' ? '—' : value;
  if (typeof value === 'object') return JSON.stringify(value);
  return String(value);
}

function shortId(value?: string | null): string {
  if (!value) return '—';
  return value.length > 12 ? `${value.slice(0, 8)}…` : value;
}

function getPaymentsListErrorMessage(error: unknown): string {
  if (error instanceof Error) return error.message;
  const norm = (error as { normalized?: { message?: string } })?.normalized;
  if (norm?.message) return norm.message;
  return 'Zahlungen konnten nicht geladen werden. Bitte erneut versuchen.';
}

export default function PaymentsPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { hasPermission } = usePermissions();
  const canCancel = hasPermission(PERMISSIONS.PAYMENT_CANCEL);
  const canRefund = hasPermission(PERMISSIONS.REFUND_CREATE);
  const canOpenReceipt = hasPermission(PERMISSIONS.SALE_VIEW);

  const [dateRange, setDateRange] = useState<[dayjs.Dayjs, dayjs.Dayjs]>([
    dayjs(DEFAULT_DATE_RANGE.startDate),
    dayjs(DEFAULT_DATE_RANGE.endDate),
  ]);
  const [methodFilter, setMethodFilter] = useState<string | undefined>();
  const [statusFilter, setStatusFilter] = useState<string | undefined>();
  const [selectedPaymentId, setSelectedPaymentId] = useState<string | null>(null);
  const [cancelReason, setCancelReason] = useState('');
  const [refundReason, setRefundReason] = useState('');
  const [refundAmount, setRefundAmount] = useState<number | null>(null);

  useEffect(() => {
    const pid = searchParams?.get('paymentId')?.trim();
    if (pid) setSelectedPaymentId(pid);
  }, [searchParams]);

  const listParams = useMemo(
    () => ({
      startDate: dateRange[0].format('YYYY-MM-DD'),
      endDate: dateRange[1].format('YYYY-MM-DD'),
      pageSize: 500,
      pageNumber: 1,
    }),
    [dateRange]
  );

  const { data, isLoading, isError, error, refetch } = useGetApiAdminPayments(listParams);
  const { data: statsRaw, isLoading: statsLoading } = useGetApiAdminPaymentsStatistics({
    startDate: listParams.startDate,
    endDate: listParams.endDate,
  });
  const { data: paymentDetail, isLoading: detailLoading } = useGetApiAdminPaymentsId(selectedPaymentId ?? '', {
    query: { enabled: !!selectedPaymentId },
  });
  const paymentDetailData = paymentDetail as AdminPaymentDetailDto | undefined;

  const paymentsResponse = data as { items?: AdminPaymentListItemDto[] } | undefined;
  const payments: AdminPaymentListItemDto[] = useMemo(
    () => paymentsResponse?.items ?? [],
    [paymentsResponse?.items]
  );

  const stats: PaymentStatisticsShape | null =
    statsRaw && typeof statsRaw === 'object' ? (statsRaw as PaymentStatisticsShape) : null;
  const operationalDetail = paymentDetailData;
  const safeOperationalDetail: AdminPaymentDetailDto = operationalDetail ?? ({} as AdminPaymentDetailDto);

  const filteredPayments = useMemo(() => {
    return payments.filter((p) => {
      if (methodFilter && p.method !== methodFilter) return false;
      if (statusFilter && p.status !== statusFilter) return false;
      return true;
    });
  }, [payments, methodFilter, statusFilter]);

  const paymentsScopeSummary = useMemo(() => {
    const parts = [
      `${dateRange[0].format('DD.MM.YYYY')}–${dateRange[1].format('DD.MM.YYYY')}`,
      `${payments.length} vom Server (max. 500 je Abfrage)`,
      `${filteredPayments.length} sichtbar nach Tabellenfilter`,
    ];
    if (methodFilter) parts.push(`Methode = ${methodFilter}`);
    if (statusFilter) parts.push(`Status = ${statusFilter}`);
    return parts.join(' · ');
  }, [dateRange, payments.length, filteredPayments.length, methodFilter, statusFilter]);

  const cancelMutation = useMutation({
    mutationFn: async () => {
      if (!selectedPaymentId) throw new Error('Keine Zahlung ausgewählt');
      return postApiAdminPaymentsIdCancel(selectedPaymentId, { reason: cancelReason.trim() });
    },
    onSuccess: async () => {
      message.success('Zahlung storniert');
      setCancelReason('');
      await refetch();
    },
    onError: (err: Error) => message.error(err?.message ?? 'Storno fehlgeschlagen'),
  });

  const refundMutation = useMutation({
    mutationFn: async () => {
      if (!selectedPaymentId) throw new Error('Keine Zahlung ausgewählt');
      if (!refundAmount || refundAmount <= 0) throw new Error('Rückerstattungsbetrag muss größer als 0 sein');
      return postApiAdminPaymentsIdRefund(selectedPaymentId, { amount: refundAmount, reason: refundReason.trim() });
    },
    onSuccess: async () => {
      message.success('Rückerstattung verarbeitet');
      setRefundAmount(null);
      setRefundReason('');
      await refetch();
    },
    onError: (err: Error) => message.error(err?.message ?? 'Rückerstattung fehlgeschlagen'),
  });

  const openReceipt = async () => {
    const receiptId = paymentDetailData?.receiptId;
    if (receiptId) {
      router.push(`/receipts/${receiptId}`);
      return;
    }
    if (!selectedPaymentId) {
      message.warning('Keine Zahlung ausgewählt');
      return;
    }
    try {
      const receipt = await getReceiptByPaymentForensics(selectedPaymentId);
      if (!receipt.receiptId) {
        message.warning('Kein Beleg für diese Zahlung gefunden');
        return;
      }
      router.push(`/receipts/${receipt.receiptId}`);
    } catch {
      message.warning('Kein Beleg für diese Zahlung gefunden');
    }
  };

  const methodOptions = Array.from(new Set(payments.map((p) => p.method).filter(Boolean))) as string[];
  const statusOptions = Array.from(new Set(payments.map((p) => p.status).filter(Boolean))) as string[];

  const columns = [
    {
      title: 'Transaktions-ID',
      dataIndex: 'transactionId',
      key: 'transactionId',
      render: (text: string) => <code style={{ fontSize: '12px' }}>{text || '—'}</code>,
    },
    {
      title: 'Datum',
      dataIndex: 'createdAt',
      key: 'createdAt',
      render: (date: string) => (date ? dayjs(date).format('DD.MM.YYYY HH:mm') : '—'),
    },
    {
      title: 'Betrag',
      dataIndex: 'totalAmount',
      key: 'amount',
      align: 'right' as const,
      render: (val: number, record: AdminPaymentListItemDto) => `${(val ?? 0).toFixed(2)} ${record.currency || 'EUR'}`,
    },
    {
      title: 'Zahlungsart',
      dataIndex: 'method',
      key: 'method',
      render: (method: string) => <Tag color="blue">{method || '—'}</Tag>,
    },
    {
      title: 'Status',
      dataIndex: 'status',
      key: 'status',
      render: (status: string) => {
        const colors: Record<string, string> = {
          Success: 'green',
          Pending: 'orange',
          Failed: 'red',
          Cancelled: 'default',
          Refunded: 'purple',
        };
        return <Tag color={colors[status] || 'default'}>{status || '—'}</Tag>;
      },
    },
    {
      title: 'Verknüpfte Entitäten',
      key: 'linkedEntities',
      width: 300,
      render: (_: unknown, row: AdminPaymentListItemDto) => {
        const pid = row.id?.trim();
        const foErr = row.finanzOnlineError?.trim();
        const hasLinks =
          Boolean(row.receiptId) || Boolean(row.invoiceNumber) || Boolean(row.offlineReplayBatchCorrelationId);
        const hasFo = Boolean(row.finanzOnlineStatus?.trim());
        const hasAny = Boolean(pid) || hasLinks || hasFo;
        if (!hasAny) {
          return <Typography.Text type="secondary">—</Typography.Text>;
        }
        const foColors: Record<string, string> = {
          Submitted: 'green',
          Pending: 'blue',
          Failed: 'red',
          NeedsReconciliation: 'orange',
        };
        const foColor = row.finanzOnlineStatus
          ? foColors[row.finanzOnlineStatus] || 'default'
          : 'default';
        return (
          <Space direction="vertical" size={6} style={{ maxWidth: 292 }}>
            {pid ? (
              <Typography.Text type="secondary" style={{ fontSize: 11, display: 'block' }}>
                Zahlungs-ID:{' '}
                <Typography.Text code copyable={{ text: pid }} style={{ fontSize: 11 }}>
                  {pid.length > 22 ? `${pid.slice(0, 20)}…` : pid}
                </Typography.Text>
              </Typography.Text>
            ) : null}
            {hasLinks ? (
              <Space size={4} wrap>
                {row.receiptId ? (
                  <Link href={`/receipts/${row.receiptId}`} target="_blank" rel="noopener noreferrer">
                    <Tag color="blue">
                      Beleg
                      {row.receiptNumber?.trim() ? (
                        <>
                          :{' '}
                          <Typography.Text code style={{ fontSize: 11 }}>
                            {row.receiptNumber.trim()}
                          </Typography.Text>
                        </>
                      ) : null}
                    </Tag>
                  </Link>
                ) : null}
                {row.invoiceNumber ? (
                  <Link
                    href={`/invoices?query=${encodeURIComponent(row.invoiceNumber)}`}
                    target="_blank"
                    rel="noopener noreferrer"
                  >
                    <Tag color="purple">Rechnung</Tag>
                  </Link>
                ) : null}
                {row.offlineReplayBatchCorrelationId ? (
                  <Link
                    href={`/rksv/incident?correlationId=${encodeURIComponent(row.offlineReplayBatchCorrelationId)}`}
                    target="_blank"
                    rel="noopener noreferrer"
                  >
                    <Tag color="orange">Incident</Tag>
                  </Link>
                ) : null}
              </Space>
            ) : null}
            {hasFo ? (
              <Space direction="vertical" size={2} style={{ width: '100%' }}>
                <Tag color={foColor}>FO {row.finanzOnlineStatus}</Tag>
                {foErr ? (
                  <Typography.Text type="danger" ellipsis={{ tooltip: foErr }} style={{ fontSize: 11, maxWidth: 280 }}>
                    {foErr}
                  </Typography.Text>
                ) : null}
              </Space>
            ) : null}
          </Space>
        );
      },
    },
    {
      title: 'Aktionen',
      key: 'actions',
      render: (_: unknown, row: AdminPaymentListItemDto) => (
        <Button size="small" icon={<InfoCircleOutlined />} onClick={() => setSelectedPaymentId(row.id ?? null)}>
          Details
        </Button>
      ),
    },
  ];

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={ADMIN_NAV_LABELS.payments}
        breadcrumbs={[ADMIN_OVERVIEW_CRUMB, { title: ADMIN_NAV_LABELS.payments }]}
        actions={
          <Space wrap>
            <Button icon={<ReloadOutlined />} onClick={() => refetch()} loading={isLoading}>
              {OPERATOR_SHARED_COPY.toolbarRefresh}
            </Button>
            <Button icon={<CreditCardOutlined />}>Terminal-Status</Button>
          </Space>
        }
      >
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0, maxWidth: 640 }}>
          Zahlungen im gewählten Zeitraum. Zeile wählen für Details, Storno und Rückerstattung sowie Verknüpfungen zu
          Beleg und FinanzOnline.
        </Typography.Paragraph>
      </AdminPageHeader>

      <Card size="small">
        <Space wrap>
          <RangePicker
            value={dateRange}
            onChange={(v) => {
              if (!v || !v[0] || !v[1]) return;
              setDateRange([v[0], v[1]]);
            }}
            format="DD.MM.YYYY"
            allowClear={false}
          />
          <Select
            placeholder="Methode"
            allowClear
            value={methodFilter}
            onChange={(v) => setMethodFilter(v)}
            style={{ width: 160 }}
            options={methodOptions.map((m) => ({ value: m, label: m }))}
          />
          <Select
            placeholder="Status"
            allowClear
            value={statusFilter}
            onChange={(v) => setStatusFilter(v)}
            style={{ width: 180 }}
            options={statusOptions.map((s) => ({ value: s, label: s }))}
          />
        </Space>
      </Card>

      <AdminPageScopeSummary label="Aktive Ansicht:">{paymentsScopeSummary}</AdminPageScopeSummary>

      {isError ? (
        <Alert
          type="error"
          showIcon
          message="Zahlungen konnten nicht geladen werden"
          description={getPaymentsListErrorMessage(error)}
          action={
            <Button size="small" onClick={() => refetch()}>
              {OPERATOR_SHARED_COPY.retryAfterError}
            </Button>
          }
        />
      ) : null}

      {!isError ? (
      <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
        <Col xs={24} sm={12} md={6}>
          <Card size="small">
            <Statistic title="Anzahl Zahlungen" value={stats?.totalPayments ?? filteredPayments.length} loading={statsLoading} />
          </Card>
        </Col>
        <Col xs={24} sm={12} md={6}>
          <Card size="small">
            <Statistic title="Gesamtbetrag" value={stats?.totalAmount ?? 0} precision={2} suffix="EUR" loading={statsLoading} />
          </Card>
        </Col>
        <Col xs={24} sm={12} md={6}>
          <Card size="small">
            <Statistic title="Durchschnitt" value={stats?.averageAmount ?? 0} precision={2} suffix="EUR" loading={statsLoading} />
          </Card>
        </Col>
        <Col xs={24} sm={12} md={6}>
          <Card size="small">
            <Statistic title="TSE signiert" value={stats?.tseSignedPayments ?? 0} loading={statsLoading} />
          </Card>
        </Col>
        <Col xs={24} sm={12} md={6}>
          <Card size="small">
            <Statistic title="FinanzOnline gesendet" value={stats?.finanzOnlineSentPayments ?? 0} loading={statsLoading} />
          </Card>
        </Col>
        <Col xs={24} sm={12} md={6}>
          <Card size="small">
            <Statistic
              title="FO gesendeter Betrag"
              value={stats?.finanzOnlineSentAmount ?? 0}
              precision={2}
              suffix="EUR"
              loading={statsLoading}
            />
          </Card>
        </Col>
      </Row>
      ) : null}

      {!isError ? (
      <Table
        columns={columns}
        dataSource={filteredPayments}
        loading={isLoading}
        rowKey="id"
        scroll={{ x: 1280 }}
        locale={{
          emptyText: (
            <Empty
              image={Empty.PRESENTED_IMAGE_SIMPLE}
              description="Keine Zahlungen für diesen Zeitraum oder die gewählten Filter."
            />
          ),
        }}
      />
      ) : null}

      <Drawer
        title="Zahlungsdetails"
        open={!!selectedPaymentId}
        onClose={() => setSelectedPaymentId(null)}
        width={640}
        destroyOnClose
      >
        {detailLoading ? (
          <Typography.Text type="secondary">Lade Details…</Typography.Text>
        ) : paymentDetailData ? (
          <>
            <Card size="small" title="Kerninformationen" style={{ marginBottom: 12 }}>
              <Descriptions size="small" column={1} bordered>
                <Descriptions.Item label="Payment ID">
                  <Typography.Text code copyable>
                    {selectedPaymentId}
                  </Typography.Text>
                </Descriptions.Item>
                <Descriptions.Item label="Transaktion">
                  <Typography.Text code copyable>
                    {fmtDetail(paymentDetailData.transactionId)}
                  </Typography.Text>
                </Descriptions.Item>
                <Descriptions.Item label="Belegnummer">
                  <Typography.Text code copyable>
                    {fmtDetail(paymentDetailData.receiptNumber)}
                  </Typography.Text>
                </Descriptions.Item>
                <Descriptions.Item label="Zeitpunkt (Server)">
                  {fmtDetail(paymentDetailData.createdAt)}
                </Descriptions.Item>
                <Descriptions.Item label="Betrag">{fmtDetail(paymentDetailData.totalAmount)} EUR</Descriptions.Item>
                <Descriptions.Item label="Zahlungsart (Roh)">
                  {fmtDetail(paymentDetailData.paymentMethodRaw ?? paymentDetailData.method)}
                </Descriptions.Item>
                <Descriptions.Item label="Kasse (FK, UUID)">
                  <Typography.Text code copyable>
                    {fmtDetail(paymentDetailData.cashRegisterId)}
                  </Typography.Text>
                </Descriptions.Item>
                <Descriptions.Item label="Kundenname">{fmtDetail(paymentDetailData.customerName)}</Descriptions.Item>
                <Descriptions.Item label="Idempotency Key">
                  <Typography.Text code copyable>
                    {fmtDetail(paymentDetailData.idempotencyKey)}
                  </Typography.Text>
                </Descriptions.Item>
              </Descriptions>
            </Card>

            <Card size="small" title="Verknüpfte Entitäten" style={{ marginBottom: 12 }}>
              <Descriptions size="small" column={1} bordered>
                <Descriptions.Item label="Payment ID">
                  <Typography.Text code copyable>
                    {fmtDetail(paymentDetailData.id)}
                  </Typography.Text>
                </Descriptions.Item>
                <Descriptions.Item label="Beleg-ID">
                  {safeOperationalDetail.receiptId ? (
                    <Space wrap>
                      <Typography.Text code copyable>{safeOperationalDetail.receiptId}</Typography.Text>
                      <Link href={`/receipts/${safeOperationalDetail.receiptId}`} target="_blank" rel="noopener noreferrer">
                        Öffnen
                      </Link>
                    </Space>
                  ) : (
                    '—'
                  )}
                </Descriptions.Item>
                <Descriptions.Item label="Rechnung">
                  {safeOperationalDetail.invoiceNumber || safeOperationalDetail.invoiceId ? (
                    <Space wrap>
                      <Typography.Text code copyable>
                        {safeOperationalDetail.invoiceNumber ?? shortId(safeOperationalDetail.invoiceId)}
                      </Typography.Text>
                      {safeOperationalDetail.invoiceNumber ? (
                        <Link
                          href={`/invoices?query=${encodeURIComponent(safeOperationalDetail.invoiceNumber)}`}
                          target="_blank"
                          rel="noopener noreferrer"
                        >
                          Öffnen
                        </Link>
                      ) : null}
                    </Space>
                  ) : (
                    '—'
                  )}
                </Descriptions.Item>
                <Descriptions.Item label="Replay-Batch-Correlation-ID">
                  {safeOperationalDetail.offlineReplayBatchCorrelationId ? (
                    <Space wrap>
                      <Typography.Text code copyable>{safeOperationalDetail.offlineReplayBatchCorrelationId}</Typography.Text>
                      <Link
                        href={`/rksv/incident?correlationId=${encodeURIComponent(safeOperationalDetail.offlineReplayBatchCorrelationId)}`}
                        target="_blank"
                        rel="noopener noreferrer"
                      >
                        {OPERATOR_LINK_LABELS.incidentAggregate}
                      </Link>
                      <Link
                        href={`/rksv/replay-batch/${encodeURIComponent(safeOperationalDetail.offlineReplayBatchCorrelationId)}`}
                        target="_blank"
                        rel="noopener noreferrer"
                      >
                        {OPERATOR_LINK_LABELS.replayBatchDetail}
                      </Link>
                    </Space>
                  ) : (
                    '—'
                  )}
                </Descriptions.Item>
              </Descriptions>
            </Card>

            <Card size="small" title="Betrieb / Herkunft" style={{ marginBottom: 12 }}>
              <Descriptions size="small" column={1} bordered>
                <Descriptions.Item label="Zahlungsursprung">
                  {safeOperationalDetail.isOfflineOrigin
                    ? 'Offline-Warteschlange → Server-Replay'
                    : 'Direkt (Online)'}
                </Descriptions.Item>
                <Descriptions.Item label="Belegnummer gesetzt">
                  {safeOperationalDetail.receiptNumber ? 'Ja' : 'Nein'}
                </Descriptions.Item>
                <Descriptions.Item label="Beleg-ID">
                  <Typography.Text code copyable>
                    {fmtDetail(safeOperationalDetail.receiptId)}
                  </Typography.Text>
                </Descriptions.Item>
                <Descriptions.Item label="Rechnung persistiert (API)">
                  {safeOperationalDetail.invoicePersisted == null
                    ? '— (Feld nicht in älterer API-Antwort)'
                    : fmtDetail(safeOperationalDetail.invoicePersisted)}
                </Descriptions.Item>
              </Descriptions>
            </Card>

            <Card size="small" title="Offline / Replay" style={{ marginBottom: 12 }}>
              <Typography.Paragraph type="secondary" style={{ marginBottom: 8, fontSize: 12 }}>
                Korrelation zwischen Offline-Warteschlange und Server-Replay (Support).
              </Typography.Paragraph>
              <Descriptions size="small" column={1} bordered>
                <Descriptions.Item label="Offline-Transaktions-ID">
                  <Typography.Text code copyable>
                    {fmtDetail(safeOperationalDetail.offlineTransactionId)}
                  </Typography.Text>
                </Descriptions.Item>
                <Descriptions.Item label="Replay-Batch-Correlation-ID">
                  <Typography.Text code copyable>
                    {fmtDetail(safeOperationalDetail.offlineReplayBatchCorrelationId)}
                  </Typography.Text>
                </Descriptions.Item>
              </Descriptions>
            </Card>

            <Card size="small" title="FinanzOnline" style={{ marginBottom: 12 }}>
              <Descriptions size="small" column={1} bordered>
                <Descriptions.Item label="Status">{fmtDetail(safeOperationalDetail.finanzOnlineStatus)}</Descriptions.Item>
                <Descriptions.Item label="Fehler">{fmtDetail(safeOperationalDetail.finanzOnlineError)}</Descriptions.Item>
                <Descriptions.Item label="Referenz-ID">
                  <Typography.Text code copyable>
                    {fmtDetail(safeOperationalDetail.finanzOnlineReferenceId)}
                  </Typography.Text>
                </Descriptions.Item>
                <Descriptions.Item label="Letzter Versuch (UTC)">
                  {fmtDetail(paymentDetailData.finanzOnlineLastAttemptAtUtc)}
                </Descriptions.Item>
                <Descriptions.Item label="Retries">{fmtDetail(paymentDetailData.finanzOnlineRetryCount)}</Descriptions.Item>
              </Descriptions>
            </Card>

            <Collapse
              size="small"
              items={[
                {
                  key: 'raw',
                  label: 'Rohe API-Antwort (Technik)',
                  children: (
                    <Typography.Paragraph copyable style={{ marginBottom: 0, fontSize: 11 }}>
                      {JSON.stringify(paymentDetailData, null, 2)}
                    </Typography.Paragraph>
                  ),
                },
              ]}
              style={{ marginBottom: 16 }}
            />

            <Space direction="vertical" style={{ width: '100%' }} size="middle">
              <Button type="primary" onClick={openReceipt} disabled={!canOpenReceipt}>
                Beleg öffnen
              </Button>

              {!canCancel && (
                <Alert
                  type="info"
                  showIcon
                  message="Storno nicht erlaubt"
                  description="Für Storno fehlt die erforderliche Berechtigung."
                />
              )}
              {canCancel && (
                <Card size="small" title="Storno">
                  <Space direction="vertical" style={{ width: '100%' }}>
                    <Input
                      placeholder="Storno-Grund"
                      value={cancelReason}
                      onChange={(e) => setCancelReason(e.target.value)}
                    />
                    <Button
                      danger
                      loading={cancelMutation.isPending}
                      disabled={!cancelReason.trim()}
                      onClick={() =>
                        Modal.confirm({
                          title: 'Zahlung stornieren?',
                          content: 'Diese Aktion storniert die Zahlung. Fortfahren?',
                          okText: 'Stornieren',
                          okButtonProps: { danger: true },
                          cancelText: 'Abbrechen',
                          onOk: () => cancelMutation.mutate(),
                        })
                      }
                    >
                      Storno ausführen
                    </Button>
                  </Space>
                </Card>
              )}

              {!canRefund && (
                <Alert
                  type="info"
                  showIcon
                  message="Refund nicht erlaubt"
                  description="Für Rückerstattungen fehlt die erforderliche Berechtigung."
                />
              )}
              {canRefund && (
                <Card size="small" title="Rückerstattung">
                  <Space direction="vertical" style={{ width: '100%' }}>
                    <InputNumber
                      min={0.01}
                      precision={2}
                      placeholder="Betrag"
                      value={refundAmount ?? undefined}
                      onChange={(v) => setRefundAmount(typeof v === 'number' ? v : null)}
                      style={{ width: '100%' }}
                    />
                    <Input
                      placeholder="Grund für Rückerstattung"
                      value={refundReason}
                      onChange={(e) => setRefundReason(e.target.value)}
                    />
                    <Button
                      loading={refundMutation.isPending}
                      disabled={!refundReason.trim() || !refundAmount || refundAmount <= 0}
                      onClick={() =>
                        Modal.confirm({
                          title: 'Rückerstattung ausführen?',
                          content: 'Diese Aktion erstellt eine Rückerstattung für die gewählte Zahlung. Fortfahren?',
                          okText: 'Rückerstattung ausführen',
                          cancelText: 'Abbrechen',
                          onOk: () => refundMutation.mutate(),
                        })
                      }
                    >
                      Rückerstattung ausführen
                    </Button>
                  </Space>
                </Card>
              )}
            </Space>
          </>
        ) : (
          <Alert type="warning" showIcon message="Keine Detaildaten verfügbar" />
        )}
      </Drawer>
    </AdminPageShell>
  );
}
