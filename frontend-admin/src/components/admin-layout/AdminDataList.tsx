'use client';

import React, { ReactNode } from 'react';
import { Card, Spin, Alert, Empty } from 'antd';
import { useI18n } from '@/i18n';

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
    emptyText,
    children,
}: AdminDataListProps) {
    const { t } = useI18n();
    const resolvedEmptyDescription = emptyText ?? t('common.dataList.emptyDefault');

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
                    message={t('common.dataList.errorLoadTitle')}
                    description={error?.message ?? t('common.messages.unknownError')}
                    showIcon
                />
            </Card>
        );
    }

    if (isEmpty) {
        return (
            <Card>
                <Empty description={resolvedEmptyDescription} />
            </Card>
        );
    }

    return <Card>{children}</Card>;
}
