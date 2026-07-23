'use client';

import { EyeOutlined, PrinterOutlined } from '@ant-design/icons';
import { Button, Space, Tag, Typography } from 'antd';
import type { ColumnsType, TablePaginationConfig, TableRowSelection } from 'antd/es/table';
import type { FilterValue, SorterResult } from 'antd/es/table/interface';
import Link from 'next/link';
import React from 'react';

import { DateColumn } from '@/components/DateColumn';
import { VirtualTable } from '@/components/VirtualTable';
import { adminTablePaginationDefaults } from '@/components/ui/adminTablePagination';
import { ReprintButton } from '@/features/payments/components/ReprintButton';
import type { ReceiptListItemDto } from '@/features/receipts/types/receipts';
import { formatRksvSpecialReceiptKindDisplay } from '@/features/receipts/utils/formatRksvSpecialReceiptKind';
import { reportPdfTypeFromSpecialReceiptKind } from '@/features/reports/api/reportPdfApi';
import { StoredReportPdfButton } from '@/features/reports/components/StoredReportPdfButton';
import { useI18n } from '@/i18n';
import { formatNumber } from '@/i18n/formatting';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';
import { formatEUR } from '@/shared/utils/currency';
import { formatRegisterDisplayLabel } from '@/shared/utils/registerIdentity';

interface ReceiptsTableProps {
  data: ReceiptListItemDto[];
  loading: boolean;
  isPlaceholderData: boolean;
  pagination: {
    current: number;
    pageSize: number;
    total: number;
  };
  sortField?: string;
  sortOrder?: 'ascend' | 'descend';
  /** Optional text when table has no data (e.g. "No receipts found. Try adjusting filters.") */
  emptyText?: string;
  onTableChange: (
    pagination: TablePaginationConfig,
    filters: Record<string, FilterValue | null>,
    sorter: SorterResult<ReceiptListItemDto> | SorterResult<ReceiptListItemDto>[]
  ) => void;
  /** Zeigt eine zweite Aktion «Nachdruck» (z. B. Operations Center mit receipt.reprint). */
  reprintEnabled?: boolean;
  reprintActionLabel?: string;
  onStartReprint?: (row: ReceiptListItemDto) => void;
  /** Admin PDF reprint column (shown only when caller opts in and JWT has receipt.reprint). */
  showPaymentPdfReprint?: boolean;
  /** Stored original PDF download (receipt.reprint). */
  showStoredPdfDownload?: boolean;
  rowSelection?: TableRowSelection<ReceiptListItemDto>;
}

function buildColumns(
  t: (key: string, options?: Record<string, string | number>) => string,
  reprintEnabled: boolean | undefined,
  reprintActionLabel: string | undefined,
  onStartReprint: ((row: ReceiptListItemDto) => void) | undefined,
  showPaymentPdfReprint: boolean | undefined,
  showStoredPdfDownload: boolean | undefined
): ColumnsType<ReceiptListItemDto> {
  const base: ColumnsType<ReceiptListItemDto> = [
    {
      title: t('receipts.table.colReceipt'),
      dataIndex: 'issuedAt',
      key: 'receipt',
      sorter: true,
      width: 210,
      render: (_: string, row: ReceiptListItemDto) => (
        <Space orientation="vertical" size={0}>
          <Typography.Text strong ellipsis style={{ maxWidth: 200, display: 'block' }}>
            {row.receiptNumber}
          </Typography.Text>
          <Typography.Text
            type="secondary"
            style={{ fontSize: 12, fontVariantNumeric: 'tabular-nums' }}
          >
            <DateColumn date={row.issuedAt} format="datetime" />
          </Typography.Text>
        </Space>
      ),
    },
    {
      title: t('receipts.table.colRegisterFk'),
      dataIndex: 'cashRegisterId',
      key: 'cashRegisterFk',
      width: 160,
      ellipsis: true,
      render: (_: string, row) =>
        row.cashRegisterId ? (
          <Typography.Text
            type="secondary"
            style={{ fontFamily: 'monospace', fontSize: 11 }}
            ellipsis
          >
            {row.cashRegisterId}
          </Typography.Text>
        ) : (
          <Typography.Text type="secondary">—</Typography.Text>
        ),
    },
    {
      title: t('receipts.table.colRegisterDisplay'),
      dataIndex: 'registerDisplayNumber',
      key: 'registerDisplayNumber',
      width: 120,
      render: (_: string | undefined, row) => {
        const disp = formatRegisterDisplayLabel(row.registerDisplayNumber);
        return disp === '—' ? (
          <Typography.Text type="secondary">—</Typography.Text>
        ) : (
          <Tag>{disp}</Tag>
        );
      },
    },
    {
      title: t('receipts.table.colCashier'),
      dataIndex: 'cashierId',
      key: 'cashierId',
      width: 120,
      ellipsis: true,
      render: (text: string | null) => (
        <Typography.Text type="secondary" ellipsis>
          {text || '—'}
        </Typography.Text>
      ),
    },
    {
      title: t('receipts.table.colSpecialKind'),
      dataIndex: 'rksvSpecialReceiptKind',
      key: 'rksvSpecialReceiptKind',
      width: 130,
      render: (kind: string | null | undefined) =>
        kind ? (
          <Tag color="blue">{formatRksvSpecialReceiptKindDisplay(t, kind)}</Tag>
        ) : (
          <Typography.Text type="secondary">—</Typography.Text>
        ),
    },
    {
      title: t('receipts.table.colGrandTotal'),
      dataIndex: 'grandTotal',
      key: 'grandTotal',
      align: 'right',
      width: 120,
      sorter: true,
      render: (val: number) => (
        <Typography.Text strong style={{ fontVariantNumeric: 'tabular-nums' }}>
          {formatEUR(val)}
        </Typography.Text>
      ),
    },
  ];

  if (reprintEnabled && onStartReprint && reprintActionLabel) {
    base.push({
      title: reprintActionLabel,
      key: 'reprint',
      width: 130,
      fixed: 'right',
      align: 'center',
      render: (_: unknown, record: ReceiptListItemDto) => (
        <Button
          size="small"
          icon={<PrinterOutlined />}
          disabled={!record.paymentId?.trim()}
          onClick={() => onStartReprint(record)}
        >
          {reprintActionLabel}
        </Button>
      ),
    });
  }

  if (showStoredPdfDownload) {
    base.push({
      title: t('receipts.table.colStoredPdf'),
      key: 'storedPdf',
      width: 110,
      fixed: 'right',
      align: 'center',
      render: (_: unknown, record: ReceiptListItemDto) => (
        <StoredReportPdfButton
          reportType={reportPdfTypeFromSpecialReceiptKind(record.rksvSpecialReceiptKind)}
          targetId={record.paymentId}
          fileNameBase={record.receiptNumber}
          size="small"
        />
      ),
    });
  }

  if (showPaymentPdfReprint) {
    base.push({
      title: t('receipts.table.colPdfReprint'),
      key: 'pdfReprint',
      width: 150,
      fixed: 'right',
      align: 'center',
      render: (_: unknown, record: ReceiptListItemDto) => (
        <ReprintButton
          paymentId={record.paymentId}
          receiptNumber={record.receiptNumber}
          size="small"
        />
      ),
    });
  }

  base.push({
    title: t('receipts.table.colNext'),
    key: 'actions',
    width: 108,
    fixed: 'right',
    align: 'right' as const,
    render: (_: unknown, record: ReceiptListItemDto) => (
      <Link href={`/receipts/${record.receiptId}`}>
        <Button type="primary" size="small" icon={<EyeOutlined />}>
          {t('receipts.table.open')}
        </Button>
      </Link>
    ),
  });

  return base;
}

