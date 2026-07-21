'use client';

/**
 * Shared loading fallbacks for `next/dynamic`.
 *
 * Next.js requires the **options argument** to be an object literal
 * (see https://nextjs.org/docs/messages/invalid-dynamic-options-type).
 * Reuse these loaders inside inline options, e.g.:
 *
 *   dynamic(() => import('./Chart'), { ssr: false, loading: DynamicChartLoading })
 */
import { Skeleton } from 'antd';

import { PageSkeleton, TableSkeleton } from '@/components/Skeleton';

export function DynamicChartLoading() {
  return <Skeleton active paragraph={{ rows: 6 }} />;
}

export function DynamicChartCompactLoading() {
  return <Skeleton active paragraph={{ rows: 4 }} />;
}

export function DynamicChartPageLoading() {
  return <Skeleton active paragraph={{ rows: 10 }} />;
}

export function DynamicPageLoading4() {
  return <PageSkeleton widgets={4} />;
}

export function DynamicPageLoading5() {
  return <PageSkeleton widgets={5} />;
}

export function DynamicPageLoading6() {
  return <PageSkeleton widgets={6} />;
}

export function DynamicTableLoading() {
  return <TableSkeleton rows={10} cols={6} />;
}

export function DynamicTableLoadingCompact() {
  return <TableSkeleton rows={8} cols={5} />;
}

export function DynamicTableLoadingAudit() {
  return <TableSkeleton rows={10} cols={5} />;
}

/** Modals/drawers: no placeholder (chunk loads on open). */
export function DynamicModalLoading() {
  return null;
}
