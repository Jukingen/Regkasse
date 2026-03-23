'use client';

import React, { ReactNode } from 'react';
import { Typography, Space, Breadcrumb, Divider } from 'antd';
import Link from 'next/link';

const { Title } = Typography;

interface AdminPageHeaderProps {
    title: string;
    breadcrumbs?: Array<{ title: string; href?: string }>;
    actions?: ReactNode;
    children?: ReactNode;
}

export function AdminPageHeader({ title, breadcrumbs, actions, children }: AdminPageHeaderProps) {
    return (
        <div style={{ marginBottom: 24 }}>
            {breadcrumbs && (
                <nav aria-label="Brotkrümelnavigation" style={{ marginBottom: 16 }}>
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
