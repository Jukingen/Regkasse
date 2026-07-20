'use client';

import type { ReactNode } from 'react';
import { Button, Empty } from 'antd';

import { useI18n } from '@/i18n';

export type EmptyStateProps = {
    title?: string;
    description?: string;
    icon?: ReactNode;
    actionText?: string;
    onAction?: () => void;
};

export function EmptyState({
    title,
    description,
    icon,
    actionText,
    onAction,
}: EmptyStateProps) {
    const { t } = useI18n();

    const resolvedTitle = title ?? t('common.emptyState.defaultTitle');
    const resolvedDescription = description ?? t('common.emptyState.defaultDescription');

    return (
        <Empty
            image={icon ?? Empty.PRESENTED_IMAGE_SIMPLE}
            description={
                <div>
                    <p style={{ fontSize: 16, fontWeight: 500, marginBottom: 4 }}>{resolvedTitle}</p>
                    <p style={{ color: 'var(--ant-color-text-secondary)', marginBottom: 0 }}>
                        {resolvedDescription}
                    </p>
                </div>
            }
        >
            {actionText && onAction ? (
                <Button type="primary" onClick={onAction}>
                    {actionText}
                </Button>
            ) : null}
        </Empty>
    );
}
