'use client';

import { FilePdfOutlined, PlusOutlined, ReloadOutlined } from '@ant-design/icons';
import { useQuery } from '@tanstack/react-query';
import { Button, DatePicker, Space, Table, Tooltip } from 'antd';
import type { ColumnsType, TablePaginationConfig } from 'antd/es/table';
import type { FilterValue, SorterResult } from 'antd/es/table/interface';
import { type Dayjs } from 'dayjs';
import { usePathname, useRouter, useSearchParams } from 'next/navigation';
import React, { useEffect, useMemo, useState } from 'react';

import type { LicenseSaleResponse } from '@/api/generated/model';
import { dateColumnRender } from '@/components/DateColumn';
import { EmptyState } from '@/components/EmptyState';
import { StatusBadge, resolveStatusType } from '@/components/StatusBadge';
import { BillingAccessGate } from '@/features/billing/components/BillingAccessGate';
import { BillingSalesBulkBar } from '@/features/billing/components/BillingSalesBulkBar';
import {
  BillingSalesBulkConfirmModal,
  BillingSalesBulkProgressModal,
} from '@/features/billing/components/BillingSalesBulkModals';
import { BillingSalesFilterBar } from '@/features/billing/components/BillingSalesFilterBar';
import { LicenseValidityCell } from '@/features/billing/components/LicenseValidityCell';
import { useBillingAccess } from '@/features/billing/hooks/useBillingAccess';
import { useBillingSalesList, useLicenseSalesBulkActions } from '@/features/billing/hooks';
import {
  DEFAULT_BILLING_SALES_FILTERS,
  DEFAULT_LICENSE_SALES_SORT_BY,
  DEFAULT_LICENSE_SALES_SORT_DIR,
  type BillingSalesFilterState,
  type LicenseSalesSortField,
  billingSalesFiltersToSearchParams,
  getLicenseSalesAntSortOrder,
  isLicenseSalesSortField,
  parseBillingSalesFiltersFromSearchParams,
} from '@/features/billing/utils/billingSalesFilters';
import { downloadLicenseSaleInvoicePdf } from '@/features/billing/utils/downloadInvoicePdf';
import { listAdminTenants } from '@/features/super-admin/api/adminTenants';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { openApiErrorMessage } from '@/shared/errors/openApiErrorMessage';

const { RangePicker } = DatePicker;

