'use client';

import React, { ReactNode } from 'react';
import { Typography, Space, Breadcrumb } from 'antd';
import Link from 'next/link';
import { useI18n } from '@/i18n';

const { Title } = Typography;

interface AdminPageHeaderProps {
    /** Plain string or inline elements (e.g. title + status Tag). */
    title: React.ReactNode;
    breadcrumbs?: Array<{ title: string; href?: string }>;
    actions?: ReactNode;
    children?: ReactNode;
}

export function AdminPageHeader({ title, breadcrumbs, actions, children }: AdminPageHeaderProps) {
    const { t } = useI18n();

    return (
        <div style={{ marginBottom: 24 }}>
            {breadcrumbs && (
                <nav aria-label={t('common.aria.breadcrumbNav')} style={{ marginBottom: 16 }}>
                    <Breadcrumb
                        items={breadcrumbs.map((b) => ({
                            title: b.href ? <Link href={b.href}>{b.title}</Link> : b.title,
                        }))}
                    />
                </nav>
            )}

            <header>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <Title level={1} style={{ margin: 0 }}>
                        {title}
                    </Title>
                    {actions && <Space>{actions}</Space>}
                </div>
            </header>

            {children ? <div style={{ marginTop: 16 }}>{children}</div> : null}
        </div>
    );
}
