'use client';

import { PlusOutlined } from '@ant-design/icons';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Button,
  Card,
  DatePicker,
  Form,
  Input,
  Modal,
  Space,
  Table,
  Tag,
} from 'antd';
import type { ColumnsType, TablePaginationConfig } from 'antd/es/table';
import type { FilterValue, SorterResult } from 'antd/es/table/interface';
import { type Dayjs } from 'dayjs';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import React, { useMemo, useState } from 'react';

import type { LicenseSaleResponse } from '@/api/generated/model';
import { dateColumnRender } from '@/components/DateColumn';
import { billingApi } from '@/features/billing/api/billingApi';
import { BillingSalesBulkBar } from '@/features/billing/components/BillingSalesBulkBar';
import {
  BillingSalesBulkConfirmModal,
  BillingSalesBulkProgressModal,
} from '@/features/billing/components/BillingSalesBulkModals';
import { BillingSalesFilterBar } from '@/features/billing/components/BillingSalesFilterBar';
import { LicenseSaleDetailDrawer } from '@/features/billing/components/LicenseSaleDetailDrawer';
import { LicenseValidityCell } from '@/features/billing/components/LicenseValidityCell';
import { billingQueryKeys } from '@/features/billing/constants/billingQueryKeys';
import { useBillingAccess } from '@/features/billing/hooks/useBillingAccess';
import { useLicenseSalesBulkActions } from '@/features/billing/hooks/useLicenseSalesBulkActions';
import { useBillingSalesList } from '@/features/billing/hooks/useBillingSalesList';
import {
  formatLicensePlanLabel,
  formatSaleStatusLabel,
} from '@/features/billing/utils/billingFormatters';
import {
  DEFAULT_BILLING_SALES_FILTERS,
  DEFAULT_LICENSE_SALES_SORT_BY,
  DEFAULT_LICENSE_SALES_SORT_DIR,
  type BillingSalesFilterState,
  type LicenseSalesSortField,
  getLicenseSalesAntSortOrder,
  isLicenseSalesSortField,
} from '@/features/billing/utils/billingSalesFilters';
import { downloadLicenseSaleInvoicePdf } from '@/features/billing/utils/downloadInvoicePdf';
import { listAdminTenants } from '@/features/super-admin/api/adminTenants';
import { useNotify } from '@/hooks/useNotify';
import { formatCurrency, useI18n } from '@/i18n';

