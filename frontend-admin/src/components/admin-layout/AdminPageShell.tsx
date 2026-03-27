'use client';

import React from 'react';
import { Space, Typography } from 'antd';
import { useI18n } from '@/i18n';

/**
 * Standard vertical page wrapper for protected admin list/operation screens.
 * Matches spacing used on receipts/products (Space large + full width + bottom padding).
 */
export function AdminPageShell({ children }: { children: React.ReactNode }) {
    return (
        <Space direction="vertical" size="large" style={{ width: '100%', paddingBottom: 24 }}>
            {children}
        </Space>
    );
}

/**
 * Compact scope / filter summary strip (operator scanability).
 */
export function AdminPageScopeSummary({
    label,
    children,
}: {
    label: React.ReactNode;
    children: React.ReactNode;
}) {
    const { t } = useI18n();

    return (
        <section
            aria-label={t('common.aria.pageScopeContext')}
            style={{
                marginBottom: 0,
                marginTop: 0,
                fontSize: 12,
                padding: '8px 12px',
                background: 'var(--ant-color-fill-quaternary)',
                borderRadius: 6,
            }}
        >
            <Typography.Paragraph type="secondary" style={{ marginBottom: 0, marginTop: 0, fontSize: 12 }}>
                <Typography.Text strong style={{ fontSize: 12 }}>
                    {label}
                </Typography.Text>{' '}
                {children}
            </Typography.Paragraph>
        </section>
    );
}
