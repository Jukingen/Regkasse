'use client';

import { EyeOutlined, SearchOutlined } from '@ant-design/icons';
import {
  Avatar,
  Button,
  Collapse,
  Input,
  Select,
  Space,
  Table,
  Tag,
  Tooltip,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import React, { useCallback, useMemo, useState } from 'react';

import type { AdminShiftRow } from '@/features/shifts/api/shiftsOverview';
import { ShiftDetailModal } from '@/features/shifts/components/ShiftDetailModal';
import {
  cashierInitial,
  differenceTextColor,
  filterShiftHistory,
  groupShiftHistoryByRegister,
  shiftStatusRowBackground,
  shiftStatusTagColor,
  shortUserId,
  summarizeShiftHistory,
  type ShiftHistoryStatusFilter,
} from '@/features/shifts/utils/shiftHistoryDisplay';
import { FORMAT_EMPTY_DISPLAY, formatCurrency, formatDateTime, useI18n } from '@/i18n';

export type ShiftHistoryTableProps = {
  rows: AdminShiftRow[];
  loading?: boolean;
  /** Server-side register filter already applied; still allow client register filter when unset. */
  serverRegisterId?: string;
};

export const ShiftHistoryTable: React.FC<ShiftHistoryTableProps> = ({
  rows,
  loading = false,
  serverRegisterId,
}) => {
  const { t, formatLocale } = useI18n();
  const ts = useCallback((path: string) => t(`shifts:${path}`), [t]);

  const [cashierId, setCashierId] = useState<string | undefined>();
  const [clientRegisterId, setClientRegisterId] = useState<string | undefined>();
  const [status, setStatus] = useState<ShiftHistoryStatusFilter>('all');
  const [search, setSearch] = useState('');
  const [detailShift, setDetailShift] = useState<AdminShiftRow | null>(null);

  const formatDt = useCallback(
    (value?: string | null) =>
      value
        ? formatDateTime(value, formatLocale, { dateStyle: 'short', timeStyle: 'short' })
        : FORMAT_EMPTY_DISPLAY,
    [formatLocale]
  );

  const formatMoney = useCallback(
    (value?: number | null) => formatCurrency(value ?? 0, formatLocale),
    [formatLocale]
  );

  const cashierOptions = useMemo(() => {
    const map = new Map<string, string>();
    for (const row of rows) {
      if (!map.has(row.cashierId)) {
        map.set(row.cashierId, row.cashierName);
      }
    }
    return Array.from(map.entries())
      .map(([id, name]) => ({
        value: id,
        label: `${name} (#${shortUserId(id)})`,
      }))
      .sort((a, b) => a.label.localeCompare(b.label));
  }, [rows]);

  const registerOptions = useMemo(() => {
    const map = new Map<string, string>();
    for (const row of rows) {
      if (!map.has(row.cashRegisterId)) {
        map.set(row.cashRegisterId, row.registerNumber?.trim() || row.cashRegisterId);
      }
    }
    return Array.from(map.entries())
      .map(([id, label]) => ({ value: id, label }))
      .sort((a, b) => a.label.localeCompare(b.label));
  }, [rows]);

  const filteredRows = useMemo(() => {
    const filtered = filterShiftHistory(rows, {
      cashierId,
      cashRegisterId: serverRegisterId ? undefined : clientRegisterId,
      status,
      search,
    });
    return [...filtered].sort(
      (a, b) => new Date(b.startedAt).getTime() - new Date(a.startedAt).getTime()
    );
  }, [cashierId, clientRegisterId, rows, search, serverRegisterId, status]);

  const summary = useMemo(() => summarizeShiftHistory(filteredRows), [filteredRows]);
  const groups = useMemo(() => groupShiftHistoryByRegister(filteredRows), [filteredRows]);

  const renderCashier = useCallback(
    (row: AdminShiftRow) => (
      <Tooltip
        title={
          <div>
            <div>
              {ts('userId')}: {row.cashierId}
            </div>
            <div>
              {ts('tooltips.cashierName')}: {row.cashierName}
            </div>
          </div>
        }
      >
        <Space size={8}>
          <Avatar size="small">{cashierInitial(row.cashierName)}</Avatar>
          <span>
            {row.cashierName}{' '}
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              (#{shortUserId(row.cashierId)})
            </Typography.Text>
          </span>
        </Space>
      </Tooltip>
    ),
    [ts]
  );

  const renderRegister = useCallback(
    (row: AdminShiftRow) => {
      const label = row.registerNumber?.trim() || row.cashRegisterId;
      return (
        <Tooltip
          title={
            <div>
              <div>
                {ts('tooltips.registerNumber')}: {label}
              </div>
              <div>
                {ts('tooltips.registerId')}: {row.cashRegisterId}
              </div>
            </div>
          }
        >
          <span>{label}</span>
        </Tooltip>
      );
    },
    [ts]
  );

  const renderMoneyBreakdown = useCallback(
    (amount: number, row: AdminShiftRow, label: string) => (
      <Tooltip
        title={
          <div>
            <div>
              {ts('columns.sales')}: {formatMoney(row.totalSales)}
            </div>
            <div>
              {ts('columns.cash')}: {formatMoney(row.totalCash)}
            </div>
            <div>
              {ts('columns.card')}: {formatMoney(row.totalCard)}
            </div>
          </div>
        }
      >
        <span aria-label={label}>{formatMoney(amount)}</span>
      </Tooltip>
    ),
    [formatMoney, ts]
  );

  const renderDifference = useCallback(
    (value: number) => (
      <Typography.Text style={{ color: differenceTextColor(value) }}>
        {formatMoney(value)}
      </Typography.Text>
    ),
    [formatMoney]
  );

  const columns: ColumnsType<AdminShiftRow> = useMemo(
    () => [
      {
        title: ts('columns.cashier'),
        key: 'cashier',
        ellipsis: true,
        sorter: (a, b) => a.cashierName.localeCompare(b.cashierName),
        render: (_v, row) => renderCashier(row),
      },
      {
        title: ts('columns.register'),
        key: 'register',
        width: 120,
        render: (_v, row) => renderRegister(row),
      },
      {
        title: ts('columns.startedAt'),
        dataIndex: 'startedAt',
        key: 'startedAt',
        width: 160,
        defaultSortOrder: 'descend',
        sorter: (a, b) => new Date(a.startedAt).getTime() - new Date(b.startedAt).getTime(),
        render: formatDt,
      },
      {
        title: ts('columns.endedAt'),
        dataIndex: 'endedAt',
        key: 'endedAt',
        width: 160,
        sorter: (a, b) =>
          new Date(a.endedAt ?? 0).getTime() - new Date(b.endedAt ?? 0).getTime(),
        render: formatDt,
      },
      {
        title: ts('columns.startBalance'),
        dataIndex: 'startBalance',
        key: 'startBalance',
        align: 'right',
        render: formatMoney,
      },
      {
        title: ts('columns.endBalance'),
        dataIndex: 'endBalance',
        key: 'endBalance',
        align: 'right',
        render: formatMoney,
      },
      {
        title: ts('columns.sales'),
        dataIndex: 'totalSales',
        key: 'totalSales',
        align: 'right',
        sorter: (a, b) => a.totalSales - b.totalSales,
        render: (v: number, row) => renderMoneyBreakdown(v, row, ts('columns.sales')),
      },
      {
        title: ts('columns.cash'),
        dataIndex: 'totalCash',
        key: 'totalCash',
        align: 'right',
        render: (v: number, row) => renderMoneyBreakdown(v, row, ts('columns.cash')),
      },
      {
        title: ts('columns.card'),
        dataIndex: 'totalCard',
        key: 'totalCard',
        align: 'right',
        render: (v: number, row) => renderMoneyBreakdown(v, row, ts('columns.card')),
      },
      {
        title: ts('columns.cashCount'),
        dataIndex: 'cashCount',
        key: 'cashCount',
        align: 'right',
        render: (v: number | null | undefined) =>
          v == null ? FORMAT_EMPTY_DISPLAY : formatMoney(v),
      },
      {
        title: ts('columns.difference'),
        dataIndex: 'difference',
        key: 'difference',
        align: 'right',
        sorter: (a, b) => a.difference - b.difference,
        render: (v: number) => renderDifference(v),
      },
      {
        title: ts('columns.status'),
        dataIndex: 'status',
        key: 'status',
        width: 150,
        filters: [
          { text: ts('status.Completed'), value: 'Completed' },
          { text: ts('status.Discrepancy'), value: 'Discrepancy' },
          { text: ts('status.Active'), value: 'Active' },
        ],
        onFilter: (value, record) => record.status === value,
        render: (statusValue: string) => (
          <Tag color={shiftStatusTagColor(statusValue)}>
            {ts(`status.${statusValue}`) || statusValue}
          </Tag>
        ),
      },
      {
        title: ts('columns.actions'),
        key: 'actions',
        width: 110,
        fixed: 'right',
        render: (_v, row) => (
          <Button
            size="small"
            icon={<EyeOutlined />}
            onClick={() => setDetailShift(row)}
          >
            {ts('actions.details')}
          </Button>
        ),
      },
    ],
    [formatDt, formatMoney, renderCashier, renderDifference, renderMoneyBreakdown, renderRegister, ts]
  );

  const statusOptions: { value: ShiftHistoryStatusFilter; label: string }[] = [
    { value: 'all', label: ts('filters.allStatuses') },
    { value: 'Completed', label: ts('status.Completed') },
    { value: 'Discrepancy', label: ts('status.Discrepancy') },
    { value: 'Active', label: ts('status.Active') },
  ];

  return (
    <>
      <Space wrap style={{ marginBottom: 12 }}>
        <span>{ts('filters.cashier')}</span>
        <Select
          allowClear
          showSearch
          optionFilterProp="label"
          placeholder={ts('filters.allCashiers')}
          style={{ minWidth: 220 }}
          value={cashierId}
          onChange={(v) => setCashierId(v ?? undefined)}
          options={cashierOptions}
        />
        {!serverRegisterId ? (
          <>
            <span>{ts('filters.register')}</span>
            <Select
              allowClear
              showSearch
              optionFilterProp="label"
              placeholder={ts('filters.allRegisters')}
              style={{ minWidth: 180 }}
              value={clientRegisterId}
              onChange={(v) => setClientRegisterId(v ?? undefined)}
              options={registerOptions}
            />
          </>
        ) : null}
        <span>{ts('filters.status')}</span>
        <Select
          style={{ minWidth: 160 }}
          value={status}
          onChange={(v: ShiftHistoryStatusFilter) => setStatus(v)}
          options={statusOptions}
        />
        <Input
          allowClear
          prefix={<SearchOutlined />}
          placeholder={ts('filters.searchPlaceholder')}
          style={{ minWidth: 220 }}
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
      </Space>

      {groups.length === 0 ? (
        <Table<AdminShiftRow>
          rowKey="id"
          size="small"
          loading={loading}
          dataSource={[]}
          columns={columns}
          pagination={false}
          locale={{ emptyText: ts('empty') }}
        />
      ) : (
        <Collapse
          defaultActiveKey={groups.map((g) => g.cashRegisterId)}
          items={groups.map((group) => ({
            key: group.cashRegisterId,
            label: t('shifts:grouping.registerHeader', {
              register: group.registerLabel,
              count: group.shifts.length,
            }),
            children: (
              <Table<AdminShiftRow>
                rowKey="id"
                size="small"
                loading={loading}
                dataSource={group.shifts}
                columns={columns}
                pagination={{ pageSize: 20, showSizeChanger: true }}
                scroll={{ x: 1400 }}
                locale={{ emptyText: ts('empty') }}
                onRow={(record) => ({
                  style: {
                    background: shiftStatusRowBackground(record.status),
                  },
                })}
              />
            ),
          }))}
        />
      )}

      {filteredRows.length > 0 ? (
        <Space wrap style={{ marginTop: 12 }}>
          <Typography.Text strong>{ts('summary.filteredTotal')}:</Typography.Text>
          <Typography.Text>
            {ts('summary.shiftCount')}: {summary.count}
          </Typography.Text>
          <Typography.Text>
            {ts('summary.totalSales')}: {formatMoney(summary.totalSales)}
          </Typography.Text>
          <Typography.Text>
            {ts('summary.totalCash')}: {formatMoney(summary.totalCash)}
          </Typography.Text>
          <Typography.Text>
            {ts('summary.totalCard')}: {formatMoney(summary.totalCard)}
          </Typography.Text>
          <Typography.Text style={{ color: differenceTextColor(summary.totalDifference) }}>
            {ts('summary.totalDifference')}: {formatMoney(summary.totalDifference)}
          </Typography.Text>
        </Space>
      ) : null}

      <ShiftDetailModal
        open={detailShift != null}
        shift={detailShift}
        onClose={() => setDetailShift(null)}
      />
    </>
  );
};
