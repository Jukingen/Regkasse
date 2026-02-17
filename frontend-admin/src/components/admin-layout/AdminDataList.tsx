'use client';

import React, { ReactNode } from 'react';
import { Card, Spin, Alert, Empty } from 'antd';

interface AdminDataListProps {
    isLoading: boolean;
    isError?: boolean;
    error?: Error | null;
    isEmpty?: boolean;
    emptyText?: string;
    children: ReactNode;
}

export function AdminDataList({
    isLoading,
    isError,
    error,
    isEmpty,
    emptyText = 'No data found',
    children
}: AdminDataListProps) {
    if (isLoading) {
        return (
            <Card>
                <div style={{ textAlign: 'center', padding: '50px 0' }}>
                    <Spin size="large" />
                </div>
            </Card>
        );
    }

    if (isError) {
        return (
            <Card>
                <Alert
                    type="error"
                    message="Error loading data"
                    description={error?.message || 'Unknown error occurred'}
                    showIcon
                />
            </Card>
        );
    }

    if (isEmpty) {
        return (
            <Card>
                <Empty description={emptyText} />
            </Card>
        );
    }

    return <Card>{children}</Card>;
}
