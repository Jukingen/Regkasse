'use client';

import dynamic from 'next/dynamic';

import { TableSkeleton } from '@/components/Skeleton';

const ProductsPageContent = dynamic(
  () => import('@/features/products/components/ProductsPageContent'),
  { loading: () => <TableSkeleton rows={10} cols={6} /> }
);

export default function ProductsPage() {
  return <ProductsPageContent />;
}
