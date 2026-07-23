'use client';

import { DownloadOutlined, ReloadOutlined } from '@ant-design/icons';
import { Alert, Button, Dropdown, Space, Tooltip, Typography } from 'antd';
import type { MenuProps } from 'antd';
import type { TablePaginationConfig } from 'antd/es/table';
import type { FilterValue, SorterResult } from 'antd/es/table/interface';
import React, { Suspense, useCallback, useMemo, useRef, useState } from 'react';

import { TableSkeleton } from '@/components/Skeleton';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { recordDownloadHistory } from '@/features/download-history/api/downloadHistoryApi';
import { BatchDownloadProgressModal } from '@/features/receipts/components/BatchDownloadProgressModal';
import ReceiptsFilterBar from '@/features/receipts/components/ReceiptsFilterBar';
import ReceiptsTable from '@/features/receipts/components/ReceiptsTable';
import { useReceiptListQuery } from '@/features/receipts/hooks/useReceiptListQuery';
import { useReceiptSearchParams } from '@/features/receipts/hooks/useReceiptSearchParams';
import type { ReceiptListItemDto, ReceiptListParams } from '@/features/receipts/types/receipts';
import {
  type BatchDownloadProgress,
  BATCH_RECEIPT_PDF_MAX,
  batchDownloadReceiptPdfs,
} from '@/features/receipts/utils/batchDownloadReceiptPdfs';
import { fetchAllFilteredReceipts } from '@/features/receipts/utils/fetchAllFilteredReceipts';
import { useNotify } from '@/hooks/useNotify';
import { useExportDownloadShortcutHandlers } from '@/hooks/useExportDownloadShortcutHandlers';
import { useKeyboardShortcutLabels } from '@/components/KeyboardShortcutsProvider';
import { useI18n } from '@/i18n';
import { formatBytes, formatNumber } from '@/i18n/formatting';
import { formatDate } from '@/lib/dateUtils';
import { ADMIN_NAV_LABEL_KEYS, adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

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
  const notify = useNotify();
  const { hasPermission } = usePermissions();
  const canBatchPdf =
    hasPermission(PERMISSIONS.REPORT_VIEW) || hasPermission(PERMISSIONS.RECEIPT_REPRINT);
  const { getShortcutLabel } = useKeyboardShortcutLabels();

  const { params, setParams, resetFilters } = useReceiptSearchParams();
  const { data, isLoading, isPlaceholderData, isError, error, refetch } =
    useReceiptListQuery(params);

  const [selectedRowKeys, setSelectedRowKeys] = useState<React.Key[]>([]);
  const [selectedRowsById, setSelectedRowsById] = useState<Record<string, ReceiptListItemDto>>({});
  const [batchProgress, setBatchProgress] = useState<BatchDownloadProgress | null>(null);
  const [batchOpen, setBatchOpen] = useState(false);
  const [selectingAll, setSelectingAll] = useState(false);
  const abortRef = useRef<AbortController | null>(null);

  /** Parse sort string "field:order" into Ant Design's sortField/sortOrder */
  const parsedSort = params.sort?.split(':') ?? [];
  const sortField = parsedSort[0];
  const sortOrder = parsedSort[1] === 'asc' ? ('ascend' as const) : ('descend' as const);

  const visibleRows = data?.items ?? [];
  const selectedRows = useMemo(
    () =>
      selectedRowKeys
        .map((key) => selectedRowsById[String(key)])
        .filter((row): row is ReceiptListItemDto => Boolean(row)),
    [selectedRowKeys, selectedRowsById]
  );

  const selectedEligible = useMemo(
    () => selectedRows.filter((r) => Boolean(r.paymentId?.trim())),
    [selectedRows]
  );

  const clearSelection = useCallback(() => {
    setSelectedRowKeys([]);
    setSelectedRowsById({});
  }, []);

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
    clearSelection();
    setParams(values);
  };

  const handleResetFilters = () => {
    clearSelection();
    resetFilters();
  };

  const selectAllVisible = () => {
    const nextKeys = visibleRows.map((r) => r.receiptId);
    const nextMap = { ...selectedRowsById };
    for (const row of visibleRows) {
      nextMap[row.receiptId] = row;
    }
    setSelectedRowKeys(nextKeys);
    setSelectedRowsById(nextMap);
  };

  const selectAllFiltered = async () => {
    setSelectingAll(true);
    try {
      const { items, truncated, totalCount } = await fetchAllFilteredReceipts({
        sort: params.sort,
        receiptNumber: params.receiptNumber,
        cashRegisterId: params.cashRegisterId,
        cashierId: params.cashierId,
        issuedFrom: params.issuedFrom,
        issuedTo: params.issuedTo,
      });
      const nextMap: Record<string, ReceiptListItemDto> = {};
      for (const row of items) {
        nextMap[row.receiptId] = row;
      }
      setSelectedRowsById(nextMap);
      setSelectedRowKeys(items.map((r) => r.receiptId));
      if (truncated) {
        notify.warning(
          t('receipts.batch.selectAllTruncated', {
            selected: items.length,
            total: totalCount,
            max: BATCH_RECEIPT_PDF_MAX,
          })
        );
      } else {
        notify.success(t('receipts.batch.selectAllSuccess', { count: items.length }));
      }
    } catch {
      notify.error(t('receipts.batch.selectAllFailed'));
    } finally {
      setSelectingAll(false);
    }
  };

  const runBatchDownload = async () => {
    if (selectedEligible.length === 0) {
      notify.warning(t('receipts.batch.noneEligible'));
      return;
    }

    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;
    setBatchOpen(true);
    setBatchProgress({
      phase: 'fetch',
      percent: 0,
      current: 0,
      total: selectedEligible.length,
      failedCount: 0,
    });

    try {
      const result = await batchDownloadReceiptPdfs(selectedEligible, {
        signal: controller.signal,
        onProgress: setBatchProgress,
      });

      try {
        await recordDownloadHistory({
          fileName: result.zipFileName,
          fileType: 'zip',
          fileSize: result.zipBlob.size,
          sourceKind: 'receipt-pdf-batch',
        });
      } catch {
        // best-effort
      }

      notify.success(
        t('receipts.batch.downloadSuccess', {
          ok: result.successCount,
          fail: result.failedCount,
          size: formatBytes(result.totalBytes, formatLocale),
        })
      );
      clearSelection();
    } catch (err) {
      const aborted =
        controller.signal.aborted ||
        (err instanceof DOMException && err.name === 'AbortError');
      if (aborted) {
        setBatchProgress({
          phase: 'error',
          percent: 0,
          current: 0,
          total: selectedEligible.length,
          failedCount: 0,
          message: t('receipts.batch.cancelled'),
        });
        return;
      }
      const code = err instanceof Error ? err.message : '';
      const message =
        code === 'BATCH_TOO_LARGE'
          ? t('receipts.batch.tooLarge', { max: BATCH_RECEIPT_PDF_MAX })
          : code === 'ALL_DOWNLOADS_FAILED'
            ? t('receipts.batch.allFailed')
            : t('receipts.batch.downloadFailed');
      setBatchProgress({
        phase: 'error',
        percent: 0,
        current: 0,
        total: selectedEligible.length,
        failedCount: selectedEligible.length,
        message,
      });
      notify.error(message);
    }
  };

  useExportDownloadShortcutHandlers(
    {
      onOpenBatchDownload: () => {
        if (!canBatchPdf || batchOpen) return;
        void runBatchDownload();
      },
      onDownloadExport: () => {
        if (!canBatchPdf || batchOpen) return;
        void runBatchDownload();
      },
    },
    canBatchPdf
  );

  const batchShortcutTitle = t('keyboardShortcuts.openBatchDownloadWithShortcut', {
    shortcut: getShortcutLabel('openBatchDownload'),
  });
  const downloadShortcutTitle = t('keyboardShortcuts.downloadExportWithShortcut', {
    shortcut: getShortcutLabel('downloadExport'),
  });

  const selectMenuItems: MenuProps['items'] = [
    {
      key: 'visible',
      label: t('receipts.batch.selectAllVisible'),
      onClick: selectAllVisible,
    },
    {
      key: 'filtered',
      label: t('receipts.batch.selectAllFiltered'),
      onClick: () => void selectAllFiltered(),
    },
    {
      key: 'clear',
      label: t('receipts.batch.clearSelection'),
      onClick: clearSelection,
    },
  ];

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
          range: `${formatDate(params.issuedFrom)}–${formatDate(params.issuedTo)}`,
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
        onReset={handleResetFilters}
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

      {canBatchPdf && !isError ? (
        <Space wrap style={{ width: '100%', justifyContent: 'space-between' }}>
          <Space wrap>
            <Dropdown menu={{ items: selectMenuItems }} trigger={['click']}>
              <Button loading={selectingAll}>{t('receipts.batch.selectMenu')}</Button>
            </Dropdown>
            <Tooltip title={batchShortcutTitle}>
              <Button
                type="primary"
                icon={<DownloadOutlined />}
                disabled={selectedEligible.length === 0 || batchOpen}
                onClick={() => void runBatchDownload()}
                title={downloadShortcutTitle}
              >
                {t('receipts.batch.downloadSelected')}
              </Button>
            </Tooltip>
          </Space>
          <Typography.Text type="secondary">
            {selectedRowKeys.length === 0
              ? t('receipts.batch.noneSelected')
              : t('receipts.batch.selectedSummary', {
                  count: selectedEligible.length,
                  total: selectedRowKeys.length,
                })}
          </Typography.Text>
        </Space>
      ) : null}

      {!isError ? (
        <ReceiptsTable
          data={visibleRows}
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
          rowSelection={
            canBatchPdf
              ? {
                  selectedRowKeys,
                  onChange: (keys, rows) => {
                    setSelectedRowKeys(keys);
                    setSelectedRowsById((prev) => {
                      const next = { ...prev };
                      for (const row of rows) {
                        if (row) next[row.receiptId] = row;
                      }
                      // Drop keys no longer selected
                      const keySet = new Set(keys.map(String));
                      for (const id of Object.keys(next)) {
                        if (!keySet.has(id)) delete next[id];
                      }
                      return next;
                    });
                  },
                  getCheckboxProps: (row) => ({
                    disabled: !row.paymentId?.trim(),
                  }),
                }
              : undefined
          }
        />
      ) : null}

      <BatchDownloadProgressModal
        open={batchOpen}
        progress={batchProgress}
        onCancel={() => {
          if (batchProgress?.phase === 'done' || batchProgress?.phase === 'error') {
            setBatchOpen(false);
            setBatchProgress(null);
            return;
          }
          abortRef.current?.abort();
        }}
      />
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
