'use client';

import type { ReactNode } from 'react';

import { TableSkeleton } from './TableSkeleton';

type InitialTableSkeletonProps = {
  loading: boolean;
  rowCount: number;
  rows?: number;
  cols?: number;
  children: ReactNode;
};

/** Skeleton rows on the first load. Existing rows stay visible while a later page loads. */
export function InitialTableSkeleton({
  loading,
  rowCount,
  rows = 8,
  cols = 4,
  children,
}: InitialTableSkeletonProps) {
  if (loading && rowCount === 0) {
    return <TableSkeleton rows={rows} cols={cols} />;
  }
  return <>{children}</>;
}
