'use client';

import dynamic from 'next/dynamic';

import { TableSkeleton } from '@/components/Skeleton';

const ReceiptsPageContent = dynamic(
  () => import('@/features/receipts/components/ReceiptsPageContent'),
  { loading: () => <TableSkeleton rows={10} cols={6} /> }
);

export default function ReceiptsPage() {
  return <ReceiptsPageContent />;
}
