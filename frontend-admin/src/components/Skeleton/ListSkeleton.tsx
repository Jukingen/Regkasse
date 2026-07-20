'use client';

import type { ReactNode } from 'react';
import { List, Skeleton } from 'antd';

export type ListSkeletonProps = {
    count?: number;
    loading?: boolean;
    children?: ReactNode;
};

export function ListSkeleton({ count = 5, loading = true, children }: ListSkeletonProps) {
    if (!loading) return <>{children}</>;

    const safeCount = Math.max(1, count);

    const data = Array.from({ length: safeCount }).map((_, i) => ({
        key: i,
        title: <Skeleton.Input active size="small" block />,
        description: <Skeleton.Input active size="small" block style={{ width: '60%' }} />,
        avatar: <Skeleton.Avatar active size="large" />,
    }));

    return (
        <List
            dataSource={data}
            renderItem={(item) => (
                <List.Item>
                    <List.Item.Meta
                        avatar={item.avatar}
                        title={item.title}
                        description={item.description}
                    />
                </List.Item>
            )}
        />
    );
}
