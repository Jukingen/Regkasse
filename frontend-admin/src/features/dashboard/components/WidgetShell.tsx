'use client';

import React from 'react';
import { Button, Card, Space, Tooltip } from 'antd';
import { HolderOutlined, ReloadOutlined } from '@ant-design/icons';
import { useI18n } from '@/i18n/I18nProvider';

export type WidgetShellProps = {
    title: string;
    dragHandleProps?: React.HTMLAttributes<HTMLButtonElement>;
    onRefresh?: () => void;
    refreshing?: boolean;
    extra?: React.ReactNode;
    children: React.ReactNode;
};

/** Shared card chrome: title, manual refresh, optional drag handle. */
export function WidgetShell({
    title,
    dragHandleProps,
    onRefresh,
    refreshing,
    extra,
    children,
}: WidgetShellProps) {
    const { t } = useI18n();
    const refreshLabel = t('dashboard.widgetShell.refresh');
    const dragLabel = t('dashboard.widgetShell.drag_widget');

    return (
        <Card
            variant="borderless"
            title={
                <Space size="small">
                    {dragHandleProps ? (
                        <button
                            type="button"
                            aria-label={dragLabel}
                            {...dragHandleProps}
                            style={{
                                cursor: 'grab',
                                border: 'none',
                                background: 'transparent',
                                padding: 0,
                                display: 'inline-flex',
                                color: 'rgba(0,0,0,0.45)',
                            }}
                        >
                            <HolderOutlined />
                        </button>
                    ) : null}
                    <span>{title}</span>
                </Space>
            }
            extra={
                <Space>
                    {extra}
                    {onRefresh ? (
                        <Tooltip title={refreshLabel}>
                            <Button
                                type="text"
                                size="small"
                                icon={<ReloadOutlined spin={refreshing} />}
                                onClick={onRefresh}
                                disabled={refreshing}
                                aria-label={refreshLabel}
                            />
                        </Tooltip>
                    ) : null}
                </Space>
            }
            style={{ height: '100%' }}
        >
            {children}
        </Card>
    );
}
