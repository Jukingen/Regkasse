'use client';

import { ReloadOutlined } from '@ant-design/icons';
import { Alert, Button, Space, Tooltip, Typography } from 'antd';
import type { TablePaginationConfig } from 'antd/es/table';
import type { FilterValue, SorterResult } from 'antd/es/table/interface';
import dayjs from 'dayjs';
import React, { Suspense, useMemo } from 'react';

import { TableSkeleton } from '@/components/Skeleton';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import ReceiptsFilterBar from '@/features/receipts/components/ReceiptsFilterBar';
import ReceiptsTable from '@/features/receipts/components/ReceiptsTable';
import { useReceiptListQuery } from '@/features/receipts/hooks/useReceiptListQuery';
import { useReceiptSearchParams } from '@/features/receipts/hooks/useReceiptSearchParams';
import type { ReceiptListItemDto, ReceiptListParams } from '@/features/receipts/types/receipts';
import { useI18n } from '@/i18n';
import { formatNumber } from '@/i18n/formatting';
import { ADMIN_NAV_LABEL_KEYS, adminOverviewCrumb } from '@/shared/adminShellLabels';

/** Extract user-facing error message from API/network error */
function getReceiptListErrorMessage(error: unknown, fallback: string): string {
  if (error instanceof Error) return error.message;
  const norm = (error as { normalized?: { message?: string } })?.normalized;
  if (norm?.message) return norm.message;
  return fallback;
}

function ReceiptsLoadingFallback() {
  return <TableSkeleton rows={8} cols={5} />;
}

function ReceiptsPageContent() {
  const { t, formatLocale } = useI18n();
  const { params, setParams, resetFilters } = useReceiptSearchParams();
  const { data, isLoading, isPlaceholderData, isError, error, refetch } =
    useReceiptListQuery(params);

  /** Parse sort string "field:order" into Ant Design's sortField/sortOrder */
  const parsedSort = params.sort?.split(':') ?? [];
  const sortField = parsedSort[0];
  const sortOrder = parsedSort[1] === 'asc' ? ('ascend' as const) : ('descend' as const);

  /** Handle Ant Table onChange for pagination + sorting */
  const handleTableChange = (
    pagination: TablePaginationConfig,
    _filters: Record<string, FilterValue | null>,
    sorter: SorterResult<ReceiptListItemDto> | SorterResult<ReceiptListItemDto>[]
  ) => {
    const single = Array.isArray(sorter) ? sorter[0] : sorter;
    const updates: Partial<ReceiptListParams> = {
      page: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 25,
    };

    if (single?.field && single?.order) {
      const dir = single.order === 'ascend' ? 'asc' : 'desc';
      updates.sort = `${String(single.field)}:${dir}`;
    }

    setParams(updates);
  };

  /** Handle filter form submission */
  const handleFilterChange = (values: Partial<ReceiptListParams>) => {
    setParams(values);
  };

  const isEmpty = !isLoading && !isError && !data?.items?.length;
  const emptyText = isEmpty ? t('receipts.list.emptyFiltered') : undefined;

  const scopeSummary = useMemo(() => {
    const p = data?.page ?? params.page;
    const ps = data?.pageSize ?? params.pageSize;
    const tc = data?.totalCount;
    const parts = [
      t('receipts.list.scopePageLine', { page: p, pageSize: ps }),
      tc != null
        ? t('receipts.list.scopeTotalApi', {
            count: formatNumber(tc, formatLocale, { maximumFractionDigits: 0 }),
          })
        : t('receipts.list.scopeTotalNotLoaded'),
    ];
    if (params.receiptNumber?.trim()) {
      parts.push(t('receipts.list.scopeReceiptNr', { number: params.receiptNumber.trim() }));
    }
    if (params.cashRegisterId?.trim()) {
      parts.push(t('receipts.list.scopeRegister', { id: params.cashRegisterId.trim() }));
    }
    if (params.cashierId?.trim()) {
      parts.push(t('receipts.list.scopeCashier', { id: params.cashierId.trim() }));
    }
    if (params.issuedFrom && params.issuedTo) {
      parts.push(
        t('receipts.list.scopeDateRange', {
          range: `${dayjs(params.issuedFrom).format('DD.MM.YYYY')}–${dayjs(params.issuedTo).format('DD.MM.YYYY')}`,
        })
      );
    } else {
      parts.push(t('receipts.list.scopeNoDateFilter'));
    }
    if (params.sort) {
      parts.push(t('receipts.list.scopeSort', { sort: params.sort }));
    }
    return parts.join(' · ');
  }, [data?.page, data?.pageSize, data?.totalCount, params, formatLocale, t]);

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <AdminPageHeader
        title={t(ADMIN_NAV_LABEL_KEYS.receipts)}
        breadcrumbs={[adminOverviewCrumb(t), { title: t(ADMIN_NAV_LABEL_KEYS.receipts) }]}
        actions={
          <Tooltip title={t('receipts.list.refreshTooltip')}>
            <Button icon={<ReloadOutlined />} onClick={() => refetch()} loading={isLoading}>
              {t('receipts.list.refresh')}
            </Button>
          </Tooltip>
        }
      >
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t('receipts.list.intro')}
        </Typography.Paragraph>
      </AdminPageHeader>

      {isError ? (
        <Alert
          type="error"
          title={t('receipts.list.errorLoadTitle')}
          description={getReceiptListErrorMessage(error, t('receipts.list.loadErrorFallback'))}
          showIcon
          action={
            <Button size="small" onClick={() => refetch()}>
              {t('common.buttons.retry')}
            </Button>
          }
        />
      ) : null}

      <ReceiptsFilterBar
        initialValues={params}
        onFilterChange={handleFilterChange}
        onReset={resetFilters}
        loading={isLoading}
      />

      {!isError ? (
        <Typography.Paragraph
          type="secondary"
          style={{
            marginBottom: 0,
            marginTop: 8,
            fontSize: 12,
            padding: '8px 12px',
            background: 'var(--ant-color-fill-quaternary)',
            borderRadius: 6,
          }}
        >
          <Typography.Text strong style={{ fontSize: 12 }}>
            {t('receipts.list.scopeLabel')}{' '}
          </Typography.Text>
          {scopeSummary}
        </Typography.Paragraph>
      ) : null}

      {!isError ? (
        <ReceiptsTable
          data={data?.items ?? []}
          loading={isLoading}
          isPlaceholderData={isPlaceholderData}
          pagination={{
            current: data?.page ?? params.page,
            pageSize: data?.pageSize ?? params.pageSize,
            total: data?.totalCount ?? 0,
          }}
          sortField={sortField}
          sortOrder={sortOrder}
          onTableChange={handleTableChange}
          emptyText={emptyText}
          showPaymentPdfReprint
          showStoredPdfDownload
        />
      ) : null}
    </Space>
  );
}

/**
 * Receipts List Page
 * Wrapped in Suspense because useSearchParams() requires it in Next.js App Router.
 */
export default function ReceiptsPage() {
  return (
    <Suspense fallback={<ReceiptsLoadingFallback />}>
      <ReceiptsPageContent />
    </Suspense>
  );
}
