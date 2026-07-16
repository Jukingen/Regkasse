'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
/**
 * Admin odeme listesi ve detay cekmecesi; metinler payments namespace, sayi/tarih/para formatLocale ile.
 */
import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Table, Card, Typography, Tag, Space, Button, Drawer, Descriptions, Alert, Statistic, Row, Col, Input, InputNumber, Collapse, Empty } from 'antd';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell, AdminPageScopeSummary } from '@/components/admin-layout/AdminPageShell';
import { ADMIN_NAV_LABEL_KEYS, adminOverviewCrumb } from '@/shared/adminShellLabels';
import { CreditCardOutlined, InfoCircleOutlined, ReloadOutlined } from '@ant-design/icons';
import { OPERATOR_LINK_LABELS } from '@/shared/operatorTruthCopy';
import {
  postApiAdminPaymentsIdRefund,
  useGetApiAdminPaymentsId,
  useGetApiAdminPaymentsStatistics,
} from '@/api/generated/admin/admin';
import type { AdminPaymentDetailDto, AdminPaymentListItemDto } from '@/api/generated/model';
import { RefundReasonCode } from '@/api/generated/model/refundReasonCode';
import { useAdminPaymentsList } from '@/features/payments/api/adminPaymentsListQuery';
import { PaymentFilterBar } from '@/features/payments/components/PaymentFilterBar';
import { useKeysetCursors } from '@/shared/pagination/useKeysetPageStack';
import type { PaymentFilters } from '@/features/payments/types/paymentFilters';
import { countActivePaymentFilters } from '@/features/payments/utils/countActivePaymentFilters';
import {
  buildPaymentListSearchParams,
  createDefaultPaymentFilters,
  parsePaymentFiltersFromSearchParams,
  parsePaymentPaginationFromSearchParams,
} from '@/features/payments/utils/paymentFilterUrl';
import { paymentFiltersToApiParams } from '@/features/payments/utils/paymentFiltersToApiParams';
import dayjs from 'dayjs';
import { useMutation } from '@tanstack/react-query';
import { useRouter, useSearchParams } from 'next/navigation';
import Link from 'next/link';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { getReceiptByPaymentForensics } from '@/features/receipts/api/forensics-client';
import { useI18n } from '@/i18n';
import { ApiErrorAlertDescription } from '@/shared/errors/ApiErrorAlertDescription';
import { openApiErrorMessage } from '@/shared/errors/openApiErrorMessage';
import { ReprintButton } from '@/features/payments/components/ReprintButton';
import { CancellationModal } from '@/features/payments/components/CancellationModal';
import { adminTableScrollXy, shouldUseAdminTableVirtual } from '@/components/ui/adminTableVirtual';
import {
  FORMAT_EMPTY_DISPLAY,
  createIntlFormatters,
  formatCurrency,
  formatDateTime,
} from '@/i18n/formatting';
import { useTenantLicenseStatus } from '@/features/license/hooks/useLicenseStatus';


const DEFAULT_LIST_PAGE_SIZE = 50;

const PAYMENT_STATUS_FILTER_VALUES = ['Success', 'Pending', 'Failed', 'Cancelled', 'Refunded'] as const;

interface PaymentStatisticsShape {
  totalPayments?: number;
  totalAmount?: number;
  averageAmount?: number;
  tseSignedPayments?: number;
  finanzOnlineSentPayments?: number;
  finanzOnlineSentAmount?: number;
}

type IntlFormatters = ReturnType<typeof createIntlFormatters>;

function getVoucherRedeemedAmount(value: unknown): number {
  if (!value || typeof value !== 'object') return 0;
  const raw = (value as Record<string, unknown>).voucherRedeemedAmount;
  if (typeof raw !== 'number' || !Number.isFinite(raw) || raw <= 0) return 0;
  return raw;
}

function getSettlementAmount(value: unknown, fallbackTotal: number): number {
  if (!value || typeof value !== 'object') return fallbackTotal;
  const raw = (value as Record<string, unknown>).settlementAmount;
  if (typeof raw !== 'number' || !Number.isFinite(raw)) return fallbackTotal;
  return raw;
}

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

