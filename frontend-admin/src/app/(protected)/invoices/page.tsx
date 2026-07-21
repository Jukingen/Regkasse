'use client';

import dynamic from 'next/dynamic';

import { TableSkeleton } from '@/components/Skeleton';

const InvoiceList = dynamic(
  () =>
    import('@/features/invoices/components/InvoiceList').then((m) => ({
      default: m.InvoiceList,
    })),
  { loading: () => <TableSkeleton rows={10} cols={6} /> }
);

export default function InvoicesPage() {
  return <InvoiceList />;
}