export default function BillingSalesPage() {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const { message } = useAntdApp();
  const { t } = useI18n();
  const canAccess = useBillingAccess();
  const [pdfLoadingId, setPdfLoadingId] = useState<string | null>(null);
  const [selectedRowKeys, setSelectedRowKeys] = useState<React.Key[]>([]);
  const [selectedRows, setSelectedRows] = useState<LicenseSaleResponse[]>([]);
  const bulk = useLicenseSalesBulkActions();

  const [filters, setFilters] = useState<BillingSalesFilterState>(() => ({
    ...DEFAULT_BILLING_SALES_FILTERS,
    ...parseBillingSalesFiltersFromSearchParams(new URLSearchParams(searchParams.toString())),
  }));

  const tenantsQuery = useQuery({
    queryKey: ['admin-tenants', 'billing-filter'],
    queryFn: () => listAdminTenants(false),
    enabled: canAccess,
  });

  const tenantOptions = useMemo(
    () =>
      (tenantsQuery.data ?? []).map((tenant) => ({
        value: tenant.id,
        label: `${tenant.name} (${tenant.slug})`,
      })),
    [tenantsQuery.data]
  );

  const { data, isLoading, isFetching, refetch } = useBillingSalesList(filters);

  useEffect(() => {
    const next = billingSalesFiltersToSearchParams(filters).toString();
    const current = searchParams.toString();
    if (next === current) return;
    router.replace(next ? `${pathname}?${next}` : pathname, { scroll: false });
  }, [filters, pathname, router, searchParams]);

  const handleDateRange = (range: [Dayjs | null, Dayjs | null] | null) => {
    setFilters((prev) => ({
      ...prev,
      fromDate: range?.[0]?.startOf('day').toISOString(),
      toDate: range?.[1]?.endOf('day').toISOString(),
      page: 1,
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

  const handlePdfDownload = async (saleId: string, invoiceNumber?: string | null) => {
    setPdfLoadingId(saleId);
    try {
      await downloadLicenseSaleInvoicePdf(
        saleId,
        invoiceNumber ? `${invoiceNumber}.pdf` : undefined
      );
    } catch (err) {
      openApiErrorMessage(message.open, t, err, { logContext: 'BillingSalesPage.downloadPdf' });
    } finally {
      setPdfLoadingId(null);
    }
  };

  const columns: ColumnsType<LicenseSaleResponse> = [
    {
      title: 'Rechnungsnummer',
      dataIndex: 'invoiceNumber',
      key: 'invoiceNumber',
      sorter: true,
      sortOrder: getLicenseSalesAntSortOrder('invoiceNumber', filters.sortBy, filters.sortDir),
      render: (text: string | null | undefined, record) =>
        record.id ? (
          <Button
            type="link"
            style={{ padding: 0 }}
            onClick={() => router.push(`/admin/billing/sales/${record.id}`)}
          >
            {text ?? '—'}
          </Button>
        ) : (
          (text ?? '—')
        ),
    },
    {
      title: 'Mandant',
      dataIndex: 'tenantName',
      key: 'tenant',
      sorter: true,
      sortOrder: getLicenseSalesAntSortOrder('tenant', filters.sortBy, filters.sortDir),
      render: (text: string | null | undefined, record) =>
        record.tenantId ? (
          <Button
            type="link"
            style={{ padding: 0 }}
            onClick={() => router.push(`/admin/tenants/${record.tenantId}`)}
          >
            {text ?? record.tenantSlug ?? '—'}
          </Button>
        ) : (
          (text ?? '—')
        ),
    },
    { title: 'Slug', dataIndex: 'tenantSlug', key: 'tenantSlug' },
    {
      title: 'Lizenzplan',
      dataIndex: 'licensePlan',
      key: 'licensePlan',
      sorter: true,
      sortOrder: getLicenseSalesAntSortOrder('licensePlan', filters.sortBy, filters.sortDir),
    },
    {
      title: 'Lizenzschlüssel',
      dataIndex: 'licenseKey',
      key: 'licenseKey',
      sorter: true,
      sortOrder: getLicenseSalesAntSortOrder('licenseKey', filters.sortBy, filters.sortDir),
      render: (text: string | null | undefined) => {
        if (!text) return '—';
        const short = text.length > 15 ? `${text.substring(0, 15)}…` : text;
        return (
          <Tooltip title={text}>
            <code>{short}</code>
          </Tooltip>
        );
      },
    },
    {
      title: 'Gültig bis',
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
      title: t('billing.licenseSales.daysRemaining'),
      key: 'daysRemaining',
      width: 160,
      sorter: true,
      sortOrder: getLicenseSalesAntSortOrder('daysRemaining', filters.sortBy, filters.sortDir),
      render: (_, record) => (
        <LicenseValidityCell validUntilUtc={record.validUntilUtc} mode="days" />
      ),
    },
    {
      title: 'Betrag (Netto)',
      dataIndex: 'priceNet',
      key: 'priceNet',
      sorter: true,
      sortOrder: getLicenseSalesAntSortOrder('priceNet', filters.sortBy, filters.sortDir),
      align: 'right',
      render: (value: number | undefined) => (value != null ? `€ ${value.toFixed(2)}` : '—'),
    },
    {
      title: 'Status',
      dataIndex: 'status',
      key: 'status',
      render: (status: string | null | undefined) => {
        const key = (status ?? '').toLowerCase();
        const labelMap: Record<string, string> = {
          active: t('billing.sales.statusActive'),
          cancelled: t('billing.sales.statusCancelled'),
          refunded: t('billing.sales.statusRefunded'),
        };
        const resolved = resolveStatusType(key);
        if (resolved) {
          return <StatusBadge status={resolved} label={labelMap[key]} />;
        }
        if (key === 'refunded') {
          return <StatusBadge status="warning" label={labelMap.refunded} />;
        }
        return <StatusBadge status="info" label={status || '—'} />;
      },
    },
    {
      title: 'Verkauft am',
      dataIndex: 'soldAtUtc',
      key: 'soldAtUtc',
      sorter: true,
      sortOrder: getLicenseSalesAntSortOrder('soldAtUtc', filters.sortBy, filters.sortDir),
      render: dateColumnRender('datetime'),
    },
    {
      title: 'Aktionen',
      key: 'actions',
      width: 80,
      render: (_, record) =>
        record.id ? (
          <Space>
            <Button
              type="link"
              size="small"
              icon={<FilePdfOutlined />}
              loading={pdfLoadingId === record.id}
              onClick={() => void handlePdfDownload(record.id!, record.invoiceNumber)}
            />
          </Space>
        ) : null,
    },
  ];

  return (
    <BillingAccessGate>
      <div style={{ padding: 24 }}>
        <div
          style={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            marginBottom: 16,
            flexWrap: 'wrap',
            gap: 16,
          }}
        >
          <div>
            <h1 style={{ margin: 0 }}>Lizenzverkäufe</h1>
            <p style={{ color: '#64748b', marginBottom: 0 }}>
              Alle Lizenzverkäufe und -verlängerungen im Überblick.
            </p>
          </div>
          <Button
            type="primary"
            icon={<PlusOutlined />}
            onClick={() => router.push('/admin/billing/sales/new')}
          >
            Neuer Verkauf
          </Button>
        </div>

        <BillingSalesFilterBar
          filters={filters}
          onChange={setFilters}
          onClear={clearFilters}
          tenantOptions={tenantOptions}
          tenantsLoading={tenantsQuery.isLoading}
          extra={
            <>
              <RangePicker
                key={`range-${filters.fromDate ?? ''}-${filters.toDate ?? ''}`}
                onChange={handleDateRange}
              />
              <Button icon={<ReloadOutlined />} onClick={() => void refetch()}>
                Aktualisieren
              </Button>
            </>
          }
        />

        <BillingSalesBulkBar
          selectedCount={selectedRows.length}
          disabled={bulk.running}
          onAction={(action) => bulk.requestAction(action, selectedRows)}
        />

        <Table<LicenseSaleResponse>
          columns={columns}
          dataSource={data?.items ?? []}
          rowKey={(row) => row.id ?? row.licenseKey ?? row.invoiceNumber ?? 'row'}
          loading={isLoading || isFetching}
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
            total: data?.totalCount ?? 0,
            showSizeChanger: true,
          }}
          locale={{
            emptyText: (
              <EmptyState
                title={t('billing.sales.noResults')}
                actionText={t('billing.sales.newSale')}
                onAction={() => router.push('/admin/billing/sales/new')}
              />
            ),
          }}
        />

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
      </div>
    </BillingAccessGate>
  );
}