export function BillingSalesTable({ showHeaderActions = true }: { showHeaderActions?: boolean }) {
  const { t, formatLocale } = useI18n();
  const notify = useNotify();
  const router = useRouter();
  const queryClient = useQueryClient();
  const canAccess = useBillingAccess();

  const [filters, setFilters] = useState<BillingSalesFilterState>(DEFAULT_BILLING_SALES_FILTERS);
  const [selectedSale, setSelectedSale] = useState<LicenseSaleResponse | null>(null);
  const [cancelTarget, setCancelTarget] = useState<LicenseSaleResponse | null>(null);
  const [pdfLoadingId, setPdfLoadingId] = useState<string | null>(null);
  const [selectedRowKeys, setSelectedRowKeys] = useState<React.Key[]>([]);
  const [selectedRows, setSelectedRows] = useState<LicenseSaleResponse[]>([]);
  const bulk = useLicenseSalesBulkActions();

  const tenantsQuery = useQuery({
    queryKey: ['admin-tenants', 'billing-filter'],
    queryFn: () => listAdminTenants(false),
    enabled: canAccess,
  });

  const salesQuery = useBillingSalesList(filters);

  const cancelMutation = billingApi.useCancel({
    mutation: {
      onSuccess: async () => {
        notify.successKey('billing.sales.cancelSuccess');
        setSelectedSale(null);
        await queryClient.invalidateQueries({ queryKey: billingQueryKeys.all });
      },
      onError: (err) => notify.apiError(err, { logContext: 'BillingSalesTable.cancel' }),
    },
  });

  const tenantOptions = useMemo(
    () =>
      (tenantsQuery.data ?? []).map((tenant) => ({
        value: tenant.id,
        label: `${tenant.name} (${tenant.slug})`,
      })),
    [tenantsQuery.data]
  );

  const statusColor = (status: string | null | undefined) => {
    switch (status) {
      case 'active':
        return 'green';
      case 'cancelled':
        return 'red';
      case 'refunded':
        return 'orange';
      default:
        return 'default';
    }
  };

  const handleDownloadPdf = async (sale: LicenseSaleResponse) => {
    if (!sale.id) return;
    setPdfLoadingId(sale.id);
    try {
      await downloadLicenseSaleInvoicePdf(
        sale.id,
        sale.invoiceNumber ? `${sale.invoiceNumber}.pdf` : undefined
      );
    } catch (err) {
      notify.apiError(err, { logContext: 'BillingSalesTable.downloadPdf' });
    } finally {
      setPdfLoadingId(null);
    }
  };

  const handleCancel = (sale: LicenseSaleResponse) => {
    if (!sale.id) return;
    setCancelTarget(sale);
  };

  const columns: ColumnsType<LicenseSaleResponse> = [
    {
      title: t('billing.sales.columns.invoice'),
      dataIndex: 'invoiceNumber',
      key: 'invoiceNumber',
      sorter: true,
      sortOrder: getLicenseSalesAntSortOrder('invoiceNumber', filters.sortBy, filters.sortDir),
      render: (value: string | null | undefined) => value ?? '—',
    },
    {
      title: t('billing.sales.columns.tenant'),
      key: 'tenant',
      sorter: true,
      sortOrder: getLicenseSalesAntSortOrder('tenant', filters.sortBy, filters.sortDir),
      render: (_, row) => (
        <Link href={`/admin/tenants/${row.tenantId}`}>
          {row.tenantName ?? row.tenantSlug ?? row.tenantId}
        </Link>
      ),
    },
    {
      title: t('billing.sales.columns.licenseKey'),
      dataIndex: 'licenseKey',
      key: 'licenseKey',
      sorter: true,
      sortOrder: getLicenseSalesAntSortOrder('licenseKey', filters.sortBy, filters.sortDir),
      ellipsis: true,
    },
    {
      title: t('billing.sales.columns.plan'),
      dataIndex: 'licensePlan',
      key: 'licensePlan',
      sorter: true,
      sortOrder: getLicenseSalesAntSortOrder('licensePlan', filters.sortBy, filters.sortDir),
      render: (plan: string | null | undefined) => formatLicensePlanLabel(plan, t),
    },
    {
      title: t('billing.sales.columns.priceGross'),
      dataIndex: 'priceGross',
      key: 'priceGross',
      sorter: true,
      sortOrder: getLicenseSalesAntSortOrder('priceGross', filters.sortBy, filters.sortDir),
      align: 'right',
      render: (value: number | undefined) =>
        value != null ? formatCurrency(value, formatLocale, { currency: 'EUR' }) : '—',
    },
    {
      title: t('billing.sales.columns.validUntil'),
      dataIndex: 'validUntilUtc',
      key: 'validUntilUtc',
      sorter: true,
      sortOrder: getLicenseSalesAntSortOrder('validUntilUtc', filters.sortBy, filters.sortDir),
      defaultSortOrder: 'ascend',
      render: (value: string | null | undefined) => (
        <LicenseValidityCell validUntilUtc={value} mode="date" />
      ),
    },
    {
      title: t('billing.sales.columns.daysRemaining'),
      key: 'daysRemaining',
      width: 160,
      sorter: true,
      sortOrder: getLicenseSalesAntSortOrder('daysRemaining', filters.sortBy, filters.sortDir),
      render: (_, row) => <LicenseValidityCell validUntilUtc={row.validUntilUtc} mode="days" />,
    },
    {
      title: t('billing.sales.columns.status'),
      dataIndex: 'status',
      key: 'status',
      render: (status: string | null | undefined) => (
        <Tag color={statusColor(status)}>{formatSaleStatusLabel(status, t)}</Tag>
      ),
    },
    {
      title: t('billing.sales.columns.soldAt'),
      dataIndex: 'soldAtUtc',
      key: 'soldAtUtc',
      sorter: true,
      sortOrder: getLicenseSalesAntSortOrder('soldAtUtc', filters.sortBy, filters.sortDir),
      render: dateColumnRender('datetime'),
    },
    {
      title: t('billing.sales.columns.actions'),
      key: 'actions',
      width: 120,
      render: (_, row) => (
        <Space>
          <Button type="link" size="small" onClick={() => setSelectedSale(row)}>
            {t('billing.sales.view')}
          </Button>
        </Space>
      ),
    },
  ];

  const onDateRangeChange = (range: [Dayjs | null, Dayjs | null] | null) => {
    setFilters((prev) => ({
      ...prev,
      page: 1,
      fromDate: range?.[0]?.startOf('day').toISOString(),
      toDate: range?.[1]?.endOf('day').toISOString(),
    }));
  };

  const clearFilters = () => {
    setFilters({ ...DEFAULT_BILLING_SALES_FILTERS });
  };

  const handleTableChange = (
    pagination: TablePaginationConfig,
    _tableFilters: Record<string, FilterValue | null>,
    sorter: SorterResult<LicenseSaleResponse> | SorterResult<LicenseSaleResponse>[]
  ) => {
    const active = Array.isArray(sorter) ? sorter[0] : sorter;
    const columnKey = String(active.columnKey ?? active.field ?? '');
    const nextSortBy: LicenseSalesSortField = isLicenseSalesSortField(columnKey)
      ? columnKey
      : DEFAULT_LICENSE_SALES_SORT_BY;
    const nextSortDir =
      active.order === 'ascend'
        ? 'asc'
        : active.order === 'descend'
          ? 'desc'
          : DEFAULT_LICENSE_SALES_SORT_DIR;

    setFilters((prev) => ({
      ...prev,
      page: pagination.current ?? prev.page,
      pageSize: pagination.pageSize ?? prev.pageSize,
      sortBy: active.order ? nextSortBy : DEFAULT_LICENSE_SALES_SORT_BY,
      sortDir: active.order ? nextSortDir : DEFAULT_LICENSE_SALES_SORT_DIR,
    }));
  };

  return (
    <>
      <Card variant="borderless">
        <div
          style={{
            marginBottom: 16,
            display: 'flex',
            flexWrap: 'wrap',
            gap: 12,
            justifyContent: 'space-between',
            alignItems: 'flex-start',
          }}
        >
          <BillingSalesFilterBar
            filters={filters}
            onChange={setFilters}
            onClear={clearFilters}
            tenantOptions={tenantOptions}
            tenantsLoading={tenantsQuery.isLoading}
            extra={<DatePicker.RangePicker key={`range-${filters.fromDate ?? ''}-${filters.toDate ?? ''}`} onChange={onDateRangeChange} />}
          />
          {showHeaderActions ? (
            <Button
              type="primary"
              icon={<PlusOutlined />}
              onClick={() => router.push('/admin/billing/sales/new')}
            >
              {t('billing.sales.newSale')}
            </Button>
          ) : null}
        </div>

        <BillingSalesBulkBar
          selectedCount={selectedRows.length}
          disabled={bulk.running}
          onAction={(action) => bulk.requestAction(action, selectedRows)}
        />

        <Table<LicenseSaleResponse>
          rowKey={(row) => row.id ?? row.licenseKey ?? row.invoiceNumber ?? 'row'}
          columns={columns}
          dataSource={salesQuery.data?.items ?? []}
          loading={salesQuery.isLoading || salesQuery.isFetching}
          onChange={handleTableChange}
          rowSelection={{
            selectedRowKeys,
            onChange: (keys, rows) => {
              setSelectedRowKeys(keys);
              setSelectedRows(rows);
            },
            preserveSelectedRowKeys: true,
          }}
          showSorterTooltip={{
            title: t('billing.licenseSales.sort.clickToSort'),
          }}
          pagination={{
            current: filters.page,
            pageSize: filters.pageSize,
            total: salesQuery.data?.totalCount ?? 0,
            showSizeChanger: true,
          }}
        />
      </Card>

      <BillingSalesBulkConfirmModal
        open={bulk.confirmOpen}
        action={bulk.pendingAction}
        selectedCount={selectedRows.length}
        eligibleCount={bulk.eligibleCountForPending}
        loading={bulk.running}
        onCancel={bulk.closeConfirm}
        onConfirm={async (reason) => {
          await bulk.confirmPending(reason);
          setSelectedRowKeys([]);
          setSelectedRows([]);
        }}
      />
      <BillingSalesBulkProgressModal open={bulk.progressOpen} progress={bulk.progress} />

      <LicenseSaleDetailDrawer
        open={selectedSale != null}
        saleId={selectedSale?.id ?? null}
        initialSale={selectedSale}
        onClose={() => setSelectedSale(null)}
        pdfLoading={selectedSale?.id != null && pdfLoadingId === selectedSale.id}
        onDownloadInvoice={(sale) => void handleDownloadPdf(sale)}
        onCancelSale={handleCancel}
      />

      {cancelTarget ? (
        <BillingSaleCancelModal
          sale={cancelTarget}
          loading={cancelMutation.isPending}
          onClose={() => setCancelTarget(null)}
          onConfirm={async (cancellationReason) => {
            await cancelMutation.mutateAsync({
              id: cancelTarget.id!,
              data: { cancellationReason },
            });
            setCancelTarget(null);
            setSelectedSale(null);
          }}
        />
      ) : null}
    </>
  );
}

