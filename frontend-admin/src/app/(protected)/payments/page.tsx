'use client';

/**
 * Admin odeme listesi ve detay cekmecesi; metinler payments namespace, sayi/tarih/para formatLocale ile.
 */
import React, { useCallback, useEffect, useMemo, useState } from 'react';
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
import { OPERATOR_LINK_LABELS } from '@/shared/operatorTruthCopy';
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
import { useI18n } from '@/i18n';
import {
  FORMAT_EMPTY_DISPLAY,
  createIntlFormatters,
  formatCurrency,
  formatDateTime,
} from '@/i18n/formatting';

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

type IntlFormatters = ReturnType<typeof createIntlFormatters>;

function formatDetailValue(value: unknown, fmt: IntlFormatters, yes: string, no: string): string {
  if (value === null || value === undefined) return FORMAT_EMPTY_DISPLAY;
  if (typeof value === 'boolean') return value ? yes : no;
  if (typeof value === 'number') return Number.isFinite(value) ? fmt.formatNumber(value) : FORMAT_EMPTY_DISPLAY;
  if (typeof value === 'string') return value.trim() === '' ? FORMAT_EMPTY_DISPLAY : value;
  if (typeof value === 'object') return JSON.stringify(value);
  return String(value);
}

function shortId(value?: string | null): string {
  if (!value) return FORMAT_EMPTY_DISPLAY;
  return value.length > 12 ? `${value.slice(0, 8)}…` : value;
}

function getPaymentsListErrorMessage(error: unknown, fallback: string): string {
  if (error instanceof Error && error.message.trim()) return error.message.trim();
  const norm = (error as { normalized?: { message?: string } })?.normalized;
  if (norm?.message?.trim()) return norm.message.trim();
  const msg = (error as { message?: string })?.message;
  if (typeof msg === 'string' && msg.trim()) return msg.trim();
  return fallback;
}

