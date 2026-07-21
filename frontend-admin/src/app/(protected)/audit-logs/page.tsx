'use client';

import dynamic from 'next/dynamic';

import { TableSkeleton } from '@/components/Skeleton';

const AuditLogsPageContent = dynamic(
  () => import('@/features/audit-logs/components/AuditLogsPageContent'),
  { loading: () => <TableSkeleton rows={10} cols={5} /> }
);

export default function AuditLogsPage() {
  return <AuditLogsPageContent />;
}
