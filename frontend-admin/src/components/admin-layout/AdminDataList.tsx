'use client';

import React, { ReactNode } from 'react';
import { Card, Spin, Alert, Empty } from 'antd';
import { useI18n } from '@/i18n';
import { ApiErrorAlertDescription } from '@/shared/errors/ApiErrorAlertDescription';

interface AdminDataListProps {
    isLoading: boolean;
    isError?: boolean;
    error?: Error | null;
    /** Label for technicalConsole (English only) */
    errorLogContext?: string;
    isEmpty?: boolean;
    emptyText?: string;
    children: ReactNode;
}

export function AdminDataList({
    isLoading,
    isError,
    error,
    errorLogContext = 'AdminDataList',
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
                    <Spin size="large" tip={t('common.dataList.loadingTip')} />
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
                    description={
                        error ? (
                            <ApiErrorAlertDescription
                                t={t}
                                error={error}
                                logContext={errorLogContext}
                                fallbackKey="common.messages.unknownError"
                            />
                        ) : (
                            t('common.messages.unknownError')
                        )
                    }
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