type BillingSaleCancelModalProps = {
  sale: LicenseSaleResponse;
  loading: boolean;
  onClose: () => void;
  onConfirm: (cancellationReason: string) => Promise<void>;
};

function BillingSaleCancelModal({ loading, onClose, onConfirm }: BillingSaleCancelModalProps) {
  const { t } = useI18n();
  const [form] = Form.useForm<{ cancellationReason: string }>();

  const handleOk = async () => {
    const values = await form.validateFields();
    await onConfirm(values.cancellationReason);
  };

  return (
    <Modal
      open
      title={t('billing.sales.cancelConfirmTitle')}
      onCancel={onClose}
      onOk={handleOk}
      confirmLoading={loading}
      okText={t('billing.sales.cancelSale')}
      okButtonProps={{ danger: true }}
      cancelText={t('common.buttons.cancel')}
      destroyOnHidden
    >
      <Form form={form} layout="vertical" style={{ marginTop: 8 }}>
        <Form.Item
          name="cancellationReason"
          label={t('billing.sales.cancelReasonLabel')}
          rules={[
            { required: true, message: t('billing.sales.cancelReasonRequired') },
            { min: 10, message: t('billing.sales.cancelReasonRequired') },
          ]}
        >
          <Input.TextArea rows={3} />
        </Form.Item>
      </Form>
    </Modal>
  );
}
