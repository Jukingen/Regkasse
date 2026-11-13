'use client';

import { Skeleton, Table } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import type { ReactNode } from 'react';

export type TableSkeletonProps = {
  rows?: number;
  cols?: number;
  loading?: boolean;
  children?: ReactNode;
};

type SkeletonRow = {
  key: number;
  [colKey: string]: number | string;
};

export function TableSkeleton({
  rows = 5,
  cols = 4,
  loading = true,
  children,
}: TableSkeletonProps) {
  if (!loading) return <>{children}</>;

  const safeRows = Math.max(1, rows);
  const safeCols = Math.max(1, cols);

  const columns: ColumnsType<SkeletonRow> = Array.from({ length: safeCols }).map((_, i) => ({
    title: <Skeleton.Input active size="small" style={{ width: 96 }} />,
    dataIndex: `col${i}`,
    key: `col${i}`,
    render: () => <Skeleton.Input active size="small" block />,
  }));

  const data: SkeletonRow[] = Array.from({ length: safeRows }).map((_, i) => ({
    key: i,
    ...Object.fromEntries(Array.from({ length: safeCols }).map((_, j) => [`col${j}`, ''])),
  }));

  return <Table<SkeletonRow> columns={columns} dataSource={data} pagination={false} />;
}
