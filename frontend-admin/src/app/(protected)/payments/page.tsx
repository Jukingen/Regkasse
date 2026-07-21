'use client';

import dynamic from 'next/dynamic';

import { TableSkeleton } from '@/components/Skeleton';

const PaymentsPageContent = dynamic(
  () => import('@/features/payments/components/PaymentsPageContent'),
  { loading: () => <TableSkeleton rows={10} cols={6} /> }
);

export default function PaymentsPage() {
  return <PaymentsPageContent />;
}
