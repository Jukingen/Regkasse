'use client';

import dynamic from 'next/dynamic';
import { TableSkeleton } from '@/components/Skeleton';

const UsersPageContent = dynamic(
    () => import('@/features/users/components/UsersPageContent'),
    { loading: () => <TableSkeleton rows={10} cols={6} /> },
);

export default function UsersPage() {
    return <UsersPageContent />;
}
