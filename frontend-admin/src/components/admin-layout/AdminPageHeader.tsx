'use client';

import React, { ReactNode } from 'react';
import { Typography, Space } from 'antd';
import { Breadcrumb } from '@/components/Breadcrumb';
import type { AdminBreadcrumbItem } from '@/shared/adminShellLabels';

const { Title } = Typography;

interface AdminPageHeaderProps {
    /** Plain string or inline elements (e.g. title + status Tag). */
    title: React.ReactNode;
    breadcrumbs?: AdminBreadcrumbItem[];
    actions?: ReactNode;
    children?: ReactNode;
}

export function AdminPageHeader({ title, breadcrumbs, actions, children }: AdminPageHeaderProps) {
    return (
        <div style={{ marginBottom: 24 }}>
            {breadcrumbs && <Breadcrumb items={breadcrumbs} />}

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
