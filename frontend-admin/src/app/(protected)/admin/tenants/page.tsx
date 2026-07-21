'use client';

import dynamic from 'next/dynamic';

import { TableSkeleton } from '@/components/Skeleton';

const TenantsTable = dynamic(() => import('@/features/super-admin/components/TenantsTable'), {
  loading: () => <TableSkeleton rows={8} cols={5} />,
});

export default function SuperAdminTenantsPage() {
  return <TenantsTable />;
}
