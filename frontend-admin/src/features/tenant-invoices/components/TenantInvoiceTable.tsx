'use client';

import { DownloadOutlined } from '@ant-design/icons';
import { Button, DatePicker, Empty, Select, Space, Table, Tag } from 'antd';
import type { ColumnsType, TablePaginationConfig } from 'antd/es/table';
import type { Dayjs } from 'dayjs';
import { useCallback, useMemo, useState } from 'react';

import { TableSkeleton } from '@/components/Skeleton';
import {
  downloadTenantInvoicePdf,
  fetchTenantInvoices,
  tenantInvoiceQueryKeys,
  type TenantInvoiceDto,
} from '@/features/tenant-invoices/api/tenantInvoices';
import {
  getTenantInvoiceFileName,
  getTenantInvoiceStatusColor,
  getTenantInvoiceStatusLabelKey,
} from '@/features/tenant-invoices/utils/tenantInvoiceDisplay';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useAuthorizedQuery } from '@/hooks/useAuthorizedQuery';
import { useI18n } from '@/i18n';
import { formatCurrency } from '@/i18n/formatting';
import { formatGermanDate } from '@/lib/dateFormatter';
import { triggerBlobDownload } from '@/lib/download/exportDownload';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { openApiErrorMessage } from '@/shared/errors/openApiErrorMessage';

const { RangePicker } = DatePicker;

const DEFAULT_PAGE_SIZE = 20;

export function TenantInvoiceTable() {
  const { t, formatLocale } = useI18n();
  const { message } = useAntdApp();
  const [range, setRange] = useState<[Dayjs | null, Dayjs | null] | null>(null);
  const [status, setStatus] = useState<string | undefined>();
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);
  const [downloadingId, setDownloadingId] = useState<string | null>(null);

  const fromDate = range?.[0]?.startOf('day').toISOString();
  const toDate = range?.[1]?.endOf('day').toISOString();

  const listParams = useMemo(
    () => ({
      page,
      pageSize,
      status,
      fromDate,
      toDate,
    }),
    [fromDate, page, pageSize, status, toDate]
  );

  const query = useAuthorizedQuery({
    queryKey: tenantInvoiceQueryKeys.list(listParams),
    queryFn: ({ signal }) => fetchTenantInvoices(listParams, signal),
    requiredPermission: [PERMISSIONS.LICENSE_MANAGE],
  });

  const handleDownload = useCallback(
    async (row: TenantInvoiceDto) => {
      setDownloadingId(row.id);
      try {
        const blob = await downloadTenantInvoicePdf(row.id);
        triggerBlobDownload(blob, getTenantInvoiceFileName(row.invoiceNumber));
      } catch (error) {
        openApiErrorMessage(message.open, t, error, {
          fallbackKey: 'tenantPortal.invoices.downloadError',
          logContext: 'TenantInvoices.download',
        });
      } finally {
        setDownloadingId(null);
      }
    },
    [message.open, t]
  );

  const statusOptions = useMemo(
    () => [
      { value: 'paid', label: t('tenantPortal.invoices.statuses.paid') },
      { value: 'unpaid', label: t('tenantPortal.invoices.statuses.unpaid') },
      { value: 'overdue', label: t('tenantPortal.invoices.statuses.overdue') },
      { value: 'cancelled', label: t('tenantPortal.invoices.statuses.cancelled') },
      { value: 'refunded', label: t('tenantPortal.invoices.statuses.refunded') },
    ],
    [t]
  );

  const columns: ColumnsType<TenantInvoiceDto> = useMemo(
    () => [
      {
        title: t('tenantPortal.invoices.invoiceNumber'),
        dataIndex: 'invoiceNumber',
        key: 'invoiceNumber',
      },
      {
        title: t('tenantPortal.invoices.date'),
        dataIndex: 'issuedAt',
        key: 'issuedAt',
        render: (value: string, row) => formatGermanDate(value || row.invoiceDateUtc),
      },
      {
        title: t('tenantPortal.invoices.amount'),
        dataIndex: 'amountGross',
        key: 'amountGross',
        align: 'right',
        render: (value: number, row) =>
          formatCurrency(value, formatLocale, { currency: row.currency || 'EUR' }),
      },
      {
        title: t('tenantPortal.invoices.status'),
        dataIndex: 'status',
        key: 'status',
        render: (rowStatus: string) => (
          <Tag color={getTenantInvoiceStatusColor(rowStatus)}>
            {t(getTenantInvoiceStatusLabelKey(rowStatus))}
          </Tag>
        ),
      },
      {
        title: t('tenantPortal.invoices.actions'),
        key: 'actions',
        render: (_, row) => (
          <Button
            type="link"
            icon={<DownloadOutlined />}
            loading={downloadingId === row.id}
            onClick={() => void handleDownload(row)}
          >
            {t('tenantPortal.invoices.download')}
          </Button>
        ),
      },
    ],
    [downloadingId, formatLocale, handleDownload, t]
  );

  const pagination: TablePaginationConfig = {
    current: page,
    pageSize,
    total: query.data?.totalCount ?? 0,
    showSizeChanger: true,
    pageSizeOptions: [10, 20, 50],
    onChange: (nextPage, nextSize) => {
      setPage(nextPage);
      setPageSize(nextSize);
    },
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <Space wrap>
        <Select
          allowClear
          placeholder={t('tenantPortal.invoices.filterStatus')}
          aria-label={t('tenantPortal.invoices.filterStatus')}
          value={status}
          options={statusOptions}
          onChange={(next) => {
            setStatus(next ?? undefined);
            setPage(1);
          }}
          style={{ width: 180 }}
        />
        <RangePicker
          value={range}
          onChange={(next) => {
            setRange(next);
            setPage(1);
          }}
          allowClear
          aria-label={t('tenantPortal.invoices.filterDate')}
        />
      </Space>

      {query.isLoading ? <TableSkeleton rows={6} cols={5} /> : null}

      {!query.isLoading ? (
        <Table<TenantInvoiceDto>
          rowKey="id"
          columns={columns}
          dataSource={query.data?.items ?? []}
          pagination={pagination}
          locale={{
            emptyText: (
              <Empty
                image={Empty.PRESENTED_IMAGE_SIMPLE}
                description={t('tenantPortal.invoices.noInvoices')}
              />
            ),
          }}
        />
      ) : null}
    </div>
  );
}
