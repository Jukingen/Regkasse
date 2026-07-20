'use client';

import type { ReactNode } from 'react';
import { Card, Skeleton, Space } from 'antd';

export type FormSkeletonProps = {
    fields?: number;
    loading?: boolean;
    children?: ReactNode;
};

export function FormSkeleton({ fields = 4, loading = true, children }: FormSkeletonProps) {
    if (!loading) return <>{children}</>;

    const safeFields = Math.max(1, fields);

    return (
        <Card>
            <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                {Array.from({ length: safeFields }).map((_, i) => (
                    <div key={i}>
                        <Skeleton.Input active size="small" style={{ width: 120, marginBottom: 4 }} />
                        <Skeleton.Input active size="large" block />
                    </div>
                ))}
                <Skeleton.Button active size="large" block />
            </Space>
        </Card>
    );
}
