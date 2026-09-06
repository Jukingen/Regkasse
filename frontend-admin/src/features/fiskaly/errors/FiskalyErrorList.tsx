'use client';

import { Button, Space, Table, Tag } from 'antd';
import type { ColumnsType, TablePaginationConfig } from 'antd/es/table';

import type { FiskalyErrorListItem, FiskalyErrorReviewStatus } from '@/features/fiskaly/api/fiskalyErrors';

export type FiskalyErrorListProps = {
  items: FiskalyErrorListItem[];
  loading: boolean;
  isSuperAdmin: boolean;
  canResolve: boolean;
  resolvePending: boolean;
  page: number;
  pageSize: number;
  total: number;
  emptyText: string;
  labels: {
    date: string;
    code: string;
    message: string;
    type: string;
    tenant: string;
    user: string;
    status: string;
    actions: string;
    markResolved: string;
    markKnownIssue: string;
    markOpen: string;
  };
  kindLabel: (value: string) => string;
  reviewLabel: (status: string) => string;
  formatDate: (value: string) => string;
  onPageChange: (page: number, pageSize: number) => void;
  onSelect: (id: string) => void;
  onResolve: (id: string, status: FiskalyErrorReviewStatus) => void;
};

function reviewColor(status: string): 'default' | 'success' | 'warning' | 'error' {
  if (status === 'resolved') return 'success';
  if (status === 'known_issue') return 'warning';
  return 'error';
}

export function FiskalyErrorList({
  items,
  loading,
  isSuperAdmin,
  canResolve,
  resolvePending,
  page,
  pageSize,
  total,
  emptyText,
  labels,
  kindLabel,
  reviewLabel,
  formatDate,
  onPageChange,
  onSelect,
  onResolve,
}: FiskalyErrorListProps) {
  const columns: ColumnsType<FiskalyErrorListItem> = [
    {
      title: labels.date,
      dataIndex: 'createdAtUtc',
      render: (value: string) => formatDate(value),
    },
    {
      title: labels.code,
      dataIndex: 'errorCode',
      render: (value: string | null | undefined) => value || '—',
    },
    {
      title: labels.message,
      dataIndex: 'errorMessage',
      ellipsis: true,
      render: (value: string | null | undefined) => value || '—',
    },
    {
      title: labels.type,
      dataIndex: 'operationType',
      render: (value: string) => kindLabel(value),
    },
    ...(isSuperAdmin
      ? [
          {
            title: labels.tenant,
            dataIndex: 'tenantName',
            render: (_: unknown, row: FiskalyErrorListItem) => row.tenantName || row.tenantId,
          },
        ]
      : []),
    {
      title: labels.user,
      dataIndex: 'userDisplayName',
      render: (_: unknown, row: FiskalyErrorListItem) => row.userDisplayName || row.userId,
    },
    {
      title: labels.status,
      dataIndex: 'reviewStatus',
      render: (value: string) => <Tag color={reviewColor(value)}>{reviewLabel(value)}</Tag>,
    },
    ...(canResolve
      ? [
          {
            title: labels.actions,
            key: 'actions',
            render: (_: unknown, row: FiskalyErrorListItem) => (
              <Space size="small" onClick={(event) => event.stopPropagation()}>
                <Button
                  size="small"
                  disabled={row.reviewStatus === 'resolved' || resolvePending}
                  onClick={() => onResolve(row.id, 'resolved')}
                >
                  {labels.markResolved}
                </Button>
                <Button
                  size="small"
                  disabled={row.reviewStatus === 'known_issue' || resolvePending}
                  onClick={() => onResolve(row.id, 'known_issue')}
                >
                  {labels.markKnownIssue}
                </Button>
                {row.reviewStatus !== 'open' ? (
                  <Button size="small" disabled={resolvePending} onClick={() => onResolve(row.id, 'open')}>
                    {labels.markOpen}
                  </Button>
                ) : null}
              </Space>
            ),
          },
        ]
      : []),
  ];

  const pagination: TablePaginationConfig = {
    current: page,
    pageSize,
    total,
    showSizeChanger: true,
    onChange: onPageChange,
  };

  return (
    <Table<FiskalyErrorListItem>
      rowKey="id"
      size="small"
      loading={loading}
      columns={columns}
      dataSource={items}
      pagination={pagination}
      locale={{ emptyText }}
      onRow={(row) => ({
        onClick: () => onSelect(row.id),
        style: { cursor: 'pointer' },
      })}
    />
  );
}
