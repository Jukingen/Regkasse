'use client';

import { Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType, TablePaginationConfig } from 'antd/es/table';
import React from 'react';

import { dateColumnRender } from '@/components/DateColumn';
import type { MonatsbelegOpsRow } from '@/features/rksv/utils/monatsbelegOpsPresentation';
import { formatJahresbelegFonDeadline } from '@/features/rksv/utils/monatsbelegOpsPresentation';
import { useI18n } from '@/i18n/I18nProvider';

const DEP_STATUSES = new Set(['InDep', 'Missing']);
const FON_STATUSES = new Set([
  'NotRequired',
  'Pending',
  'Submitted',
  'Verified',
  'Failed',
  'ManualVerificationRequired',
]);

function depColor(status: string): string {
  return status === 'InDep' ? 'green' : 'red';
}

function fonColor(status: string): string {
  const s = status.toLowerCase();
  if (s === 'verified') return 'green';
  if (s === 'notrequired') return 'default';
  if (s === 'submitted' || s === 'pending') return 'orange';
  return 'red';
}

function displayStatusColor(status: MonatsbelegOpsRow['displayStatus']): string {
  switch (status) {
    case 'Created':
      return 'green';
    case 'AutoCreated':
      return 'cyan';
    case 'Missing':
      return 'gold';
    case 'Overdue':
      return 'red';
    default:
      return 'default';
  }
}

function autoCreateColor(status: MonatsbelegOpsRow['autoCreateStatus']): string {
  switch (status) {
    case 'Success':
      return 'green';
    case 'Pending':
      return 'blue';
    case 'Failed':
      return 'orange';
    case 'Missed':
      return 'red';
    default:
      return 'default';
  }
}

export type MonatsbelegOpsTableProps = {
  rows: MonatsbelegOpsRow[];
  loading?: boolean;
  pagination: TablePaginationConfig;
  variant: 'monats' | 'jahres';
  emptyText: string;
};

export function MonatsbelegOpsTable({
  rows,
  loading,
  pagination,
  variant,
  emptyText,
}: MonatsbelegOpsTableProps) {
  const { t } = useI18n();
  const tp = (path: string, options?: Record<string, string | number>) =>
    t(`rksvHub.monatsbelegePage.${path}`, options);

  const columns: ColumnsType<MonatsbelegOpsRow> = [
    {
      title: tp('colMonth'),
      dataIndex: 'period',
      key: 'period',
      width: 110,
    },
    {
      title: tp('colRegister'),
      key: 'register',
      ellipsis: true,
      render: (_, row) => {
        const loc = row.registerLocation?.trim();
        return loc ? `${row.registerNumber} — ${loc}` : row.registerNumber;
      },
    },
    {
      title: tp('colStatus'),
      dataIndex: 'displayStatus',
      key: 'displayStatus',
      width: 170,
      render: (status: MonatsbelegOpsRow['displayStatus']) => (
        <Tag color={displayStatusColor(status)} data-testid={`monatsbeleg-display-${status}`}>
          {tp(`displayStatus.${status}`)}
        </Tag>
      ),
    },
    {
      title: tp('colSalesGate'),
      dataIndex: 'salesGateMode',
      key: 'salesGateMode',
      width: 140,
      render: (mode: MonatsbelegOpsRow['salesGateMode']) => (
        <Tag data-testid={`monatsbeleg-gate-${mode}`}>{tp(`gate.${mode}`)}</Tag>
      ),
    },
    {
      title: tp('colAutoCreate'),
      dataIndex: 'autoCreateStatus',
      key: 'autoCreateStatus',
      width: 140,
      render: (status: MonatsbelegOpsRow['autoCreateStatus']) =>
        status === 'None' ? (
          <Typography.Text type="secondary">{tp('autoCreate.None')}</Typography.Text>
        ) : (
          <Tag color={autoCreateColor(status)} data-testid={`monatsbeleg-auto-${status}`}>
            {tp(`autoCreate.${status}`)}
          </Tag>
        ),
    },
    {
      title: tp('colFon'),
      dataIndex: 'fonStatus',
      key: 'fonStatus',
      width: 160,
      render: (v: string) => (
        <Tag color={fonColor(v)} data-testid={`monatsbeleg-fon-${v}`}>
          {FON_STATUSES.has(v) ? tp(`fon.${v}`) : v || '—'}
        </Tag>
      ),
    },
  ];

  if (variant === 'jahres') {
    columns.push({
      title: tp('colFonDeadline'),
      key: 'fonDeadline',
      width: 130,
      render: (_, row) => formatJahresbelegFonDeadline(row.year),
    });
  } else {
    columns.splice(2, 0, {
      title: tp('colCreatedAt'),
      dataIndex: 'createdAtUtc',
      key: 'createdAtUtc',
      width: 180,
      render: dateColumnRender('datetimeSeconds'),
    });
    columns.push({
      title: tp('colTse'),
      dataIndex: 'tseSignature',
      key: 'tseSignature',
      width: 180,
      ellipsis: true,
      render: (v: string) => v || '—',
    });
    columns.push({
      title: tp('colDep'),
      dataIndex: 'depStatus',
      key: 'depStatus',
      width: 110,
      render: (v: string) => (
        <Tag color={depColor(v)}>{DEP_STATUSES.has(v) ? tp(`dep.${v}`) : v || '—'}</Tag>
      ),
    });
    columns.push({
      title: tp('colCreatedBy'),
      dataIndex: 'createdBy',
      key: 'createdBy',
      width: 140,
      render: (v: string, row) => (
        <Space size={4}>
          <span>{v || '—'}</span>
          {row.autoCreated ? <Tag>{tp('autoTag')}</Tag> : null}
        </Space>
      ),
    });
  }

  return (
    <Table<MonatsbelegOpsRow>
      data-testid={variant === 'jahres' ? 'jahresbeleg-ops-table' : 'monatsbeleg-ops-table'}
      rowKey={(row) =>
        row.paymentId || `${row.cashRegisterId}-${row.period}-${row.status}-${row.displayStatus}`
      }
      columns={columns}
      dataSource={rows}
      loading={loading}
      pagination={pagination}
      locale={{ emptyText: <Typography.Text type="secondary">{emptyText}</Typography.Text> }}
    />
  );
}
