'use client';

import React from 'react';
import { Button, Card, Space, Tooltip } from 'antd';
import { HolderOutlined, ReloadOutlined } from '@ant-design/icons';

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
    return (
        <Card
            bordered={false}
            title={
                <Space size="small">
                    {dragHandleProps ? (
                        <button
                            type="button"
                            aria-label="Widget verschieben"
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
                        <Tooltip title="Aktualisieren">
                            <Button
                                type="text"
                                size="small"
                                icon={<ReloadOutlined spin={refreshing} />}
                                onClick={onRefresh}
                                disabled={refreshing}
                                aria-label="Aktualisieren"
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
