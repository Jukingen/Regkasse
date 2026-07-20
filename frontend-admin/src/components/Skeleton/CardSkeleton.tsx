'use client';

import type { ReactNode } from 'react';
import { Card, Skeleton } from 'antd';

export type CardSkeletonProps = {
    count?: number;
    loading?: boolean;
    children?: ReactNode;
};

export function CardSkeleton({ count = 1, loading = true, children }: CardSkeletonProps) {
    if (!loading) return <>{children}</>;

    const safeCount = Math.max(1, count);

    return (
        <>
            {Array.from({ length: safeCount }).map((_, i) => (
                <Card key={i} style={{ marginBottom: 16 }}>
                    <Skeleton active paragraph={{ rows: 4 }} />
                </Card>
            ))}
        </>
    );
}