export default function PaymentsPage() {
  const { message, modal } = useAntdApp();

  const router = useRouter();
  const searchParams = useSearchParams();
  const { t, formatLocale } = useI18n();
  const fmt = useMemo(() => createIntlFormatters(formatLocale), [formatLocale]);

  const paymentStatusUiLabel = useCallback(
    (status: string | null | undefined) => {
      const s = status?.trim();
      if (!s) return FORMAT_EMPTY_DISPLAY;
      const keyMap: Record<string, string> = {
        Success: 'payments.statusLabels.payment.Success',
        Pending: 'payments.statusLabels.payment.Pending',
        Failed: 'payments.statusLabels.payment.Failed',
        Cancelled: 'payments.statusLabels.payment.Cancelled',
        Refunded: 'payments.statusLabels.payment.Refunded',
      };
      const path = keyMap[s];
      return path ? t(path) : s;
    },
    [t],
  );

  const finanzOnlineStatusUiLabel = useCallback(
    (status: string | null | undefined) => {
      const s = status?.trim();
      if (!s) return FORMAT_EMPTY_DISPLAY;
      const keyMap: Record<string, string> = {
        Submitted: 'payments.statusLabels.finanzOnline.Submitted',
        Pending: 'payments.statusLabels.finanzOnline.Pending',
        Failed: 'payments.statusLabels.finanzOnline.Failed',
        NeedsReconciliation: 'payments.statusLabels.finanzOnline.NeedsReconciliation',
      };
      const path = keyMap[s];
      return path ? t(path) : s;
    },
    [t],
  );

  const detailStr = useCallback(
    (value: unknown) => formatDetailValue(value, fmt, t('payments.display.yes'), t('payments.display.no')),
    [fmt, t],
  );

  const { hasPermission } = usePermissions();
  const canCancel = hasPermission(PERMISSIONS.PAYMENT_CANCEL);
  const canRefund = hasPermission(PERMISSIONS.REFUND_CREATE);
  const canOpenReceipt = hasPermission(PERMISSIONS.SALE_VIEW);
  const { data: tenantLicense } = useTenantLicenseStatus();
  const isPaymentBlockedByLicense =
    tenantLicense?.kind === 'grace_readonly' ||
    tenantLicense?.kind === 'lockdown';

  const initialPagination = useMemo(
    () => parsePaymentPaginationFromSearchParams(searchParams ?? new URLSearchParams()),
    [searchParams],
  );
  const [filters, setFilters] = useState<PaymentFilters>(() =>
    parsePaymentFiltersFromSearchParams(searchParams ?? new URLSearchParams()),
  );
  const [page, setPage] = useState(initialPagination.page);
  const [pageSize, setPageSize] = useState(initialPagination.pageSize);
  const { getAfterCursor, shouldIncludeTotalCount, cachedTotal, ingestPageMeta, resetCursors } =
    useKeysetCursors();
  const [selectedPaymentId, setSelectedPaymentId] = useState<string | null>(null);
  const [cancelModalOpen, setCancelModalOpen] = useState(false);
  const [refundReason, setRefundReason] = useState('');
  const [refundAmount, setRefundAmount] = useState<number | null>(null);
  useEffect(() => {
    const pid = searchParams?.get('paymentId')?.trim();
    if (pid) setSelectedPaymentId(pid);
  }, [searchParams]);

  const filtersSerialized = useMemo(() => JSON.stringify(filters), [filters]);
  const prevFiltersSerialized = useRef(filtersSerialized);

  useEffect(() => {
    if (prevFiltersSerialized.current !== filtersSerialized) {
      setPage(1);
      resetCursors();
      prevFiltersSerialized.current = filtersSerialized;
    }
  }, [filtersSerialized, resetCursors]);

  useEffect(() => {
    if (!searchParams) return;
    const built = buildPaymentListSearchParams(filters, { page, pageSize }, new URLSearchParams());
    const paymentId = searchParams.get('paymentId');
    if (paymentId) built.set('paymentId', paymentId);
    const nextQuery = built.toString();
    const currentQuery = searchParams.toString();
    if (nextQuery !== currentQuery) {
      router.replace(nextQuery ? `?${nextQuery}` : window.location.pathname, { scroll: false });
    }
  }, [filters, page, pageSize, router, searchParams]);

  const handleFilterChange = useCallback((next: PaymentFilters) => {
    setFilters(Object.keys(next).length === 0 ? createDefaultPaymentFilters() : next);
  }, []);

  const listParams = useMemo(
    () =>
      paymentFiltersToApiParams(filters, {
        page,
        pageSize,
        afterCursor: getAfterCursor(page),
        includeTotalCount: shouldIncludeTotalCount(page),
      }),
    [filters, page, pageSize, getAfterCursor, shouldIncludeTotalCount],
  );


  const { data, isLoading, isPlaceholderData, isError, error, refetch } = useAdminPaymentsList(listParams);

  useEffect(() => {
    if (!data) return;
    ingestPageMeta(page, {
      nextCursor: data.nextCursor,
      hasMore: data.hasMore,
      totalCount: data.total,
    });
  }, [data, page, ingestPageMeta]);
  const { data: statsRaw, isLoading: statsLoading } = useGetApiAdminPaymentsStatistics({
    startDate: listParams.startDate,
    endDate: listParams.endDate,
  });
  const { data: paymentDetail, isLoading: detailLoading, refetch: refetchPaymentDetail } = useGetApiAdminPaymentsId(selectedPaymentId ?? '', {
    query: { enabled: !!selectedPaymentId },
  });
  const paymentDetailData = paymentDetail as AdminPaymentDetailDto | undefined;

  const payments: AdminPaymentListItemDto[] = useMemo(() => data?.items ?? [], [data?.items]);
  const totalCount = cachedTotal ?? data?.total ?? payments.length;

  const stats: PaymentStatisticsShape | null =
    statsRaw && typeof statsRaw === 'object' ? (statsRaw as PaymentStatisticsShape) : null;
  const operationalDetail = paymentDetailData;
  const safeOperationalDetail: AdminPaymentDetailDto = operationalDetail ?? ({} as AdminPaymentDetailDto);

  const paymentsScopeSummary = useMemo(() => {
    const start = filters.dateRange?.[0] ?? dayjs().subtract(30, 'day');
    const end = filters.dateRange?.[1] ?? dayjs();
    const startStr = fmt.formatDate(start.toDate());
    const endStr = fmt.formatDate(end.toDate());
    const parts = [
      `${startStr}–${endStr}`,
      t('payments.scope.serverLine', { count: totalCount }),
      t('payments.scope.filteredLine', { count: payments.length }),
    ];
    const activeCount = countActivePaymentFilters(filters);
    if (activeCount > 0) {
      parts.push(t('payments.scope.activeFiltersLine', { count: activeCount }));
    }
    return parts.join(' · ');
  }, [filters, totalCount, payments.length, fmt, t]);

  const refundMutation = useMutation({
    mutationFn: async () => {
      if (!selectedPaymentId) throw new Error(t('payments.messages.errorNoPaymentSelected'));
      if (!refundAmount || refundAmount <= 0) throw new Error(t('payments.messages.errorRefundAmountPositive'));
      return postApiAdminPaymentsIdRefund(selectedPaymentId, {
        amount: refundAmount,
        reason: refundReason.trim(),
        reasonCode: RefundReasonCode.NUMBER_99,
      });
    },
    onSuccess: async () => {
      message.success(t('payments.messages.refundSuccess'));
      setRefundAmount(null);
      setRefundReason('');
      await refetch();
    },
    onError: (err: unknown) => {
      openApiErrorMessage(message.open, t, err, {
        logContext: 'PaymentsPage.refundPayment',
        fallbackKey: 'payments.messages.refundError',
      });
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

  const availableMethods = useMemo(() => {
    const fromApi = data?.activeFilters?.availablePaymentMethods;
    if (fromApi && fromApi.length > 0) return fromApi;
    const fromItems = Array.from(new Set(payments.map((p) => p.method).filter(Boolean))) as string[];
    return fromItems;
  }, [data?.activeFilters?.availablePaymentMethods, payments]);

  const availableStatuses = useMemo(() => {
    const fromApi = data?.activeFilters?.availableStatuses;
    if (fromApi && fromApi.length > 0) return fromApi;
    return [...PAYMENT_STATUS_FILTER_VALUES];
  }, [data?.activeFilters?.availableStatuses]);

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
        render: (method: string, row: AdminPaymentListItemDto) => {
          const voucherRedeemed = getVoucherRedeemedAmount(row);
          return (
            <Space size={4} wrap>
              <Tag color="blue">{method || FORMAT_EMPTY_DISPLAY}</Tag>
              {voucherRedeemed > 0 ? (
                <Tag color="purple">
                  {t('payments.table.mixedVoucherTag', {
                    amount: formatCurrency(voucherRedeemed, formatLocale, { currency: row.currency || 'EUR' }),
                  })}
                </Tag>
              ) : null}
            </Space>
          );
        },
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
          const label = paymentStatusUiLabel(status);
          return <Tag color={colors[status] || 'default'}>{label}</Tag>;
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
            <Space orientation="vertical" size={6} style={{ maxWidth: 292 }}>
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
                <Space orientation="vertical" size={2} style={{ width: '100%' }}>
                  <Tag color={foColor}>
                    {t('payments.table.foTagPrefix')} {finanzOnlineStatusUiLabel(row.finanzOnlineStatus)}
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
    [t, formatLocale, paymentStatusUiLabel, finanzOnlineStatusUiLabel]
  );

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t(ADMIN_NAV_LABEL_KEYS.payments)}
        breadcrumbs={[adminOverviewCrumb(t), { title: t(ADMIN_NAV_LABEL_KEYS.payments) }]}
        actions={
          <Space wrap>
            <Button icon={<ReloadOutlined />} onClick={() => refetch()} loading={isLoading}>
              {t('payments.toolbar.refresh')}
            </Button>
            <Button
              icon={<CreditCardOutlined />}
              href="/admin/payments/card-transactions"
            >
              {t('payments.toolbar.terminalStatus')}
            </Button>
          </Space>
        }
      >
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0, maxWidth: 640 }}>
          {t('payments.page.intro')}
        </Typography.Paragraph>
      </AdminPageHeader>

      <PaymentFilterBar
        filters={filters}
        onFilterChange={handleFilterChange}
        availableMethods={availableMethods}
        availableStatuses={availableStatuses}
      />

      <AdminPageScopeSummary label={t('payments.scope.label')}>{paymentsScopeSummary}</AdminPageScopeSummary>

      {isError ? (
        <Alert
          type="error"
          showIcon
          title={t('payments.list.loadErrorTitle')}
          description={
            error ? (
              <ApiErrorAlertDescription
                t={t}
                error={error}
                logContext="PaymentsPage.list"
                fallbackKey="payments.list.loadErrorFallback"
              />
            ) : (
              t('payments.list.loadErrorFallback')
            )
          }
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
              value={stats?.totalPayments ?? totalCount}
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
        dataSource={payments}
        loading={isLoading && !isPlaceholderData}
        rowKey="id"
        virtual={shouldUseAdminTableVirtual(payments.length)}
        scroll={adminTableScrollXy(1280, payments.length)}
        style={{
          opacity: isPlaceholderData ? 0.6 : 1,
          transition: 'opacity 0.2s',
        }}
        pagination={{
          current: page,
          pageSize,
          total: totalCount,
          showSizeChanger: true,
          showQuickJumper: false,
          pageSizeOptions: ['25', '50', '100'],
          onChange: (p, ps) => {
            setPage(p);
            if (ps != null) setPageSize(ps);
          },
        }}
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
        size={640}
        destroyOnHidden
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
                {(() => {
                  const voucherRedeemed = getVoucherRedeemedAmount(paymentDetailData);
                  if (voucherRedeemed <= 0) return null;
                  const settlementAmount = getSettlementAmount(paymentDetailData, Number(paymentDetailData.totalAmount ?? 0));
                  return (
                    <>
                      <Descriptions.Item label={t('payments.detail.labelVoucherRedeemed')}>
                        {formatCurrency(voucherRedeemed, formatLocale, {
                          currency: paymentDetailData.currency || 'EUR',
                        })}
                      </Descriptions.Item>
                      <Descriptions.Item label={t('payments.detail.labelSettlementAmount')}>
                        {formatCurrency(settlementAmount, formatLocale, {
                          currency: paymentDetailData.currency || 'EUR',
                        })}
                      </Descriptions.Item>
                      <Descriptions.Item label={t('payments.detail.labelMixedPaymentHint')}>
                        {t('payments.detail.mixedPaymentHint')}
                      </Descriptions.Item>
                    </>
                  );
                })()}
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
                <Descriptions.Item label={t('payments.detail.labelFoStatus')}>
                  {finanzOnlineStatusUiLabel(safeOperationalDetail.finanzOnlineStatus)}
                </Descriptions.Item>
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

            <Space orientation="vertical" style={{ width: '100%' }} size="middle">
              <Space wrap>
                <Button type="primary" onClick={openReceipt} disabled={!canOpenReceipt}>
                  {t('payments.detail.buttonOpenReceipt')}
                </Button>
                <ReprintButton paymentId={selectedPaymentId} receiptNumber={paymentDetailData?.receiptNumber} />
              </Space>

              {isPaymentBlockedByLicense ? (
                <Alert
                  type="error"
                  showIcon
                  title={t('payments.detail.licenseBlockedTitle')}
                  description={t('payments.detail.licenseBlockedDesc')}
                />
              ) : null}

              {!canCancel && (
                <Alert
                  type="info"
                  showIcon
                  title={t('payments.detail.cancelPermissionTitle')}
                  description={t('payments.detail.cancelPermissionDesc')}
                />
              )}
              {canCancel && (
                <Card size="small" title={t('payments.detail.cancelCardTitle')}>
                  <Space orientation="vertical" style={{ width: '100%' }}>
                    {paymentDetailData?.isStorno || paymentDetailData?.hasStornoReversal ? (
                      <Alert
                        type="info"
                        showIcon
                        title={t('payments.cancellationModal.alreadyCancelled')}
                      />
                    ) : (
                      <Button
                        danger
                        disabled={isPaymentBlockedByLicense || !paymentDetailData?.id}
                        onClick={() => setCancelModalOpen(true)}
                      >
                        {t('payments.detail.cancelButton')}
                      </Button>
                    )}
                  </Space>
                </Card>
              )}

              {paymentDetailData?.id ? (
                <CancellationModal
                  payment={paymentDetailData}
                  open={cancelModalOpen}
                  onClose={() => setCancelModalOpen(false)}
                  onSuccess={async () => {
                    await refetch();
                    await refetchPaymentDetail();
                  }}
                  disabled={isPaymentBlockedByLicense}
                />
              ) : null}

              {!canRefund && (
                <Alert
                  type="info"
                  showIcon
                  title={t('payments.detail.refundPermissionTitle')}
                  description={t('payments.detail.refundPermissionDesc')}
                />
              )}
              {canRefund && (
                <Card size="small" title={t('payments.detail.refundCardTitle')}>
                  <Space orientation="vertical" style={{ width: '100%' }}>
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
                      disabled={
                        !refundReason.trim() ||
                        !refundAmount ||
                        refundAmount <= 0 ||
                        isPaymentBlockedByLicense
                      }
                      onClick={() =>
                        modal.confirm({
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
          <Alert type="warning" showIcon title={t('payments.drawer.noDetailData')} />
        )}
      </Drawer>
    </AdminPageShell>
  );
}
