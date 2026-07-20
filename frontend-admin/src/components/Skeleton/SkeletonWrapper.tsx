'use client';

import type { ReactNode } from 'react';
import { CardSkeleton } from './CardSkeleton';
import { FormSkeleton } from './FormSkeleton';
import { ListSkeleton } from './ListSkeleton';
import { PageSkeleton } from './PageSkeleton';
import { TableSkeleton } from './TableSkeleton';
import { WidgetSkeleton } from './WidgetSkeleton';

export type SkeletonType = 'card' | 'table' | 'list' | 'form' | 'widget' | 'page';

export type SkeletonWrapperProps = {
    type?: SkeletonType;
    loading?: boolean;
    count?: number;
    children: ReactNode;
};

export function SkeletonWrapper({
    type = 'card',
    loading = true,
    count = 1,
    children,
}: SkeletonWrapperProps) {
    if (!loading) return <>{children}</>;

    switch (type) {
        case 'table':
            return <TableSkeleton rows={count} loading />;
        case 'list':
            return <ListSkeleton count={count} loading />;
        case 'form':
            return <FormSkeleton fields={count} loading />;
        case 'widget':
            return (
                <>
                    {Array.from({ length: Math.max(1, count) }).map((_, i) => (
                        <WidgetSkeleton key={i} />
                    ))}
                </>
            );
        case 'page':
            return <PageSkeleton widgets={count} />;
        case 'card':
        default:
            return <CardSkeleton count={count} loading />;
    }
}