export default function PaymentsPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { t, formatLocale } = useI18n();
  const fmt = useMemo(() => createIntlFormatters(formatLocale), [formatLocale]);

  const detailStr = useCallback(
    (value: unknown) => formatDetailValue(value, fmt, t('payments.display.yes'), t('payments.display.no')),
    [fmt, t],
  );

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
    const startStr = fmt.formatDate(dateRange[0].toDate());
    const endStr = fmt.formatDate(dateRange[1].toDate());
    const parts = [
      `${startStr}–${endStr}`,
      t('payments.scope.serverLine', { count: payments.length }),
      t('payments.scope.filteredLine', { count: filteredPayments.length }),
    ];
    if (methodFilter) parts.push(t('payments.scope.methodLine', { value: methodFilter }));
    if (statusFilter) parts.push(t('payments.scope.statusLine', { value: statusFilter }));
    return parts.join(' · ');
  }, [dateRange, payments.length, filteredPayments.length, methodFilter, statusFilter, fmt, t]);

  const loadErrorFallback = t('payments.list.loadErrorFallback');

  const cancelMutation = useMutation({
    mutationFn: async () => {
      if (!selectedPaymentId) throw new Error(t('payments.messages.errorNoPaymentSelected'));
      return postApiAdminPaymentsIdCancel(selectedPaymentId, { reason: cancelReason.trim() });
    },
    onSuccess: async () => {
      message.success(t('payments.messages.cancelSuccess'));
      setCancelReason('');
      await refetch();
    },
    onError: (err: Error) => {
      // If Error.message is a backend string, show it; otherwise localized fallback.
      message.error(err?.message ?? t('payments.messages.cancelError'));
    },
  });

  const refundMutation = useMutation({
    mutationFn: async () => {
      if (!selectedPaymentId) throw new Error(t('payments.messages.errorNoPaymentSelected'));
      if (!refundAmount || refundAmount <= 0) throw new Error(t('payments.messages.errorRefundAmountPositive'));
      return postApiAdminPaymentsIdRefund(selectedPaymentId, { amount: refundAmount, reason: refundReason.trim() });
    },
    onSuccess: async () => {
      message.success(t('payments.messages.refundSuccess'));
      setRefundAmount(null);
      setRefundReason('');
      await refetch();
    },
    onError: (err: Error) => {
      message.error(err?.message ?? t('payments.messages.refundError'));
    },
  });

  const openReceipt = async () => {
    const receiptId = paymentDetailData?.receiptId;
    if (receiptId) {
      router.push(`/receipts/${receiptId}`);
      return;
    }
    if (!selectedPaymentId) {
      message.warning(t('payments.messages.noPaymentSelected'));
      return;
    }
    try {
      const receipt = await getReceiptByPaymentForensics(selectedPaymentId);
      if (!receipt.receiptId) {
        message.warning(t('payments.messages.noReceiptForPayment'));
        return;
      }
      router.push(`/receipts/${receipt.receiptId}`);
    } catch {
      message.warning(t('payments.messages.noReceiptForPayment'));
    }
  };

  const methodOptions = Array.from(new Set(payments.map((p) => p.method).filter(Boolean))) as string[];
  const statusOptions = Array.from(new Set(payments.map((p) => p.status).filter(Boolean))) as string[];

  const columns = useMemo(
    () => [
      {
        title: t('payments.table.colTransactionId'),
        dataIndex: 'transactionId',
        key: 'transactionId',
        render: (text: string) => <code style={{ fontSize: '12px' }}>{text || FORMAT_EMPTY_DISPLAY}</code>,
      },
      {
        title: t('payments.table.colDate'),
        dataIndex: 'createdAt',
        key: 'createdAt',
        render: (date: string) => (date ? formatDateTime(date, formatLocale) : FORMAT_EMPTY_DISPLAY),
      },
      {
        title: t('payments.table.colAmount'),
        dataIndex: 'totalAmount',
        key: 'amount',
        align: 'right' as const,
        render: (val: number, record: AdminPaymentListItemDto) =>
          formatCurrency(val ?? 0, formatLocale, { currency: record.currency || 'EUR' }),
      },
      {
        title: t('payments.table.colMethod'),
        dataIndex: 'method',
        key: 'method',
        render: (method: string) => <Tag color="blue">{method || FORMAT_EMPTY_DISPLAY}</Tag>,
      },
      {
        title: t('payments.table.colStatus'),
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
          return <Tag color={colors[status] || 'default'}>{status || FORMAT_EMPTY_DISPLAY}</Tag>;
        },
      },
      {
        title: t('payments.table.colLinkedEntities'),
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
            return <Typography.Text type="secondary">{FORMAT_EMPTY_DISPLAY}</Typography.Text>;
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
                  {t('payments.table.paymentIdPrefix')}{' '}
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
                        {t('payments.table.tagReceipt')}
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
                      <Tag color="purple">{t('payments.table.tagInvoice')}</Tag>
                    </Link>
                  ) : null}
                  {row.offlineReplayBatchCorrelationId ? (
                    <Link
                      href={`/rksv/incident?correlationId=${encodeURIComponent(row.offlineReplayBatchCorrelationId)}`}
                      target="_blank"
                      rel="noopener noreferrer"
                    >
                      <Tag color="orange">{t('payments.table.tagIncident')}</Tag>
                    </Link>
                  ) : null}
                </Space>
              ) : null}
              {hasFo ? (
                <Space direction="vertical" size={2} style={{ width: '100%' }}>
                  <Tag color={foColor}>
                    {t('payments.table.foTagPrefix')} {row.finanzOnlineStatus}
                  </Tag>
                  {foErr ? (
                    <Typography.Text
                      type="danger"
                      ellipsis={{ tooltip: foErr }}
                      style={{ fontSize: 11, maxWidth: 280 }}
                      title={t('payments.detail.backendMessageTooltip')}
                    >
                      {/* RAW: FinanzOnline provider/backend error string; may be English or German */}
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
        title: t('payments.table.colActions'),
        key: 'actions',
        render: (_: unknown, row: AdminPaymentListItemDto) => (
          <Button size="small" icon={<InfoCircleOutlined />} onClick={() => setSelectedPaymentId(row.id ?? null)}>
            {t('payments.table.details')}
          </Button>
        ),
      },
    ],
    [t, formatLocale]
  );

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={ADMIN_NAV_LABELS.payments}
        breadcrumbs={[ADMIN_OVERVIEW_CRUMB, { title: ADMIN_NAV_LABELS.payments }]}
        actions={
          <Space wrap>
            <Button icon={<ReloadOutlined />} onClick={() => refetch()} loading={isLoading}>
              {t('payments.toolbar.refresh')}
            </Button>
            <Button icon={<CreditCardOutlined />}>{t('payments.toolbar.terminalStatus')}</Button>
          </Space>
        }
      >
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0, maxWidth: 640 }}>
          {t('payments.page.intro')}
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
            placeholder={t('payments.filters.methodPlaceholder')}
            allowClear
            value={methodFilter}
            onChange={(v) => setMethodFilter(v)}
            style={{ width: 160 }}
            options={methodOptions.map((m) => ({ value: m, label: m }))}
          />
          <Select
            placeholder={t('payments.filters.statusPlaceholder')}
            allowClear
            value={statusFilter}
            onChange={(v) => setStatusFilter(v)}
            style={{ width: 180 }}
            options={statusOptions.map((s) => ({ value: s, label: s }))}
          />
        </Space>
      </Card>

      <AdminPageScopeSummary label={t('payments.scope.label')}>{paymentsScopeSummary}</AdminPageScopeSummary>

      {isError ? (
        <Alert
          type="error"
          showIcon
          message={t('payments.list.loadErrorTitle')}
          description={getPaymentsListErrorMessage(error, loadErrorFallback)}
          action={
            <Button size="small" onClick={() => refetch()}>
              {t('payments.toolbar.retryAfterError')}
            </Button>
          }
        />
      ) : null}

      {!isError ? (
      <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
        <Col xs={24} sm={12} md={6}>
          <Card size="small">
            <Statistic
              title={t('payments.stats.paymentCount')}
              value={stats?.totalPayments ?? filteredPayments.length}
              loading={statsLoading}
              formatter={(v) => fmt.formatNumber(Number(v), { maximumFractionDigits: 0 })}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} md={6}>
          <Card size="small">
            <Statistic
              title={t('payments.stats.totalAmount')}
              value={stats?.totalAmount ?? 0}
              loading={statsLoading}
              formatter={(v) => formatCurrency(Number(v), formatLocale)}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} md={6}>
          <Card size="small">
            <Statistic
              title={t('payments.stats.average')}
              value={stats?.averageAmount ?? 0}
              loading={statsLoading}
              formatter={(v) => formatCurrency(Number(v), formatLocale)}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} md={6}>
          <Card size="small">
            <Statistic
              title={t('payments.stats.tseSigned')}
              value={stats?.tseSignedPayments ?? 0}
              loading={statsLoading}
              formatter={(v) => fmt.formatNumber(Number(v), { maximumFractionDigits: 0 })}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} md={6}>
          <Card size="small">
            <Statistic
              title={t('payments.stats.finanzOnlineSent')}
              value={stats?.finanzOnlineSentPayments ?? 0}
              loading={statsLoading}
              formatter={(v) => fmt.formatNumber(Number(v), { maximumFractionDigits: 0 })}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} md={6}>
          <Card size="small">
            <Statistic
              title={t('payments.stats.foSentAmount')}
              value={stats?.finanzOnlineSentAmount ?? 0}
              loading={statsLoading}
              formatter={(v) => formatCurrency(Number(v), formatLocale)}
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
              description={t('payments.empty.description')}
            />
          ),
        }}
      />
      ) : null}

      <Drawer
        title={t('payments.drawer.title')}
        open={!!selectedPaymentId}
        onClose={() => setSelectedPaymentId(null)}
        width={640}
        destroyOnClose
      >
        {detailLoading ? (
          <Typography.Text type="secondary">{t('payments.drawer.loadingDetails')}</Typography.Text>
        ) : paymentDetailData ? (
          <>
            <Card size="small" title={t('payments.detail.sectionCore')} style={{ marginBottom: 12 }}>
              <Descriptions size="small" column={1} bordered>
                <Descriptions.Item label={t('payments.detail.labelPaymentId')}>
                  <Typography.Text code copyable>
                    {selectedPaymentId}
                  </Typography.Text>
                </Descriptions.Item>
                <Descriptions.Item label={t('payments.detail.labelTransaction')}>
                  <Typography.Text code copyable>
                    {detailStr(paymentDetailData.transactionId)}
                  </Typography.Text>
                </Descriptions.Item>
                <Descriptions.Item label={t('payments.detail.labelReceiptNumber')}>
                  <Typography.Text code copyable>
                    {detailStr(paymentDetailData.receiptNumber)}
                  </Typography.Text>
                </Descriptions.Item>
                <Descriptions.Item label={t('payments.detail.labelTimestampServer')}>
                  {paymentDetailData.createdAt
                    ? formatDateTime(paymentDetailData.createdAt, formatLocale)
                    : FORMAT_EMPTY_DISPLAY}
                </Descriptions.Item>
                <Descriptions.Item label={t('payments.detail.labelAmount')}>
                  {paymentDetailData.totalAmount != null && Number.isFinite(paymentDetailData.totalAmount)
                    ? formatCurrency(paymentDetailData.totalAmount, formatLocale, {
                        currency: paymentDetailData.currency || 'EUR',
                      })
                    : FORMAT_EMPTY_DISPLAY}
                </Descriptions.Item>
                <Descriptions.Item label={t('payments.detail.labelPaymentMethodRaw')}>
                  {detailStr(paymentDetailData.paymentMethodRaw ?? paymentDetailData.method)}
                </Descriptions.Item>
                <Descriptions.Item label={t('payments.detail.labelCashRegisterFk')}>
                  <Typography.Text code copyable>
                    {detailStr(paymentDetailData.cashRegisterId)}
                  </Typography.Text>
                </Descriptions.Item>
                <Descriptions.Item label={t('payments.detail.labelCustomerName')}>{detailStr(paymentDetailData.customerName)}</Descriptions.Item>
                <Descriptions.Item label={t('payments.detail.labelIdempotencyKey')}>
                  <Typography.Text code copyable>
                    {detailStr(paymentDetailData.idempotencyKey)}
                  </Typography.Text>
                </Descriptions.Item>
              </Descriptions>
            </Card>

            <Card size="small" title={t('payments.detail.sectionLinked')} style={{ marginBottom: 12 }}>
              <Descriptions size="small" column={1} bordered>
                <Descriptions.Item label={t('payments.detail.labelPaymentId')}>
                  <Typography.Text code copyable>
                    {detailStr(paymentDetailData.id)}
                  </Typography.Text>
                </Descriptions.Item>
                <Descriptions.Item label={t('payments.detail.labelReceiptId')}>
                  {safeOperationalDetail.receiptId ? (
                    <Space wrap>
                      <Typography.Text code copyable>{safeOperationalDetail.receiptId}</Typography.Text>
                      <Link href={`/receipts/${safeOperationalDetail.receiptId}`} target="_blank" rel="noopener noreferrer">
                        {t('payments.detail.open')}
                      </Link>
                    </Space>
                  ) : (
                    FORMAT_EMPTY_DISPLAY
                  )}
                </Descriptions.Item>
                <Descriptions.Item label={t('payments.detail.labelInvoice')}>
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
                          {t('payments.detail.open')}
                        </Link>
                      ) : null}
                    </Space>
                  ) : (
                    FORMAT_EMPTY_DISPLAY
                  )}
                </Descriptions.Item>
                <Descriptions.Item label={t('payments.detail.labelReplayCorrelation')}>
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
                    FORMAT_EMPTY_DISPLAY
                  )}
                </Descriptions.Item>
              </Descriptions>
            </Card>

            <Card size="small" title={t('payments.detail.sectionOrigin')} style={{ marginBottom: 12 }}>
              <Descriptions size="small" column={1} bordered>
                <Descriptions.Item label={t('payments.detail.labelPaymentOrigin')}>
                  {safeOperationalDetail.isOfflineOrigin
                    ? t('payments.detail.originOffline')
                    : t('payments.detail.originOnline')}
                </Descriptions.Item>
                <Descriptions.Item label={t('payments.detail.labelReceiptNumberSet')}>
                  {safeOperationalDetail.receiptNumber ? t('payments.display.yes') : t('payments.display.no')}
                </Descriptions.Item>
                <Descriptions.Item label={t('payments.detail.labelReceiptIdShort')}>
                  <Typography.Text code copyable>
                    {detailStr(safeOperationalDetail.receiptId)}
                  </Typography.Text>
                </Descriptions.Item>
                <Descriptions.Item label={t('payments.detail.labelInvoicePersistedApi')}>
                  {safeOperationalDetail.invoicePersisted == null
                    ? t('payments.detail.invoicePersistedLegacyMissing')
                    : detailStr(safeOperationalDetail.invoicePersisted)}
                </Descriptions.Item>
              </Descriptions>
            </Card>

            <Card size="small" title={t('payments.detail.sectionOfflineReplay')} style={{ marginBottom: 12 }}>
              <Typography.Paragraph type="secondary" style={{ marginBottom: 8, fontSize: 12 }}>
                {t('payments.detail.offlineReplayIntro')}
              </Typography.Paragraph>
              <Descriptions size="small" column={1} bordered>
                <Descriptions.Item label={t('payments.detail.labelOfflineTxId')}>
                  <Typography.Text code copyable>
                    {detailStr(safeOperationalDetail.offlineTransactionId)}
                  </Typography.Text>
                </Descriptions.Item>
                <Descriptions.Item label={t('payments.detail.labelReplayCorrelation')}>
                  <Typography.Text code copyable>
                    {detailStr(safeOperationalDetail.offlineReplayBatchCorrelationId)}
                  </Typography.Text>
                </Descriptions.Item>
              </Descriptions>
            </Card>

            <Card size="small" title={t('payments.detail.sectionFinanzOnline')} style={{ marginBottom: 12 }}>
              <Descriptions size="small" column={1} bordered>
                <Descriptions.Item label={t('payments.detail.labelFoStatus')}>{detailStr(safeOperationalDetail.finanzOnlineStatus)}</Descriptions.Item>
                <Descriptions.Item label={t('payments.detail.labelFoError')}>
                  {safeOperationalDetail.finanzOnlineError ? (
                    <Typography.Text
                      type="danger"
                      title={t('payments.detail.backendMessageTooltip')}
                    >
                      {/* RAW: API FinanzOnline error text; language not guaranteed */}
                      {safeOperationalDetail.finanzOnlineError}
                    </Typography.Text>
                  ) : (
                    FORMAT_EMPTY_DISPLAY
                  )}
                </Descriptions.Item>
                <Descriptions.Item label={t('payments.detail.labelFoReferenceId')}>
                  <Typography.Text code copyable>
                    {detailStr(safeOperationalDetail.finanzOnlineReferenceId)}
                  </Typography.Text>
                </Descriptions.Item>
                <Descriptions.Item label={t('payments.detail.labelFoLastAttemptUtc')}>
                  {paymentDetailData.finanzOnlineLastAttemptAtUtc
                    ? formatDateTime(paymentDetailData.finanzOnlineLastAttemptAtUtc, formatLocale)
                    : FORMAT_EMPTY_DISPLAY}
                </Descriptions.Item>
                <Descriptions.Item label={t('payments.detail.labelFoRetries')}>{detailStr(paymentDetailData.finanzOnlineRetryCount)}</Descriptions.Item>
              </Descriptions>
            </Card>

            <Collapse
              size="small"
              items={[
                {
                  key: 'raw',
                  label: t('payments.detail.rawApiCollapse'),
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
                {t('payments.detail.buttonOpenReceipt')}
              </Button>

              {!canCancel && (
                <Alert
                  type="info"
                  showIcon
                  message={t('payments.detail.cancelPermissionTitle')}
                  description={t('payments.detail.cancelPermissionDesc')}
                />
              )}
              {canCancel && (
                <Card size="small" title={t('payments.detail.cancelCardTitle')}>
                  <Space direction="vertical" style={{ width: '100%' }}>
                    <Input
                      placeholder={t('payments.detail.cancelReasonPlaceholder')}
                      value={cancelReason}
                      onChange={(e) => setCancelReason(e.target.value)}
                    />
                    <Button
                      danger
                      loading={cancelMutation.isPending}
                      disabled={!cancelReason.trim()}
                      onClick={() =>
                        Modal.confirm({
                          title: t('payments.detail.cancelModalTitle'),
                          content: t('payments.detail.cancelModalContent'),
                          okText: t('payments.detail.cancelOk'),
                          okButtonProps: { danger: true },
                          cancelText: t('payments.detail.cancelCancel'),
                          onOk: () => cancelMutation.mutate(),
                        })
                      }
                    >
                      {t('payments.detail.cancelButton')}
                    </Button>
                  </Space>
                </Card>
              )}

              {!canRefund && (
                <Alert
                  type="info"
                  showIcon
                  message={t('payments.detail.refundPermissionTitle')}
                  description={t('payments.detail.refundPermissionDesc')}
                />
              )}
              {canRefund && (
                <Card size="small" title={t('payments.detail.refundCardTitle')}>
                  <Space direction="vertical" style={{ width: '100%' }}>
                    <InputNumber
                      min={0.01}
                      precision={2}
                      placeholder={t('payments.detail.refundAmountPlaceholder')}
                      value={refundAmount ?? undefined}
                      onChange={(v) => setRefundAmount(typeof v === 'number' ? v : null)}
                      style={{ width: '100%' }}
                    />
                    <Input
                      placeholder={t('payments.detail.refundReasonPlaceholder')}
                      value={refundReason}
                      onChange={(e) => setRefundReason(e.target.value)}
                    />
                    <Button
                      loading={refundMutation.isPending}
                      disabled={!refundReason.trim() || !refundAmount || refundAmount <= 0}
                      onClick={() =>
                        Modal.confirm({
                          title: t('payments.detail.refundModalTitle'),
                          content: t('payments.detail.refundModalContent'),
                          okText: t('payments.detail.refundOk'),
                          cancelText: t('payments.detail.refundCancel'),
                          onOk: () => refundMutation.mutate(),
                        })
                      }
                    >
                      {t('payments.detail.refundButton')}
                    </Button>
                  </Space>
                </Card>
              )}
            </Space>
          </>
        ) : (
          <Alert type="warning" showIcon message={t('payments.drawer.noDetailData')} />
        )}
      </Drawer>
    </AdminPageShell>
  );
}