/**
 * Paginated, sortable receipts table.
 * Purely presentational — all data and pagination state comes from props.
 */
export default function ReceiptsTable({
  data,
  loading,
  isPlaceholderData,
  pagination,
  sortField,
  sortOrder,
  emptyText,
  onTableChange,
  reprintEnabled,
  reprintActionLabel,
  onStartReprint,
  showPaymentPdfReprint,
  showStoredPdfDownload,
  rowSelection,
}: ReceiptsTableProps) {
  const { t, formatLocale } = useI18n();
  const { hasPermission } = usePermissions();
  const showPdfReprintColumn =
    Boolean(showPaymentPdfReprint) && hasPermission(PERMISSIONS.RECEIPT_REPRINT);
  const showStoredPdfColumn =
    Boolean(showStoredPdfDownload) &&
    (hasPermission(PERMISSIONS.REPORT_VIEW) || hasPermission(PERMISSIONS.RECEIPT_REPRINT));

  const columns = React.useMemo(
    () =>
      buildColumns(
        t,
        reprintEnabled,
        reprintActionLabel,
        onStartReprint,
        showPdfReprintColumn,
        showStoredPdfColumn
      ),
    [
      t,
      reprintEnabled,
      reprintActionLabel,
      onStartReprint,
      showPdfReprintColumn,
      showStoredPdfColumn,
    ]
  );

  // Apply current sort indicator to the matching column
  const columnsWithSort = columns.map((col) => {
    if ('dataIndex' in col && col.dataIndex === sortField) {
      return { ...col, sortOrder: sortOrder };
    }
    return col;
  });

  const scrollX =
    1230 +
    (reprintEnabled ? 180 : 0) +
    (showPdfReprintColumn ? 170 : 0) +
    (showStoredPdfColumn ? 120 : 0);

  return (
    <VirtualTable<ReceiptListItemDto>
      columns={columnsWithSort}
      dataSource={data}
      rowKey="receiptId"
      loading={loading && !isPlaceholderData}
      onChange={onTableChange}
      rowSelection={rowSelection}
      pagination={{
        ...adminTablePaginationDefaults,
        current: pagination.current,
        pageSize: pagination.pageSize,
        total: pagination.total,
        pageSizeOptions: [10, 25, 50, 100],
        showTotal: (total, range) =>
          t('receipts.table.paginationTotal', {
            from: formatNumber(range[0] ?? 0, formatLocale, { maximumFractionDigits: 0 }),
            to: formatNumber(range[1] ?? 0, formatLocale, { maximumFractionDigits: 0 }),
            total: formatNumber(total, formatLocale, { maximumFractionDigits: 0 }),
          }),
      }}
      scroll={{ x: scrollX }}
      locale={emptyText ? { emptyText } : undefined}
      style={{
        opacity: isPlaceholderData ? 0.6 : 1,
        transition: 'opacity 0.2s',
      }}
      size="middle"
    />
  );
}
